using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>Where the texture's alpha channel comes from.</summary>
    public enum JxlAlphaSource
    {
        /// <summary>Use the alpha channel stored in the JPEG XL file (opaque when the file has none).</summary>
        FromInput,
        /// <summary>Ignore any stored alpha; the texture is fully opaque.</summary>
        None
    }

    /// <summary>GPU compression applied to the imported texture.</summary>
    public enum JxlTextureCompression
    {
        /// <summary>Keep the decoded pixels uncompressed (RGBA32, RGBAHalf or RGBAFloat).</summary>
        None,
        /// <summary>DXT1/DXT5 for SDR, BC6H (normal quality) for HDR.</summary>
        Normal,
        /// <summary>BC7 for SDR, BC6H (best quality) for HDR.</summary>
        HighQuality
    }

    /// <summary>Uncompressed texture format used for HDR sources.</summary>
    public enum JxlHdrPrecision
    {
        /// <summary>16-bit float per channel (RGBAHalf).</summary>
        Half,
        /// <summary>32-bit float per channel (RGBAFloat).</summary>
        Float
    }

    /// <summary>
    /// Imports <c>.jxl</c> files as <see cref="Texture2D"/> assets using the bundled libjxl decoder.
    /// SDR files become RGBA32 textures (optionally DXT/BC7 compressed); HDR files (PQ, HLG or float samples)
    /// become linear RGBAHalf/RGBAFloat textures (optionally BC6H compressed).
    /// </summary>
    [ScriptedImporter(1, "jxl")]
    public sealed class JxlImporter : ScriptedImporter
    {
        // ---- Texture ----

        [Tooltip("Treat 8-bit output as sRGB-encoded color data. Turn off for data textures such as masks or normal maps. Ignored for HDR files, which are always linear.")]
        public bool sRGBTexture = true;

        [Tooltip("Generate a mipmap chain for the texture.")]
        public bool generateMipMaps = true;

        [Tooltip("Texture filtering mode.")]
        public FilterMode filterMode = FilterMode.Bilinear;

        [Tooltip("Texture wrap mode.")]
        public TextureWrapMode wrapMode = TextureWrapMode.Repeat;

        [Tooltip("Anisotropic filtering level (1 = off).")]
        [Range(1, 16)]
        public int anisoLevel = 1;

        // ---- Alpha ----

        [Tooltip("Where the alpha channel comes from. None forces the texture to be fully opaque.")]
        public JxlAlphaSource alphaSource = JxlAlphaSource.FromInput;

        [Tooltip("Mark the texture's alpha as transparency (dilates color into transparent areas for mipmaps and previews).")]
        public bool alphaIsTransparency = true;

        [Tooltip("Ask the decoder to undo premultiplied alpha when the file stores premultiplied color.")]
        public bool unpremultiplyAlpha = true;

        // ---- Color ----

        [Tooltip("For SDR files whose primaries or transfer function are not sRGB (for example Rec.2100 or P3), convert the pixels to sRGB during decoding. When off the original encoding is kept.")]
        public bool convertToSRGB = true;

        // ---- HDR ----

        [Tooltip("Uncompressed texture format used for HDR sources before compression: Half = RGBAHalf, Float = RGBAFloat.")]
        public JxlHdrPrecision hdrPrecision = JxlHdrPrecision.Half;

        // ---- Compression ----

        [Tooltip("GPU compression. None keeps the decoded pixels. Normal: DXT1 (opaque) / DXT5 (alpha) for SDR, BC6H for HDR. High Quality: BC7 for SDR, BC6H at best quality for HDR. Compression needs width and height to be multiples of 4.")]
        public JxlTextureCompression compression = JxlTextureCompression.Normal;

        // ---- Advanced ----

        [Tooltip("Keep a CPU copy of the pixels so scripts can read them (GetPixel, GetPixels). Doubles memory use.")]
        public bool isReadable = false;

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

            Texture2D tex = null;
            try
            {
                byte[] data = File.ReadAllBytes(ctx.assetPath);
                JxlImageInfo info = JxlDecoder.ReadInfo(data);
                bool isHdr = info.IsHdr;

                JxlColorTarget colorTarget;
                if (isHdr)
                    colorTarget = JxlColorTarget.LinearSRgb;
                else if (convertToSRGB && (info.IsWideGamut || info.Transfer != JxlTransfer.SRgb))
                    colorTarget = JxlColorTarget.SRgb;
                else
                    colorTarget = JxlColorTarget.Original;

                var options = new JxlDecodeOptions
                {
                    Output = isHdr ? JxlPixelOutput.RgbaFloat : JxlPixelOutput.Rgba8,
                    ColorTarget = colorTarget,
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

                bool keepAlpha = alphaSource == JxlAlphaSource.FromInput;
                bool textureHasAlpha = keepAlpha && decodedInfo.HasAlpha;

                tex = isHdr
                    ? CreateHdrTexture(decoded.Pixels, width, height, keepAlpha)
                    : CreateSdrTexture(decoded.Pixels, width, height, keepAlpha);

                tex.Apply(generateMipMaps, false);

                if (compression != JxlTextureCompression.None)
                {
                    // EditorUtility.CompressTexture refuses non-multiple-of-4 sizes, and refuses NPOT sizes when the
                    // texture has mipmaps ("Only POT textures can be compressed to this format if the texture has a mipmap").
                    bool multipleOf4 = width % 4 == 0 && height % 4 == 0;
                    bool potIfMips = !generateMipMaps || (Mathf.IsPowerOfTwo(width) && Mathf.IsPowerOfTwo(height));
                    if (!multipleOf4 || !potIfMips)
                    {
                        ctx.LogImportWarning($"JPEG XL import: texture is {width}x{height}; block compression needs multiples of 4 and, with mipmaps, power-of-two sizes, so the texture was left uncompressed.");
                    }
                    else
                    {
                        TextureFormat target;
                        if (isHdr)
                            target = TextureFormat.BC6H;
                        else if (compression == JxlTextureCompression.HighQuality)
                            target = TextureFormat.BC7;
                        else
                            target = textureHasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1;

                        TextureCompressionQuality quality = compression == JxlTextureCompression.HighQuality
                            ? TextureCompressionQuality.Best
                            : TextureCompressionQuality.Normal;

                        EditorUtility.CompressTexture(tex, target, quality);
                    }
                }

                if (!isReadable)
                    tex.Apply(false, true);

                tex.filterMode = filterMode;
                tex.wrapMode = wrapMode;
                tex.anisoLevel = Mathf.Clamp(anisoLevel, 1, 16);
                tex.alphaIsTransparency = alphaIsTransparency;
                tex.name = textureName;

                ctx.AddObjectToAsset("texture", tex, tex);
                ctx.SetMainObject(tex);
            }
            catch (JxlDecodeException e)
            {
                DestroyPartial(tex);
                ctx.LogImportError($"JPEG XL decode failed for '{ctx.assetPath}': {e.Message}");
                AddPlaceholder(ctx, textureName);
            }
            catch (Exception e)
            {
                DestroyPartial(tex);
                ctx.LogImportError($"JPEG XL import failed for '{ctx.assetPath}': {e.GetType().Name}: {e.Message}");
                AddPlaceholder(ctx, textureName);
            }
        }

        Texture2D CreateSdrTexture(byte[] pixels, int width, int height, bool keepAlpha)
        {
            int expected = width * height * 4;
            if (pixels == null || pixels.Length != expected)
                throw new InvalidOperationException($"Decoder returned {(pixels == null ? 0 : pixels.Length)} bytes, expected {expected} for {width}x{height} RGBA8.");

            if (!keepAlpha)
            {
                for (int i = 3; i < pixels.Length; i += 4)
                    pixels[i] = 255;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, generateMipMaps, !sRGBTexture);
            tex.SetPixelData(pixels, 0);
            return tex;
        }

        Texture2D CreateHdrTexture(byte[] pixels, int width, int height, bool keepAlpha)
        {
            int floatCount = width * height * 4;
            int expected = floatCount * sizeof(float);
            if (pixels == null || pixels.Length != expected)
                throw new InvalidOperationException($"Decoder returned {(pixels == null ? 0 : pixels.Length)} bytes, expected {expected} for {width}x{height} RGBA float.");

            var floats = new float[floatCount];
            Buffer.BlockCopy(pixels, 0, floats, 0, expected);

            if (!keepAlpha)
            {
                for (int i = 3; i < floats.Length; i += 4)
                    floats[i] = 1f;
            }

            if (hdrPrecision == JxlHdrPrecision.Float)
            {
                var texFloat = new Texture2D(width, height, TextureFormat.RGBAFloat, generateMipMaps, true);
                texFloat.SetPixelData(floats, 0);
                return texFloat;
            }

            var halves = new ushort[floatCount];
            for (int i = 0; i < floatCount; i++)
                halves[i] = Mathf.FloatToHalf(floats[i]);

            var texHalf = new Texture2D(width, height, TextureFormat.RGBAHalf, generateMipMaps, true);
            texHalf.SetPixelData(halves, 0);
            return texHalf;
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

        static void DestroyPartial(Texture2D tex)
        {
            if (tex != null)
                UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
