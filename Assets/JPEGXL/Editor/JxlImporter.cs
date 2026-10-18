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
    /// <summary>
    /// Imports <c>.jxl</c> files as <see cref="Texture2D"/> assets (plus a <see cref="Sprite"/> sub-asset for the Sprite type).
    /// Pixels come from the bundled libjxl decoder; the texture itself is built by Unity's own texture import pipeline
    /// (<see cref="TextureGenerator.GenerateTexture(TextureGenerationSettings, NativeArray{Color32})"/>), so texture types,
    /// NPOT scaling, mipmaps, compression formats, max size and per-platform overrides behave exactly as for a <c>.jpg</c>.
    /// SDR files go through the <see cref="Color32"/> overload; HDR files (PQ, HLG or float samples) are decoded to linear
    /// float and go through the <see cref="Color"/> overload, which yields RGBAHalf (uncompressed) or BC6H (compressed).
    /// </summary>
    /// <summary>Colour primaries for imported HDR textures.</summary>
    public enum JxlHdrPrimaries
    {
        /// <summary>sRGB / Rec.709 primaries (Unity's default linear working space).</summary>
        SRgb,
        /// <summary>Rec.2100 / Rec.2020 primaries (wide gamut).</summary>
        Rec2100
    }

    [ScriptedImporter(2, "jxl")]
    public sealed class JxlImporter : ScriptedImporter
    {
        /// <summary>Name of the platform settings entry used when no override applies (same as <see cref="TextureImporter"/>).</summary>
        public const string DefaultPlatformName = "DefaultTexturePlatform";

        /// <summary>Platforms that can carry overrides, named like <see cref="TextureImporter"/> platform settings.</summary>
        public static readonly string[] OverridePlatformNames = { "Standalone", "Android", "iPhone", "WebGL" };

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

        /// <summary>Texture settings matching what <see cref="TextureImporter"/> uses for a newly imported image of type Default.</summary>
        public static TextureImporterSettings CreateDefaultTextureSettings()
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
        public static TextureImporterPlatformSettings CreatePlatformSettings(string name)
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
            var list = new List<TextureImporterPlatformSettings>(OverridePlatformNames.Length);
            foreach (string name in OverridePlatformNames)
                list.Add(CreatePlatformSettings(name));
            return list;
        }

        /// <summary>Platform settings name <see cref="TextureImporter"/> uses for a build target group.</summary>
        public static string GetPlatformName(BuildTargetGroup group)
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
        public TextureImporterPlatformSettings GetEffectivePlatformSettings(BuildTarget target)
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
            string textureName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            if (!JxlDecoder.IsAvailable)
            {
                ctx.LogImportError("JPEG XL import failed: the native libjxl decoder is not available (it failed to load or its version does not match). " +
                                   "Check the console for the load error and Assets/JPEGXL/Plugins/Editor/x86_64.");
                AddPlaceholder(ctx, textureName);
                return;
            }

            NativeArray<Color32> sdrPixels = default;
            NativeArray<Color> hdrPixels = default;
            try
            {
                byte[] data = File.ReadAllBytes(ctx.assetPath);
                JxlImageInfo info = JxlDecoder.ReadInfo(data);
                bool isHdr = info.IsHdr;

                JxlColorTarget colorTarget;
                if (isHdr)
                    colorTarget = hdrPrimaries == JxlHdrPrimaries.Rec2100 ? JxlColorTarget.LinearRec2100 : JxlColorTarget.LinearSRgb;
                else if (convertToSRGB && (info.IsWideGamut || info.Transfer != JxlTransfer.SRgb))
                    colorTarget = JxlColorTarget.SRgb;
                else
                    colorTarget = JxlColorTarget.Original;

                var options = new JxlDecodeOptions
                {
                    Output = isHdr ? JxlPixelOutput.RgbaFloat : JxlPixelOutput.Rgba8,
                    ColorTarget = colorTarget,
                    // TextureGenerator expects Unity row order: element 0 is the bottom-left pixel and rows go
                    // upwards. Verified by JxlImporterTests.Sun_Uncompressed_PixelsMatchReference (pixels compared
                    // against djxl's PNG with unity_y = height - 1 - png_y).
                    FlipVertically = true,
                    UnpremultiplyAlpha = unpremultiplyAlpha,
                    DesiredIntensityTarget = 0f
                };

                JxlDecodedImage decoded = JxlDecoder.Decode(data, options);
                JxlImageInfo decodedInfo = decoded.Info ?? info;
                int width = decodedInfo.Width;
                int height = decodedInfo.Height;
                if (width <= 0 || height <= 0)
                    throw new InvalidOperationException($"Decoder returned invalid dimensions {width}x{height}.");

                int pixelCount = width * height;
                // Persistent rather than Temp: a 4K float image is 130 MB, far beyond the temp allocator's blocks.
                if (isHdr)
                {
                    long expected = (long)pixelCount * 16;
                    if (decoded.Pixels == null || decoded.Pixels.Length != expected)
                        throw new InvalidOperationException($"Decoder returned {(decoded.Pixels == null ? 0 : decoded.Pixels.Length)} bytes, expected {expected} for {width}x{height} RGBA float.");
                    hdrPixels = new NativeArray<Color>(pixelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    hdrPixels.Reinterpret<byte>(16).CopyFrom(decoded.Pixels);
                    ScaleHdr(hdrPixels, decodedInfo);
                }
                else
                {
                    long expected = (long)pixelCount * 4;
                    if (decoded.Pixels == null || decoded.Pixels.Length != expected)
                        throw new InvalidOperationException($"Decoder returned {(decoded.Pixels == null ? 0 : decoded.Pixels.Length)} bytes, expected {expected} for {width}x{height} RGBA8.");
                    sdrPixels = new NativeArray<Color32>(pixelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    sdrPixels.Reinterpret<byte>(4).CopyFrom(decoded.Pixels);
                }

                var importSettings = new TextureImporterSettings();
                (textureSettings ?? CreateDefaultTextureSettings()).CopyTo(importSettings);
                // Only 2D textures are produced; sprites are always a single full-image sprite.
                importSettings.textureShape = TextureImporterShape.Texture2D;
                if (importSettings.textureType == TextureImporterType.Sprite)
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
                        containsAlpha = decodedInfo.HasAlpha,
                        hdr = isHdr
                    },
                };

                if (importSettings.textureType == TextureImporterType.Sprite)
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
                    settings.qualifyForSpritePacking = true;
                    settings.spritePackingTag = string.Empty;
                }

                TextureGenerationOutput output = isHdr
                    ? TextureGenerator.GenerateTexture(settings, hdrPixels)
                    : TextureGenerator.GenerateTexture(settings, sdrPixels);

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

                Texture2D tex = output.texture;
                if (tex == null)
                    throw new InvalidOperationException("TextureGenerator did not produce a texture.");

                tex.name = textureName;
                ctx.AddObjectToAsset("texture", tex, output.thumbNail != null ? output.thumbNail : tex);
                ctx.SetMainObject(tex);

                if (output.sprites != null)
                {
                    foreach (Sprite sprite in output.sprites)
                    {
                        if (sprite != null)
                            ctx.AddObjectToAsset(sprite.name, sprite);
                    }
                }
            }
            catch (JxlDecodeException e)
            {
                ctx.LogImportError($"JPEG XL decode failed for '{ctx.assetPath}': {e.Message}");
                AddPlaceholder(ctx, textureName);
            }
            catch (Exception e)
            {
                ctx.LogImportError($"JPEG XL import failed for '{ctx.assetPath}': {e.GetType().Name}: {e.Message}");
                AddPlaceholder(ctx, textureName);
            }
            finally
            {
                if (sdrPixels.IsCreated)
                    sdrPixels.Dispose();
                if (hdrPixels.IsCreated)
                    hdrPixels.Dispose();
            }
        }

        /// <summary>Sprite IDs must stay stable across reimports; derive one from the asset GUID (falls back to the path).</summary>
        static string StableSpriteId(string assetPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string seed = (string.IsNullOrEmpty(guid) ? assetPath : guid) + "/sprite";
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return new GUID(BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()).ToString();
            }
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

        static void AddPlaceholder(AssetImportContext ctx, string textureName)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 0, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.name = textureName;
            ctx.AddObjectToAsset("texture", tex, tex);
            ctx.SetMainObject(tex);
        }
    }
}
