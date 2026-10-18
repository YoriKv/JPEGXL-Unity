using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>Colour primaries for imported HDR textures.</summary>
    public enum JxlHdrPrimaries
    {
        /// <summary>sRGB / Rec.709 primaries (Unity's default linear working space).</summary>
        SRgb,
        /// <summary>Rec.2100 / Rec.2020 primaries (wide gamut).</summary>
        Rec2100
    }

    /// <summary>
    /// One sprite of a Multiple-mode sprite sheet, the JXL equivalent of <c>SpriteMetaData</c> in a TextureImporter's
    /// <c>m_SpriteSheet.m_Sprites</c>. <see cref="rect"/> is in source-image pixels with the origin at the bottom-left,
    /// like TextureImporter; Unity scales it when the imported texture is resized (max size, NPOT).
    /// </summary>
    [Serializable]
    public sealed class JxlSpriteRect
    {
        /// <summary>Sprite name; empty names import as <c>&lt;file&gt;_&lt;index&gt;</c>.</summary>
        public string name;
        /// <summary>Rect in source pixels, origin bottom-left.</summary>
        public Rect rect;
        /// <summary>Pivot preset; <see cref="pivot"/> applies when it is <see cref="SpriteAlignment.Custom"/>.</summary>
        public SpriteAlignment alignment = SpriteAlignment.Center;
        /// <summary>Normalised pivot within <see cref="rect"/>.</summary>
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        /// <summary>Left, bottom, right, top border in pixels (9-slicing).</summary>
        public Vector4 border;
        /// <summary>Stable sprite ID as a GUID string (32 hex digits). Also the sub-asset identifier, so renaming keeps references.</summary>
        public string spriteID;
    }

    /// <summary>
    /// Imports <c>.jxl</c> files as <see cref="Texture2D"/> assets (plus a <see cref="Sprite"/> sub-asset for the Sprite type).
    /// Pixels come from the bundled libjxl decoder; the texture itself is built by Unity's own texture import pipeline
    /// (<see cref="TextureGenerator.GenerateTexture(TextureGenerationSettings, NativeArray{Color32})"/>), so texture types,
    /// NPOT scaling, mipmaps, compression formats, max size and per-platform overrides behave exactly as for a <c>.jpg</c>.
    /// SDR files go through the <see cref="Color32"/> overload; HDR files (PQ, HLG or float samples) are decoded to linear
    /// float and go through the <see cref="Color"/> overload, which yields RGBAHalf (uncompressed) or BC6H (compressed).
    /// </summary>
    [ScriptedImporter(2, "jxl")]
    public sealed partial class JxlImporter : ScriptedImporter
    {
        /// <summary>Name of the platform settings entry used when no override applies (same as <see cref="TextureImporter"/>).</summary>
        internal const string DefaultPlatformName = "DefaultTexturePlatform";

        /// <summary>Platforms that can carry overrides, named like <see cref="TextureImporter"/> platform settings.</summary>
        internal static readonly IReadOnlyList<string> OverridePlatformNames = new[] { "Standalone", "Android", "iPhone", "WebGL" };

        [Tooltip("Texture importer settings (type, sRGB, alpha, mipmaps, NPOT, sprite and normal map options, filter and wrap modes).")]
        public TextureImporterSettings textureSettings = CreateDefaultTextureSettings();

        [Tooltip("Platform settings used when no platform override is active.")]
        public TextureImporterPlatformSettings defaultPlatformSettings = CreatePlatformSettings(DefaultPlatformName);

        [Tooltip("Per-platform overrides. An entry applies when its name matches the build target group and 'overridden' is set.")]
        public List<TextureImporterPlatformSettings> platformOverrides = CreateDefaultOverrides();

        [Tooltip("For SDR files whose primaries or transfer function are not sRGB (for example Rec.2100 or P3), convert the pixels to sRGB during decoding. When off the original encoding is kept.")]
        public bool convertToSRGB = true;

        [Tooltip("Ask the decoder to undo premultiplied alpha when the file stores premultiplied color.")]
        public bool unpremultiplyAlpha = true;

        [Tooltip("HDR (PQ/HLG) files: luminance in nits that maps to 1.0 in the texture. 203 is the ITU-R BT.2408 HDR reference white, matching Unity's convention that 1.0 is SDR white. 0 keeps libjxl's scale, where 1.0 is the file's intensity target (its peak, usually 10000 nits for PQ and 1000 for HLG). Not applied to linear floating-point files.")]
        [Min(0f)]
        public float hdrReferenceWhiteNits = 203f;

        [Tooltip("HDR files: colour primaries of the imported linear texture. sRGB (Rec.709) matches Unity's linear working space; out-of-gamut colours get negative components (see Clamp Negative). Rec.2100 keeps the wide gamut for projects that render in Rec.2020.")]
        public JxlHdrPrimaries hdrPrimaries = JxlHdrPrimaries.SRgb;

        [Tooltip("HDR files: clamp negative components (out-of-gamut colours after conversion to sRGB primaries) to 0. BC6H cannot store negative values.")]
        public bool hdrClampNegative = true;

        [Tooltip("Sprite Mode Multiple: the sprites cut from the texture (edited with the Sprite Editor). Rects are in source pixels, origin bottom-left. Single mode ignores this list and uses the pivot and border in Texture Settings.")]
        public List<JxlSpriteRect> spriteSheet = new List<JxlSpriteRect>();

        /// <summary>Texture settings matching what <see cref="TextureImporter"/> uses for a newly imported image of type Default.</summary>
        internal static TextureImporterSettings CreateDefaultTextureSettings()
        {
            return new TextureImporterSettings
            {
                textureType = TextureImporterType.Default,
                textureShape = TextureImporterShape.Texture2D,
                sRGBTexture = true,
                alphaSource = TextureImporterAlphaSource.FromInput,
                alphaIsTransparency = false,
                alphaTestReferenceValue = 0.5f,
                npotScale = TextureImporterNPOTScale.ToNearest,
                readable = false,
                streamingMipmaps = false,
                streamingMipmapsPriority = 0,
                mipmapEnabled = true,
                mipmapFilter = TextureImporterMipFilter.BoxFilter,
                borderMipmap = false,
                mipMapsPreserveCoverage = false,
                fadeOut = false,
                mipmapFadeDistanceStart = 1,
                mipmapFadeDistanceEnd = 3,
                convertToNormalMap = false,
                heightmapScale = 0.25f,
                normalMapFilter = TextureImporterNormalFilter.Standard,
                generateCubemap = TextureImporterGenerateCubemap.AutoCubemap,
                cubemapConvolution = TextureImporterCubemapConvolution.None,
                seamlessCubemap = false,
                spriteMode = (int)SpriteImportMode.Single,
                spriteExtrude = 1,
                spriteMeshType = SpriteMeshType.Tight,
                spriteAlignment = (int)SpriteAlignment.Center,
                spritePivot = new Vector2(0.5f, 0.5f),
                spritePixelsPerUnit = 100f,
                spriteBorder = Vector4.zero,
                spriteGenerateFallbackPhysicsShape = true,
                spriteTessellationDetail = -1f,
                filterMode = FilterMode.Bilinear,
                aniso = 1,
                mipmapBias = 0f,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Repeat,
                wrapModeW = TextureWrapMode.Repeat,
            };
        }

        /// <summary>Platform settings with the same defaults as a fresh <see cref="TextureImporter"/>.</summary>
        internal static TextureImporterPlatformSettings CreatePlatformSettings(string name)
        {
            return new TextureImporterPlatformSettings
            {
                name = name,
                overridden = false,
                maxTextureSize = 2048,
                resizeAlgorithm = TextureResizeAlgorithm.Mitchell,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed,
                crunchedCompression = false,
                compressionQuality = 50,
            };
        }

        static List<TextureImporterPlatformSettings> CreateDefaultOverrides()
        {
            var list = new List<TextureImporterPlatformSettings>(OverridePlatformNames.Count);
            foreach (string name in OverridePlatformNames)
                list.Add(CreatePlatformSettings(name));
            return list;
        }

        /// <summary>Platform settings name <see cref="TextureImporter"/> uses for a build target group.</summary>
        internal static string GetPlatformName(BuildTargetGroup group)
        {
            switch (group)
            {
                case BuildTargetGroup.Standalone: return "Standalone";
                case BuildTargetGroup.Android: return "Android";
                case BuildTargetGroup.iOS: return "iPhone";
                case BuildTargetGroup.WebGL: return "WebGL";
                default: return group.ToString();
            }
        }

        /// <summary>The override for <paramref name="target"/> when it is enabled, otherwise <see cref="defaultPlatformSettings"/>.</summary>
        internal TextureImporterPlatformSettings GetEffectivePlatformSettings(BuildTarget target)
        {
            string platformName = GetPlatformName(BuildPipeline.GetBuildTargetGroup(target));
            if (platformOverrides != null)
            {
                foreach (var entry in platformOverrides)
                {
                    if (entry != null && entry.overridden && entry.name == platformName)
                        return entry;
                }
            }
            return defaultPlatformSettings ?? CreatePlatformSettings(DefaultPlatformName);
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            // Output depends on the libjxl build as well as the file and settings (registered by JxlNativeSetup).
            ctx.DependsOnCustomDependency(JxlNativeSetup.DecoderDependency);
            string textureName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            if (!JxlDecoder.IsAvailable)
            {
                AddPlaceholder(ctx, textureName, "JPEG XL import failed: the native libjxl decoder is not available. " + JxlDecoder.UnavailableReason);
                return;
            }

            TextureGenerationOutput output;
            string[] spriteIdentifiers;
            try
            {
                output = GenerateTexture(ctx, textureName, out spriteIdentifiers);
            }
            catch (JxlDecodeException e)
            {
                AddPlaceholder(ctx, textureName, $"JPEG XL decode failed for '{ctx.assetPath}': {e.Message}");
                return;
            }
            catch (Exception e)
            {
                AddPlaceholder(ctx, textureName, $"JPEG XL import failed for '{ctx.assetPath}': {e.GetType().Name}: {e.Message}");
                return;
            }

            Texture2D tex = output.texture;
            tex.name = textureName;
            ctx.AddObjectToAsset("texture", tex, output.thumbNail != null ? output.thumbNail : tex);
            ctx.SetMainObject(tex);
            AddSprites(ctx, output.sprites, spriteIdentifiers);
        }

        /// <summary>
        /// Decode options for <paramref name="info"/> under the current settings: linear float for HDR files, otherwise
        /// 8-bit, converted to sRGB when <see cref="convertToSRGB"/> is on and the file is not sRGB.
        /// <paramref name="rgba8"/> forces 8-bit sRGB output for HDR files too (Sprite Editor slicing copy).
        /// </summary>
        internal JxlDecodeOptions GetDecodeOptions(JxlImageInfo info, bool rgba8 = false)
        {
            bool floatOutput = info.IsHdr && !rgba8;
            JxlColorTarget colorTarget;
            if (floatOutput)
                colorTarget = hdrPrimaries == JxlHdrPrimaries.Rec2100 ? JxlColorTarget.LinearRec2100 : JxlColorTarget.LinearSRgb;
            else if (info.IsHdr || (convertToSRGB && (info.IsWideGamut || info.Transfer != JxlTransfer.SRgb)))
                colorTarget = JxlColorTarget.SRgb;
            else
                colorTarget = JxlColorTarget.Original;

            return new JxlDecodeOptions
            {
                Output = floatOutput ? JxlPixelOutput.RgbaFloat : JxlPixelOutput.Rgba8,
                ColorTarget = colorTarget,
                // TextureGenerator expects Unity row order: element 0 is the bottom-left pixel and rows go
                // upwards. Verified by JxlImporterTests.Sun_Uncompressed_PixelsMatchReference (pixels compared
                // against djxl's PNG with unity_y = height - 1 - png_y).
                FlipVertically = true,
                UnpremultiplyAlpha = unpremultiplyAlpha,
                // A desired intensity target makes libjxl tone-map instead of rescale; HDR scaling is done in ScaleHdr.
                DesiredIntensityTarget = 0f
            };
        }

        /// <summary>Reads the header of the file at <paramref name="path"/>; false with a message when that is not possible.</summary>
        internal static bool TryReadInfo(string path, out JxlImageInfo info, out string error)
        {
            info = null;
            error = null;
            if (!JxlDecoder.IsAvailable)
            {
                error = "Decoder not available: " + JxlDecoder.UnavailableReason;
                return false;
            }
            try
            {
                info = JxlDecoder.ReadInfo(File.ReadAllBytes(path));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Decodes the source and runs it through <see cref="TextureGenerator"/>. Throws on any failure.</summary>
        TextureGenerationOutput GenerateTexture(AssetImportContext ctx, string textureName, out string[] spriteIdentifiers)
        {
            byte[] data = File.ReadAllBytes(ctx.assetPath);
            JxlImageInfo info = JxlDecoder.ReadInfo(data);
            bool isHdr = info.IsHdr;
            JxlDecodedImage decoded = JxlDecoder.Decode(data, GetDecodeOptions(info));
            info = decoded.Info;
            int width = info.Width;
            int height = info.Height;

            var importSettings = new TextureImporterSettings();
            (textureSettings ?? CreateDefaultTextureSettings()).CopyTo(importSettings);
            // Only 2D textures are produced. Sprite modes: Single and Multiple (Polygon imports as Single).
            importSettings.textureShape = TextureImporterShape.Texture2D;
            bool isSprite = importSettings.textureType == TextureImporterType.Sprite;
            bool multipleSprites = isSprite && importSettings.spriteMode == (int)SpriteImportMode.Multiple;
            if (isSprite && !multipleSprites)
                importSettings.spriteMode = (int)SpriteImportMode.Single;
            if (isHdr)
                importSettings.sRGBTexture = false;

            var settings = new TextureGenerationSettings(importSettings.textureType)
            {
                assetPath = ctx.assetPath,
                enablePostProcessor = true,
                textureImporterSettings = importSettings,
                platformSettings = GetEffectivePlatformSettings(ctx.selectedBuildTarget),
                sourceTextureInformation = new SourceTextureInformation
                {
                    width = width,
                    height = height,
                    containsAlpha = info.HasAlpha,
                    hdr = isHdr
                },
            };

            spriteIdentifiers = null;
            if (isSprite)
            {
                if (multipleSprites)
                {
                    settings.spriteImportData = BuildSheetSprites(ctx, width, height, importSettings.spriteTessellationDetail, out spriteIdentifiers);
                }
                else
                {
                    settings.spriteImportData = new[]
                    {
                        new SpriteImportData
                        {
                            name = textureName,
                            rect = new Rect(0, 0, width, height),
                            alignment = (SpriteAlignment)importSettings.spriteAlignment,
                            pivot = importSettings.spritePivot,
                            border = importSettings.spriteBorder,
                            tessellationDetail = importSettings.spriteTessellationDetail,
                            spriteID = StableSpriteId(ctx.assetPath)
                        }
                    };
                    // Single mode uses the texture name as the sprite's sub-asset identifier, so its file ID never changes.
                    spriteIdentifiers = new[] { textureName };
                }
                settings.qualifyForSpritePacking = true;
                settings.spritePackingTag = string.Empty;
            }

            // Persistent rather than Temp: a 4K float image is 130 MB, far beyond the temp allocator's blocks.
            // CopyFrom throws if the decoder's buffer does not match width * height pixels.
            TextureGenerationOutput output;
            int pixelCount = width * height;
            if (isHdr)
            {
                using (var pixels = new NativeArray<Color>(pixelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory))
                {
                    pixels.Reinterpret<byte>(16).CopyFrom(decoded.Pixels);
                    decoded = null;
                    ScaleHdr(pixels, info);
                    output = TextureGenerator.GenerateTexture(settings, pixels);
                }
            }
            else
            {
                using (var pixels = new NativeArray<Color32>(pixelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory))
                {
                    pixels.Reinterpret<byte>(4).CopyFrom(decoded.Pixels);
                    decoded = null;
                    output = TextureGenerator.GenerateTexture(settings, pixels);
                }
            }

            if (output.importWarnings != null)
            {
                foreach (string warning in output.importWarnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                        ctx.LogImportWarning($"JPEG XL import: {warning.Trim()}");
                }
            }
            if (!string.IsNullOrWhiteSpace(output.importInspectorWarnings))
                ctx.LogImportWarning($"JPEG XL import: {output.importInspectorWarnings.Trim()}");

            if (output.texture == null)
                throw new InvalidOperationException("TextureGenerator did not produce a texture.");
            return output;
        }

        /// <summary>
        /// Adds the generated sprites, which come back in spriteImportData order, under <paramref name="identifiers"/>
        /// (falls back to the sprite name if the counts differ). Duplicate identifiers get a numeric suffix.
        /// </summary>
        static void AddSprites(AssetImportContext ctx, Sprite[] sprites, string[] identifiers)
        {
            if (sprites == null)
                return;
            bool useIdentifiers = identifiers != null && identifiers.Length == sprites.Length;
            var used = new HashSet<string>();
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null)
                    continue;
                string identifier = useIdentifiers ? identifiers[i] : sprite.name;
                string unique = identifier;
                for (int n = 1; !used.Add(unique); n++)
                    unique = identifier + "_" + n;
                ctx.AddObjectToAsset(unique, sprite);
            }
        }

        /// <summary>
        /// A sprite ID that stays stable across reimports, hashed from the asset's own GUID (or its path when it has
        /// none yet) and <paramref name="suffix"/>. Reading the GUID of the asset being imported is deterministic.
        /// </summary>
        internal static string StableSpriteId(string assetPath, string suffix = "/sprite")
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string seed = (string.IsNullOrEmpty(guid) ? assetPath : guid) + suffix;
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return new GUID(BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()).ToString();
            }
        }

        /// <summary>A valid sprite ID for <paramref name="entry"/>: its own when it parses as a GUID, otherwise one derived from the asset and index.</summary>
        internal static string ResolveSpriteId(JxlSpriteRect entry, string assetPath, int index)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.spriteID) && GUID.TryParse(entry.spriteID, out GUID parsed) && !parsed.Empty())
                return parsed.ToString();
            return StableSpriteId(assetPath, "/sheet/" + index + "/" + (entry?.name ?? string.Empty));
        }

        /// <summary>
        /// Multiple mode: one <see cref="SpriteImportData"/> per <see cref="spriteSheet"/> entry, in source pixel space (origin
        /// bottom-left). Rects are clipped to the image; empty ones are skipped with a warning, as TextureImporter does.
        /// </summary>
        SpriteImportData[] BuildSheetSprites(AssetImportContext ctx, int width, int height, float tessellationDetail, out string[] identifiers)
        {
            var result = new List<SpriteImportData>();
            var ids = new List<string>();
            var bounds = new Rect(0, 0, width, height);
            if (spriteSheet != null)
            {
                for (int i = 0; i < spriteSheet.Count; i++)
                {
                    JxlSpriteRect entry = spriteSheet[i];
                    if (entry == null)
                        continue;
                    string spriteName = string.IsNullOrEmpty(entry.name) ? Path.GetFileNameWithoutExtension(ctx.assetPath) + "_" + i : entry.name;
                    Rect r = entry.rect;
                    float xMin = Mathf.Max(r.xMin, bounds.xMin), yMin = Mathf.Max(r.yMin, bounds.yMin);
                    float xMax = Mathf.Min(r.xMax, bounds.xMax), yMax = Mathf.Min(r.yMax, bounds.yMax);
                    if (xMax - xMin < 1f || yMax - yMin < 1f)
                    {
                        ctx.LogImportWarning($"JPEG XL import: sprite '{spriteName}' {r} lies outside the {width}x{height} image and was skipped.");
                        continue;
                    }
                    var clipped = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                    if (clipped != r)
                        ctx.LogImportWarning($"JPEG XL import: sprite '{spriteName}' {r} was clipped to the {width}x{height} image.");

                    string id = ResolveSpriteId(entry, ctx.assetPath, i);
                    result.Add(new SpriteImportData
                    {
                        name = spriteName,
                        rect = clipped,
                        alignment = entry.alignment,
                        pivot = entry.pivot,
                        border = entry.border,
                        tessellationDetail = tessellationDetail,
                        spriteID = id
                    });
                    ids.Add(id);
                }
            }
            identifiers = ids.ToArray();
            return result.ToArray();
        }

        /// <summary>
        /// libjxl's linear output puts the file's intensity target (its nominal peak) at 1.0: PQ gives nits / intensity_target,
        /// HLG applies the OOTF and normalises the peak to 1.0 (measured, see Docs/JXL-Importer-Plan.md). Rescale so that
        /// <see cref="hdrReferenceWhiteNits"/> lands at 1.0, and optionally clamp out-of-gamut negatives. Linear float
        /// sources are left as authored.
        /// </summary>
        void ScaleHdr(NativeArray<Color> pixels, JxlImageInfo info)
        {
            bool scenePeakEncoded = info.Transfer == JxlTransfer.PQ || info.Transfer == JxlTransfer.HLG;
            float scale = scenePeakEncoded && hdrReferenceWhiteNits > 0f && info.IntensityTarget > 0f
                ? info.IntensityTarget / hdrReferenceWhiteNits
                : 1f;
            if (Mathf.Approximately(scale, 1f) && !hdrClampNegative)
                return;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                c.r *= scale; c.g *= scale; c.b *= scale;
                if (hdrClampNegative)
                {
                    if (c.r < 0f) c.r = 0f;
                    if (c.g < 0f) c.g = 0f;
                    if (c.b < 0f) c.b = 0f;
                }
                pixels[i] = c;
            }
        }

        /// <summary>Logs <paramref name="error"/> and imports a 4x4 magenta texture in place of the image.</summary>
        static void AddPlaceholder(AssetImportContext ctx, string textureName, string error)
        {
            ctx.LogImportError(error);
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = textureName };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 0, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            ctx.AddObjectToAsset("texture", tex, tex);
            ctx.SetMainObject(tex);
        }
    }
}
