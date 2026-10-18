using System;
using System.IO;
using JpegXL.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JpegXL.Tests
{
    public class JxlImporterTests
    {
        const string SdrDir = "Assets/JPEGXL/Tests/Samples/SDR/";
        const string HdrDir = "Assets/JPEGXL/Tests/Samples/HDR/";

        const string Merry = SdrDir + "merry_jxl_mas.jxl";
        const string HardwareAlpha = SdrDir + "hardware_alpha_4k.jxl";
        const string Sun = SdrDir + "sun_in_the_sky_lossy.jxl";
        const string Bt2111Pq = HdrDir + "bt2111_test_pattern_pq.jxl";
        const string HdrTestPq = HdrDir + "hdr_test_image_pq.jxl";
        const string HeatHlg = HdrDir + "heat_hlg.jxl";

        static readonly object[] AllFixtures =
        {
            new object[] { Merry, 1920, 1080 },
            new object[] { HardwareAlpha, 3840, 2160 },
            new object[] { Sun, 512, 512 },
            new object[] { Bt2111Pq, 1920, 1080 },
            new object[] { HdrTestPq, 1024, 512 },
            new object[] { HeatHlg, 1024, 1024 },
        };

        // NPOT textures with mipmaps cannot be block compressed by EditorUtility.CompressTexture (phase 2 limitation),
        // so the 1920x1080 and 3840x2160 fixtures stay uncompressed for now.
        static readonly object[] SdrFormats =
        {
            new object[] { Merry, TextureFormat.RGBA32 },
            new object[] { HardwareAlpha, TextureFormat.RGBA32 },
            new object[] { Sun, TextureFormat.DXT1 },
        };

        static readonly object[] HdrFormats =
        {
            new object[] { Bt2111Pq, TextureFormat.RGBAHalf },
            new object[] { HdrTestPq, TextureFormat.BC6H },
            new object[] { HeatHlg, TextureFormat.BC6H },
        };

        static readonly object[] HdrTransfers =
        {
            new object[] { Bt2111Pq, JxlTransfer.PQ },
            new object[] { HdrTestPq, JxlTransfer.PQ },
            new object[] { HeatHlg, JxlTransfer.HLG },
        };

        // ---------------------------------------------------------------- a. native library

        [Test]
        public void Decoder_IsAvailable_WithExpectedVersion()
        {
            Assert.IsTrue(JxlDecoder.IsAvailable, "libjxl native decoder failed to load.");
            Assert.IsNotNull(JxlDecoder.NativeVersion);
            StringAssert.StartsWith("0.12", JxlDecoder.NativeVersion);
        }

        // ---------------------------------------------------------------- b. every fixture imports

        [TestCaseSource(nameof(AllFixtures))]
        public void Fixture_ImportsAsTexture_WithExpectedSize(string path, int width, int height)
        {
            var importer = AssetImporter.GetAtPath(path);
            Assert.IsNotNull(importer, $"No importer for {path}");
            Assert.IsInstanceOf<JxlImporter>(importer);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, $"No Texture2D at {path}");
            Assert.AreEqual(width, tex.width, "width");
            Assert.AreEqual(height, tex.height, "height");
        }

        // ---------------------------------------------------------------- c. default formats

        [TestCaseSource(nameof(SdrFormats))]
        public void SdrFixture_DefaultSettings_IsCompressedSrgb(string path, TextureFormat expected)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, $"No Texture2D at {path}");
            Assert.AreEqual(expected, tex.format);
            Assert.IsTrue(tex.isDataSRGB, "SDR textures should be sRGB by default.");
        }

        [TestCaseSource(nameof(HdrFormats))]
        public void HdrFixture_DefaultSettings_IsBc6hLinear(string path, TextureFormat expected)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, $"No Texture2D at {path}");
            Assert.AreEqual(expected, tex.format);
            Assert.IsFalse(tex.isDataSRGB, "HDR textures must be linear.");
        }

        // ---------------------------------------------------------------- d. decoder level

        [Test]
        public void Decode_HardwareAlpha_Rgba8_HasFullAlphaRange()
        {
            byte[] data = File.ReadAllBytes(HardwareAlpha);
            var options = new JxlDecodeOptions
            {
                Output = JxlPixelOutput.Rgba8,
                ColorTarget = JxlColorTarget.Original,
                FlipVertically = true,
                UnpremultiplyAlpha = false,
                DesiredIntensityTarget = 0f
            };
            JxlDecodedImage image = JxlDecoder.Decode(data, options);

            Assert.IsNotNull(image);
            Assert.IsNotNull(image.Info);
            Assert.IsTrue(image.Info.HasAlpha, "HasAlpha");
            int w = image.Info.Width, h = image.Info.Height;
            Assert.AreEqual(3840, w);
            Assert.AreEqual(2160, h);
            Assert.AreEqual(w * h * 4, image.Pixels.Length);

            byte min = 255, max = 0;
            byte[] px = image.Pixels;
            for (int i = 3; i < px.Length; i += 4)
            {
                byte a = px[i];
                if (a < min) min = a;
                if (a > max) max = a;
            }
            Assert.AreEqual(0, min, "min alpha");
            Assert.AreEqual(255, max, "max alpha");
        }

        [Test]
        public void ReadInfo_SunInTheSky_Is10BitSdr()
        {
            JxlImageInfo info = JxlDecoder.ReadInfo(File.ReadAllBytes(Sun));
            Assert.IsNotNull(info);
            Assert.AreEqual(512, info.Width);
            Assert.AreEqual(512, info.Height);
            Assert.AreEqual(10, info.BitsPerSample);
            Assert.IsFalse(info.IsHdr);
        }

        [TestCaseSource(nameof(HdrTransfers))]
        public void Decode_HdrFixture_LinearFloat_IsFiniteAndNonBlack(string path, JxlTransfer transfer)
        {
            byte[] data = File.ReadAllBytes(path);

            JxlImageInfo info = JxlDecoder.ReadInfo(data);
            Assert.IsTrue(info.IsHdr, "IsHdr");
            Assert.AreEqual(transfer, info.Transfer);

            var options = new JxlDecodeOptions
            {
                Output = JxlPixelOutput.RgbaFloat,
                ColorTarget = JxlColorTarget.LinearSRgb,
                FlipVertically = true,
                UnpremultiplyAlpha = false,
                DesiredIntensityTarget = 0f
            };
            JxlDecodedImage image = JxlDecoder.Decode(data, options);

            Assert.IsNotNull(image);
            Assert.IsTrue(image.Info.IsHdr, "decoded Info.IsHdr");
            Assert.AreEqual(transfer, image.Info.Transfer);
            int w = image.Info.Width, h = image.Info.Height;
            Assert.AreEqual(w * h * 16, image.Pixels.Length);

            var floats = new float[w * h * 4];
            Buffer.BlockCopy(image.Pixels, 0, floats, 0, image.Pixels.Length);

            bool anyPositive = false;
            for (int i = 0; i < floats.Length; i++)
            {
                float v = floats[i];
                if (float.IsNaN(v) || float.IsInfinity(v))
                    Assert.Fail($"Non-finite value {v} at float index {i} in {path}");
                if ((i & 3) != 3 && v > 0f)
                    anyPositive = true;
            }
            Assert.IsTrue(anyPositive, "Expected at least one RGB value > 0.");
        }

        // ---------------------------------------------------------------- f. non-readable textures keep their pixels

        /// <summary>
        /// The importer calls Apply(false, makeNoLongerReadable: true) when Read/Write is off. This proves the
        /// serialized asset still carries pixel data by sampling the GPU copy through a RenderTexture.
        /// </summary>
        [TestCaseSource(nameof(AllFixtures))]
        public void Fixture_NonReadableTexture_HasPixelsOnGpu(string path, int width, int height)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, $"No Texture2D at {path}");
            Assert.IsFalse(tex.isReadable, "Fixture should be non-readable with default settings.");

            var rt = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var readback = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();

                Color[] px = readback.GetPixels();
                float sum = 0f;
                bool allMagenta = true;
                for (int i = 0; i < px.Length; i++)
                {
                    sum += px[i].r + px[i].g + px[i].b;
                    if (!(px[i].r > 0.9f && px[i].g < 0.1f && px[i].b > 0.9f))
                        allMagenta = false;
                }
                Assert.Greater(sum, 0f, "Texture sampled as all black: pixel data was lost after import.");
                Assert.IsFalse(allMagenta, "Texture is the magenta placeholder: import failed.");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        // ---------------------------------------------------------------- e. settings round trip

        [Test]
        public void Settings_UncompressedReadable_RoundTrip()
        {
            var importer = AssetImporter.GetAtPath(Sun) as JxlImporter;
            Assert.IsNotNull(importer, "Importer is not a JxlImporter.");

            try
            {
                importer.compression = JxlTextureCompression.None;
                importer.isReadable = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                Assert.AreEqual(TextureFormat.RGBA32, tex.format);
                Assert.IsTrue(tex.isReadable, "isReadable");

                Color c = tex.GetPixel(256, 256);
                Assert.IsTrue(c.r > 0f || c.g > 0f || c.b > 0f, $"Centre pixel is pure black: {c}");
            }
            finally
            {
                importer = AssetImporter.GetAtPath(Sun) as JxlImporter;
                if (importer != null)
                {
                    // Restore the defaults so other tests see the standard import.
                    importer.compression = JxlTextureCompression.Normal;
                    importer.isReadable = false;
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
