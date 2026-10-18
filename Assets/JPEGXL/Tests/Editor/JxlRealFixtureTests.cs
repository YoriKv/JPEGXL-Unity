using System;
using System.Diagnostics;
using System.IO;
using JpegXL.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static JpegXL.Tests.JxlImporterTests;

namespace JpegXL.Tests
{
    /// <summary>
    /// Phase 4: pixel-level checks on the generated and photographic fixtures in Samples/Real (formulas in SOURCES.md),
    /// importer/decoder consistency for HDR, a decode-everything parity check, and the crunch / single-channel paths.
    /// Coordinates named png (x, y) count rows from the top; Unity textures are bottom-up (unity_y = height - 1 - png_y).
    /// </summary>
    public class JxlRealFixtureTests
    {
        const string AlphaGradient = RealDir + "alpha_gradient.jxl";
        const string Gray16 = RealDir + "gray_16bit.jxl";
        const string Animated = RealDir + "animated_4frames.jxl";
        const string Odd = RealDir + "odd_123x77_lossy.jxl";
        const string PhotoLossy = RealDir + "photo_lossy.jxl";
        const string PhotoJpeg = RealDir + "photo_jpeg_reconstruct.jxl";

        static Color32 PngPixel(Texture2D tex, Color32[] pixels, int pngX, int pngY) =>
            pixels[(tex.height - 1 - pngY) * tex.width + pngX];

        static void AssertChannel(int expected, int actual, int tolerance, string what) =>
            Assert.LessOrEqual(Math.Abs(expected - actual), tolerance, $"{what}: expected {expected}, got {actual}");

        static bool IsPlaceholder(Texture2D tex) => tex.width == 4 && tex.height == 4;

        // ---------------------------------------------------------------- a. alpha gradient

        [Test]
        public void AlphaGradient_Default_IsDxt5_256()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlphaGradient);
            Assert.IsNotNull(tex);
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
            Assert.AreEqual(TextureFormat.DXT5, tex.format);
        }

        [Test]
        public void AlphaGradient_Uncompressed_MatchesFormula()
        {
            try
            {
                Reimport(AlphaGradient, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlphaGradient);
                Assert.AreEqual(256, tex.width);
                Assert.AreEqual(TextureFormat.RGBA32, tex.format);
                Color32[] px = tex.GetPixels32();

                // R = 255 - pngY, G = x, B = 128, A = x, except A = 0 in the 64x64 hole at png (96..159, 96..159).
                int[][] points = { new[] { 10, 10 }, new[] { 200, 50 }, new[] { 50, 200 }, new[] { 255, 255 }, new[] { 0, 0 }, new[] { 160, 160 }, new[] { 95, 120 } };
                foreach (int[] p in points)
                {
                    Color32 c = PngPixel(tex, px, p[0], p[1]);
                    string at = $"png ({p[0]},{p[1]}) got {c}";
                    AssertChannel(255 - p[1], c.r, 2, "R at " + at);
                    AssertChannel(p[0], c.g, 2, "G at " + at);
                    AssertChannel(128, c.b, 2, "B at " + at);
                    AssertChannel(p[0], c.a, 2, "A at " + at);
                }

                int[][] hole = { new[] { 96, 96 }, new[] { 128, 128 }, new[] { 159, 159 }, new[] { 100, 150 } };
                foreach (int[] p in hole)
                {
                    Color32 c = PngPixel(tex, px, p[0], p[1]);
                    AssertChannel(0, c.a, 2, $"A inside hole at png ({p[0]},{p[1]}) got {c}");
                }
            }
            finally
            {
                RestoreDefaults(AlphaGradient);
            }
        }

        // ---------------------------------------------------------------- b. 16-bit grey

        [Test]
        public void Gray16_ReadInfo_Is16BitSingleChannel()
        {
            JxlImageInfo info = JxlDecoder.ReadInfo(File.ReadAllBytes(Gray16));
            Assert.AreEqual(512, info.Width);
            Assert.AreEqual(512, info.Height);
            Assert.AreEqual(16, info.BitsPerSample, "BitsPerSample");
            Assert.AreEqual(1, info.NumColorChannels, "NumColorChannels");
            Assert.IsFalse(info.HasAlpha);
        }

        [Test]
        public void Gray16_Uncompressed_IsGreyAndMatchesFormula()
        {
            try
            {
                Reimport(Gray16, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Gray16);
                Assert.AreEqual(512, tex.width);
                Assert.AreEqual(512, tex.height);
                Color32[] px = tex.GetPixels32();

                int[][] points = { new[] { 100, 10 }, new[] { 255, 63 }, new[] { 300, 200 }, new[] { 10, 500 }, new[] { 511, 511 }, new[] { 0, 0 }, new[] { 450, 33 } };
                foreach (int[] p in points)
                {
                    Color32 c = PngPixel(tex, px, p[0], p[1]);
                    int expected = ((p[0] * 128 + (p[1] % 64) * 7) & 0xFFFF) >> 8;
                    string at = $"png ({p[0]},{p[1]}) got {c} (format {tex.format})";
                    Assert.LessOrEqual(Math.Abs(c.r - c.g), 1, "r == g at " + at);
                    Assert.LessOrEqual(Math.Abs(c.r - c.b), 1, "r == b at " + at);
                    AssertChannel(expected, c.r, 2, "value at " + at);
                }
            }
            finally
            {
                RestoreDefaults(Gray16);
            }
        }

        // ---------------------------------------------------------------- c. animation: first frame

        [Test]
        public void Animated_ImportsFirstFrame()
        {
            Assert.IsTrue(JxlDecoder.ReadInfo(File.ReadAllBytes(Animated)).HasAnimation, "HasAnimation");
            try
            {
                Reimport(Animated, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Animated);
                Assert.AreEqual(128, tex.width);
                Assert.AreEqual(128, tex.height);
                Color32[] px = tex.GetPixels32();

                Color32 square = PngPixel(tex, px, 25, 65);
                AssertChannel(255, square.r, 3, $"R of the yellow square (frame 0) at png (25,65), got {square}");
                AssertChannel(255, square.g, 3, $"G of the yellow square (frame 0) at png (25,65), got {square}");
                AssertChannel(0, square.b, 3, $"B of the yellow square (frame 0) at png (25,65), got {square}");

                // Frame 0 background is (0, 40, 200); later frames use other backgrounds.
                Color32 background = PngPixel(tex, px, 5, 5);
                AssertChannel(0, background.r, 3, $"background R, got {background}");
                AssertChannel(40, background.g, 3, $"background G, got {background}");
                AssertChannel(200, background.b, 3, $"background B, got {background}");
            }
            finally
            {
                RestoreDefaults(Animated);
            }
        }

        // ---------------------------------------------------------------- d. odd size, lossy

        [Test]
        public void Odd123x77_DefaultImport_IsNotPlaceholder()
        {
            try
            {
                // Default settings plus Read/Write so the pixels can be sampled.
                Reimport(Odd, importer => importer.textureSettings.readable = true);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Odd);
                Assert.IsNotNull(tex);
                Assert.IsFalse(IsPlaceholder(tex), "4x4 texture: the import failed and produced the placeholder.");
                // ToNearest: 123 -> 128, 77 -> 64.
                Assert.AreEqual(128, tex.width, $"width (format {tex.format})");
                Assert.AreEqual(64, tex.height, $"height (format {tex.format})");
                AssertColoursVary(tex);
                TestContext.Out.WriteLine($"odd_123x77 default: {tex.width}x{tex.height} {tex.format}");
            }
            finally
            {
                RestoreDefaults(Odd);
            }
        }

        [Test]
        public void Odd123x77_NpotNone_KeepsSourceSize()
        {
            try
            {
                Reimport(Odd, importer =>
                {
                    importer.textureSettings.readable = true;
                    importer.textureSettings.npotScale = TextureImporterNPOTScale.None;
                });
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Odd);
                Assert.IsNotNull(tex);
                Assert.AreEqual(123, tex.width, $"width (Unity chose format {tex.format})");
                Assert.AreEqual(77, tex.height, $"height (Unity chose format {tex.format})");
                AssertColoursVary(tex);
                TestContext.Out.WriteLine($"odd_123x77 NPOT None: {tex.width}x{tex.height} {tex.format}");
            }
            finally
            {
                RestoreDefaults(Odd);
            }
        }

        static void AssertColoursVary(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            Color32 first = px[0];
            int differing = 0;
            bool allMagenta = true;
            foreach (Color32 c in px)
            {
                if (Math.Abs(c.r - first.r) + Math.Abs(c.g - first.g) + Math.Abs(c.b - first.b) > 30)
                    differing++;
                if (!(c.r > 240 && c.g < 15 && c.b > 240))
                    allMagenta = false;
            }
            Assert.IsFalse(allMagenta, $"Texture is magenta (format {tex.format}).");
            Assert.Greater(differing, px.Length / 10, $"Expected varied photo content, {differing}/{px.Length} pixels differ from the first (format {tex.format}).");
        }

        // ---------------------------------------------------------------- e. photographs

        // 2048x1365 -> ToNearest 2048x1024; 1024x683 -> 1024x512 (683 is nearer to 512 than to 1024).
        [TestCase(PhotoLossy, 2048, 1365, 2048, 1024)]
        [TestCase(PhotoJpeg, 1024, 683, 1024, 512)]
        public void Photo_Default_IsDxt1_AtNearestPowerOfTwo(string path, int srcW, int srcH, int width, int height)
        {
            byte[] data = File.ReadAllBytes(path);
            var watch = Stopwatch.StartNew();
            JxlDecodedImage decoded = JxlDecoder.Decode(data, JxlDecodeOptions.Default);
            watch.Stop();
            Assert.AreEqual(srcW, decoded.Info.Width, "source width");
            Assert.AreEqual(srcH, decoded.Info.Height, "source height");
            TestContext.Out.WriteLine($"{Path.GetFileName(path)}: {data.Length} bytes, {srcW}x{srcH}, RGBA8 decode {watch.Elapsed.TotalMilliseconds:F1} ms (multithreaded: {JxlDecoder.IsMultithreaded})");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex);
            Assert.AreEqual(width, tex.width, "width");
            Assert.AreEqual(height, tex.height, "height");
            Assert.AreEqual(TextureFormat.DXT1, tex.format);
            Assert.IsTrue(tex.isDataSRGB);
        }

        // ---------------------------------------------------------------- f. HDR importer vs decoder

        // png (x, y) landmarks in solid areas of the BT.2111 pattern: 40% grey, 75% white/yellow/cyan/magenta bars and the 100% white step.
        static readonly int[][] Bt2111Landmarks =
        {
            new[] { 60, 400 }, new[] { 300, 400 }, new[] { 540, 400 }, new[] { 780, 400 }, new[] { 1260, 400 }, new[] { 1500, 700 },
        };

        [Test]
        public void Bt2111_ImportedTexels_MatchDecoderScaledByReferenceWhite()
        {
            byte[] data = File.ReadAllBytes(Bt2111Pq);
            JxlImageInfo info = JxlDecoder.ReadInfo(data);
            Assert.AreEqual(10000f, info.IntensityTarget, 1f, "PQ intensity target");
            var decoded = JxlDecoder.Decode(data, new JxlDecodeOptions
            {
                Output = JxlPixelOutput.RgbaFloat,
                ColorTarget = JxlColorTarget.LinearSRgb,
                FlipVertically = true,
                UnpremultiplyAlpha = true,
                DesiredIntensityTarget = 0f
            });
            var floats = new float[decoded.Pixels.Length / 4];
            Buffer.BlockCopy(decoded.Pixels, 0, floats, 0, decoded.Pixels.Length);
            float scale = info.IntensityTarget / 203f;

            try
            {
                Reimport(Bt2111Pq, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Bt2111Pq);
                Assert.AreEqual(TextureFormat.RGBAHalf, tex.format);
                Assert.AreEqual(info.Width, tex.width);
                Assert.AreEqual(info.Height, tex.height);

                foreach (int[] p in Bt2111Landmarks)
                {
                    int x = p[0], unityY = info.Height - 1 - p[1];
                    Color actual = tex.GetPixel(x, unityY);
                    int i = (unityY * info.Width + x) * 4;
                    for (int ch = 0; ch < 3; ch++)
                    {
                        float expected = Mathf.Max(0f, floats[i + ch] * scale);
                        float got = actual[ch];
                        float tolerance = Mathf.Max(0.02f, Mathf.Abs(expected) * 0.02f);
                        Assert.AreEqual(expected, got, tolerance, $"channel {"RGB"[ch]} at png ({p[0]},{p[1]}): decoder {floats[i + ch]} * {scale}");
                    }
                }
            }
            finally
            {
                RestoreDefaults(Bt2111Pq);
            }
        }

        // ---------------------------------------------------------------- g. every sample decodes and imports

        static string[] AllSamples() => Directory.GetFiles(SamplesDir, "*.jxl", SearchOption.AllDirectories);

        [Test]
        public void AllSamples_DecodeAndImport()
        {
            string[] files = AllSamples();
            Assert.GreaterOrEqual(files.Length, 15, "Expected the SDR, HDR and Real fixtures.");
            foreach (string file in files)
            {
                string path = file.Replace('\\', '/');
                byte[] data = File.ReadAllBytes(path);
                JxlImageInfo info = null;
                var watch = Stopwatch.StartNew();
                Assert.DoesNotThrow(() =>
                {
                    info = JxlDecoder.ReadInfo(data);
                    var options = JxlDecodeOptions.Default;
                    if (info.IsHdr)
                    {
                        options.Output = JxlPixelOutput.RgbaFloat;
                        options.ColorTarget = JxlColorTarget.LinearSRgb;
                    }
                    JxlDecodedImage image = JxlDecoder.Decode(data, options);
                    Assert.AreEqual((long)info.Width * info.Height * 4 * (info.IsHdr ? 4 : 1), image.Pixels.LongLength, "pixel buffer size");
                }, $"decode {path}");
                watch.Stop();

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"No Texture2D at {path}");
                Assert.IsFalse(IsPlaceholder(tex) && (info.Width != 4 || info.Height != 4), $"{path} imported as the 4x4 placeholder.");
                TestContext.Out.WriteLine($"{path}: {info.Width}x{info.Height} -> {tex.width}x{tex.height} {tex.format}, decode {watch.Elapsed.TotalMilliseconds:F1} ms");
            }
        }

        // ---------------------------------------------------------------- crunch and single channel

        [Test]
        public void Sun_Crunched_IsDxt1Crunched()
        {
            try
            {
                Reimport(Sun, importer => importer.defaultPlatformSettings.crunchedCompression = true);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                Assert.AreEqual(512, tex.width);
                Assert.AreEqual(TextureFormat.DXT1Crunched, tex.format);
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        [TestCase(Sun, TextureImporterSingleChannelComponent.Red, TextureFormat.R8)]
        [TestCase(AlphaGradient, TextureImporterSingleChannelComponent.Alpha, TextureFormat.Alpha8)]
        public void SingleChannel_Uncompressed_IsOneChannelFormat(string path, TextureImporterSingleChannelComponent component, TextureFormat expected)
        {
            try
            {
                Reimport(path, importer =>
                {
                    importer.textureSettings.textureType = TextureImporterType.SingleChannel;
                    importer.textureSettings.singleChannelComponent = component;
                    importer.textureSettings.sRGBTexture = false;
                    MakeReadableUncompressed(importer);
                });
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex);
                Assert.IsFalse(IsPlaceholder(tex), "placeholder");
                Assert.AreEqual(expected, tex.format);

                if (component == TextureImporterSingleChannelComponent.Alpha)
                {
                    // alpha_gradient: A = x outside the hole.
                    Color32 c = tex.GetPixels32()[(tex.height - 1 - 10) * tex.width + 200];
                    AssertChannel(200, c.a, 2, $"alpha at png (200,10), got {c}");
                }
                else
                {
                    // Sun reference pixel png (400,100) has R = 211.
                    Color32 c = tex.GetPixels32()[(tex.height - 1 - 100) * tex.width + 400];
                    AssertChannel(211, c.r, 3, $"red at png (400,100), got {c}");
                }
            }
            finally
            {
                RestoreDefaults(path);
            }
        }

        [Test]
        public void SingleChannel_Compressed_IsBc4()
        {
            try
            {
                Reimport(Sun, importer =>
                {
                    importer.textureSettings.textureType = TextureImporterType.SingleChannel;
                    importer.textureSettings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                    importer.textureSettings.sRGBTexture = false;
                });
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                Assert.AreEqual(TextureFormat.BC4, tex.format);
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }
    }
}
