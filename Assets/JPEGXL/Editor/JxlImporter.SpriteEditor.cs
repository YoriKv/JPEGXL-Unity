#if JXL_2D_SPRITE
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>
    /// Sprite Editor support (com.unity.2d.sprite). The Sprite Editor window finds the data provider by casting the
    /// asset's importer to <see cref="ISpriteEditorDataProvider"/> (the same route Unity's PSD importer uses), so the
    /// importer implements the interfaces itself. Edits are staged in <c>m_EditRects</c> and only written to the
    /// serialized fields (<see cref="spriteSheet"/> and the pivot/border in <see cref="textureSettings"/>) by
    /// <see cref="ISpriteEditorDataProvider.Apply"/>; the window then reserializes the .meta and reimports.
    /// Supported: Sprite Frame module (Single and Multiple, slicing, names, pivots, borders). Not supported: custom
    /// outlines, physics shapes, secondary textures and bones (those modules are hidden by <see cref="HasDataProvider"/>).
    /// </summary>
    public sealed partial class JxlImporter : ISpriteEditorDataProvider, ITextureDataProvider
    {
        [NonSerialized] List<SpriteRect> m_EditRects;
        [NonSerialized] SpriteRect m_EditSingle;
        [NonSerialized] Texture2D m_ReadableTexture;
        [NonSerialized] int m_SourceWidth;
        [NonSerialized] int m_SourceHeight;

        static readonly EmptyOutlineProvider s_EmptyOutlines = new EmptyOutlineProvider();

        /// <summary>The Sprite Editor's view of the sprite mode: None unless the texture type is Sprite; Polygon is treated as Single.</summary>
        public SpriteImportMode spriteImportMode
        {
            get
            {
                if (textureSettings == null || textureSettings.textureType != TextureImporterType.Sprite)
                    return SpriteImportMode.None;
                return textureSettings.spriteMode == (int)SpriteImportMode.Multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            }
        }

        float ISpriteEditorDataProvider.pixelsPerUnit => textureSettings != null ? textureSettings.spritePixelsPerUnit : 100f;

        UnityEngine.Object ISpriteEditorDataProvider.targetObject => this;

        void ISpriteEditorDataProvider.InitSpriteEditorDataProvider()
        {
            LoadEditRects();
        }

        SpriteRect[] ISpriteEditorDataProvider.GetSpriteRects()
        {
            EnsureEditRects();
            if (spriteImportMode == SpriteImportMode.Multiple)
            {
                var result = new SpriteRect[m_EditRects.Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = Copy(m_EditRects[i]);
                return result;
            }
            return new[] { Copy(m_EditSingle) };
        }

        void ISpriteEditorDataProvider.SetSpriteRects(SpriteRect[] spriteRects)
        {
            EnsureEditRects();
            if (spriteRects == null)
                return;
            if (spriteImportMode == SpriteImportMode.Multiple)
            {
                m_EditRects = new List<SpriteRect>(spriteRects.Length);
                foreach (SpriteRect rect in spriteRects)
                {
                    if (rect != null)
                        m_EditRects.Add(Copy(rect));
                }
            }
            else if (spriteImportMode != SpriteImportMode.None && spriteRects.Length == 1 && spriteRects[0] != null)
            {
                m_EditSingle = Copy(spriteRects[0]);
            }
        }

        /// <summary>Writes the staged rects to the serialized fields. The caller saves and reimports.</summary>
        void ISpriteEditorDataProvider.Apply()
        {
            EnsureEditRects();
            if (textureSettings == null)
                textureSettings = CreateDefaultTextureSettings();

            // Single mode keeps its pivot and border where TextureImporter does: in the texture settings.
            textureSettings.spriteAlignment = (int)m_EditSingle.alignment;
            textureSettings.spritePivot = m_EditSingle.pivot;
            textureSettings.spriteBorder = m_EditSingle.border;

            var sheet = new List<JxlSpriteRect>(m_EditRects.Count);
            foreach (SpriteRect rect in m_EditRects)
            {
                sheet.Add(new JxlSpriteRect
                {
                    name = rect.name,
                    rect = rect.rect,
                    alignment = rect.alignment,
                    pivot = rect.pivot,
                    border = rect.border,
                    spriteID = rect.spriteID.ToString()
                });
            }
            spriteSheet = sheet;
            EditorUtility.SetDirty(this);
        }

        T ISpriteEditorDataProvider.GetDataProvider<T>()
        {
            // The Sprite Frame module calls SetOutlines after automatic slicing; hand it a provider that keeps nothing.
            if (typeof(T) == typeof(ISpriteOutlineDataProvider) || typeof(T) == typeof(ISpritePhysicsOutlineDataProvider))
                return s_EmptyOutlines as T;
            return this as T;
        }

        /// <summary>
        /// Only the frame data and the texture are supported. Returning false for outlines, physics shapes and secondary
        /// textures hides those Sprite Editor modules instead of letting them edit data that would be discarded.
        /// </summary>
        public bool HasDataProvider(Type type)
        {
            return type == typeof(ISpriteEditorDataProvider) || type == typeof(ITextureDataProvider);
        }

        // ------------------------------------------------------------------ ITextureDataProvider

        Texture2D ITextureDataProvider.texture => AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        Texture2D ITextureDataProvider.previewTexture => AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        /// <summary>Source image size (before NPOT scaling and max size), the space sprite rects live in.</summary>
        void ITextureDataProvider.GetTextureActualWidthAndHeight(out int width, out int height)
        {
            EnsureSourceSize();
            width = m_SourceWidth;
            height = m_SourceHeight;
        }

        /// <summary>
        /// A readable RGBA32 copy at source size, decoded on demand (the imported texture may be compressed, resized or
        /// non-readable). Used by automatic and grid slicing. HDR files are decoded to 8-bit sRGB for this purpose.
        /// </summary>
        Texture2D ITextureDataProvider.GetReadableTexture2D()
        {
            if (m_ReadableTexture == null)
                m_ReadableTexture = DecodeReadableTexture(assetPath, convertToSRGB, unpremultiplyAlpha);
            return m_ReadableTexture;
        }

        /// <summary>Decodes <paramref name="path"/> to a readable, uncompressed RGBA32 texture in Unity row order.</summary>
        internal static Texture2D DecodeReadableTexture(string path, bool convertToSrgb, bool unpremultiply)
        {
            if (string.IsNullOrEmpty(path) || !JxlDecoder.IsAvailable)
                return null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                JxlImageInfo info = JxlDecoder.ReadInfo(data);
                bool convert = info.IsHdr || (convertToSrgb && (info.IsWideGamut || info.Transfer != JxlTransfer.SRgb));
                var options = new JxlDecodeOptions
                {
                    Output = JxlPixelOutput.Rgba8,
                    ColorTarget = convert ? JxlColorTarget.SRgb : JxlColorTarget.Original,
                    FlipVertically = true,
                    UnpremultiplyAlpha = unpremultiply,
                    DesiredIntensityTarget = 0f
                };
                JxlDecodedImage decoded = JxlDecoder.Decode(data, options);
                JxlImageInfo decodedInfo = decoded.Info ?? info;
                var tex = new Texture2D(decodedInfo.Width, decodedInfo.Height, TextureFormat.RGBA32, false, false)
                {
                    name = Path.GetFileNameWithoutExtension(path) + " (readable)",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point
                };
                tex.LoadRawTextureData(decoded.Pixels);
                tex.Apply(false, false);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"JPEG XL: could not decode '{path}' for the Sprite Editor: {e.Message}");
                return null;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The importer instance is replaced on every reimport; free the decoded copy with it.</summary>
        void OnDestroy()
        {
            if (m_ReadableTexture != null)
            {
                DestroyImmediate(m_ReadableTexture);
                m_ReadableTexture = null;
            }
        }

        void EnsureEditRects()
        {
            if (m_EditRects == null || m_EditSingle == null)
                LoadEditRects();
        }

        void LoadEditRects()
        {
            if (m_ReadableTexture != null)
            {
                DestroyImmediate(m_ReadableTexture);
                m_ReadableTexture = null;
            }
            m_SourceWidth = m_SourceHeight = 0;
            EnsureSourceSize();

            m_EditRects = new List<SpriteRect>();
            if (spriteSheet != null)
            {
                for (int i = 0; i < spriteSheet.Count; i++)
                {
                    JxlSpriteRect entry = spriteSheet[i];
                    if (entry == null)
                        continue;
                    m_EditRects.Add(new SpriteRect
                    {
                        name = entry.name,
                        rect = entry.rect,
                        alignment = entry.alignment,
                        pivot = entry.pivot,
                        border = entry.border,
                        spriteID = new GUID(ResolveSpriteId(entry, assetPath, i))
                    });
                }
            }

            TextureImporterSettings ts = textureSettings ?? CreateDefaultTextureSettings();
            m_EditSingle = new SpriteRect
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                rect = new Rect(0, 0, m_SourceWidth, m_SourceHeight),
                alignment = (SpriteAlignment)ts.spriteAlignment,
                pivot = ts.spritePivot,
                border = ts.spriteBorder,
                spriteID = new GUID(StableSpriteId(assetPath))
            };
        }

        void EnsureSourceSize()
        {
            if (m_SourceWidth > 0 && m_SourceHeight > 0)
                return;
            try
            {
                JxlImageInfo info = JxlDecoder.IsAvailable && File.Exists(assetPath) ? JxlDecoder.ReadInfo(File.ReadAllBytes(assetPath)) : null;
                m_SourceWidth = info?.Width ?? 0;
                m_SourceHeight = info?.Height ?? 0;
            }
            catch (Exception)
            {
                m_SourceWidth = m_SourceHeight = 0;
            }
            if (m_SourceWidth <= 0 || m_SourceHeight <= 0)
            {
                // Fall back to the imported texture (placeholder or decoder unavailable).
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                m_SourceWidth = tex != null ? tex.width : 0;
                m_SourceHeight = tex != null ? tex.height : 0;
            }
        }

        static SpriteRect Copy(SpriteRect source)
        {
            return new SpriteRect
            {
                name = source.name,
                rect = source.rect,
                alignment = source.alignment,
                pivot = source.pivot,
                border = source.border,
                customData = source.customData,
                spriteID = source.spriteID
            };
        }

        /// <summary>Outlines and physics shapes are out of scope: reads return no outline, writes are ignored.</summary>
        sealed class EmptyOutlineProvider : ISpriteOutlineDataProvider, ISpritePhysicsOutlineDataProvider
        {
            public List<Vector2[]> GetOutlines(GUID guid) => new List<Vector2[]>();
            public void SetOutlines(GUID guid, List<Vector2[]> data) { }
            public float GetTessellationDetail(GUID guid) => 0f;
            public void SetTessellationDetail(GUID guid, float value) { }
        }
    }
}
#endif
