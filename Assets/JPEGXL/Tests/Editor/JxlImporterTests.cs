using System;
using System.IO;
using JpegXL.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace JpegXL.Tests
{
    public class JxlImporterTests
    {
        const string SdrDir = "Assets/JPEGXL/Tests/Samples/SDR/";
        const string HdrDir = "Assets/JPEGXL/Tests/Samples/HDR/";
        const string RealDir = "Assets/JPEGXL/Tests/Samples/Real/";

        const string Merry = SdrDir + "merry_jxl_mas.jxl";
        const string HardwareAlpha = SdrDir + "hardware_alpha_4k.jxl";
        const string Sun = SdrDir + "sun_in_the_sky_lossy.jxl";
        const string Bt2111Pq = HdrDir + "bt2111_test_pattern_pq.jxl";
        const string HdrTestPq = HdrDir + "hdr_test_image_pq.jxl";
        const string HeatHlg = HdrDir + "heat_hlg.jxl";

        // Imported sizes follow the TextureImporter defaults a .jpg gets: Non-Power of 2 = ToNearest, Max Size = 2048.
        // 1920x1080 -> 2048x1024; 3840x2160 -> 4096x2048 -> 2048x1024 (max size).
        static readonly object[] AllFixtures =
        {
            new object[] { Merry, 2048, 1024 },
            new object[] { HardwareAlpha, 2048, 1024 },
            new object[] { Sun, 512, 512 },
            new object[] { Bt2111Pq, 2048, 1024 },
            new object[] { HdrTestPq, 1024, 512 },
            new object[] { HeatHlg, 1024, 1024 },
        };

        static readonly object[] SourceSizes =
        {
            new object[] { Merry, 1920, 1080 },
            new object[] { HardwareAlpha, 3840, 2160 },
            new object[] { Sun, 512, 512 },
            new object[] { Bt2111Pq, 1920, 1080 },
            new object[] { HdrTestPq, 1024, 512 },
            new object[] { HeatHlg, 1024, 1024 },
        };

        // Default platform settings (Automatic format, Compressed) on the Standalone target.
        static readonly object[] SdrFormats =
        {
            new object[] { Merry, TextureFormat.DXT1 },
            new object[] { HardwareAlpha, TextureFormat.DXT5 },
            new object[] { Sun, TextureFormat.DXT1 },
        };

        static readonly object[] HdrFormats =
        {
            new object[] { Bt2111Pq, TextureFormat.BC6H },
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

        [TestCaseSource(nameof(SourceSizes))]
        public void ReadInfo_Fixture_HasSourceSize(string path, int width, int height)
        {
            JxlImageInfo info = JxlDecoder.ReadInfo(File.ReadAllBytes(path));
            Assert.AreEqual(width, info.Width, "width");
            Assert.AreEqual(height, info.Height, "height");
        }

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
        /// With Read/Write off the texture has no CPU copy. This proves the serialized asset still carries pixel
        /// data by sampling the GPU copy through a RenderTexture.
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
                importer.defaultPlatformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.textureSettings.readable = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                // Opaque source, uncompressed: TextureGenerator picks RGB24, as it does for a .jpg.
                Assert.AreEqual(TextureFormat.RGB24, tex.format);
                Assert.IsTrue(tex.isReadable, "isReadable");

                Color c = tex.GetPixel(256, 256);
                Assert.IsTrue(c.r > 0f || c.g > 0f || c.b > 0f, $"Centre pixel is pure black: {c}");
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        // ---------------------------------------------------------------- helpers

        static JxlImporter GetImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as JxlImporter;
            Assert.IsNotNull(importer, $"{path} is not imported by JxlImporter.");
            return importer;
        }

        /// <summary>
        /// Applies <paramref name="change"/> to a freshly fetched importer and reimports. The importer instance is
        /// replaced by a reimport, so callers must not keep references across calls.
        /// </summary>
        static void Reimport(string path, Action<JxlImporter> change)
        {
            JxlImporter importer = GetImporter(path);
            change(importer);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        /// <summary>Puts every setting back to the importer defaults so test order does not matter.</summary>
        static void RestoreDefaults(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is JxlImporter))
                return;
            Reimport(path, importer =>
            {
                importer.textureSettings = JxlImporter.CreateDefaultTextureSettings();
                importer.defaultPlatformSettings = JxlImporter.CreatePlatformSettings(JxlImporter.DefaultPlatformName);
                for (int i = 0; i < importer.platformOverrides.Count; i++)
                    importer.platformOverrides[i] = JxlImporter.CreatePlatformSettings(importer.platformOverrides[i].name);
                importer.convertToSRGB = true;
                importer.unpremultiplyAlpha = true;
                importer.hdrReferenceWhiteNits = 203f;
                importer.hdrPrimaries = JxlHdrPrimaries.SRgb;
                importer.hdrClampNegative = true;
            });
        }

        // ---------------------------------------------------------------- phase 3: HDR scaling and primaries

        const string PqPatches = HdrDir + "pq_patches_rec2100.jxl";   // 64x8, patches of 0,1,10,100,203,1000,4000,10000 nits
        const string HlgPatches = HdrDir + "hlg_patches_rec2100.jxl"; // 64x8, scene-linear 0,.01,.1,.25,.5,.75,1,1

        static void MakeReadableUncompressed(JxlImporter importer)
        {
            importer.textureSettings.readable = true;
            importer.textureSettings.npotScale = TextureImporterNPOTScale.None;
            importer.textureSettings.mipmapEnabled = false;
            importer.defaultPlatformSettings.textureCompression = TextureImporterCompression.Uncompressed;
        }

        static Color Patch(Texture2D tex, int patch) => tex.GetPixel(patch * 8 + 4, 3);

        [Test]
        public void PqPatches_DefaultReferenceWhite203_MapsTo1()
        {
            try
            {
                Reimport(PqPatches, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PqPatches);
                Assert.AreEqual(TextureFormat.RGBAHalf, tex.format);
                Assert.AreEqual(1f, Patch(tex, 4).r, 0.01f, "203 nits");
                Assert.AreEqual(1000f / 203f, Patch(tex, 5).r, 0.05f, "1000 nits");
                Assert.AreEqual(10000f / 203f, Patch(tex, 7).r, 0.5f, "10000 nits");
                Assert.AreEqual(0f, Patch(tex, 0).r, 0.001f, "0 nits");
                Assert.AreEqual(1f, Patch(tex, 7).a, 0.001f, "alpha");
            }
            finally
            {
                RestoreDefaults(PqPatches);
            }
        }

        [Test]
        public void PqPatches_ReferenceWhiteZero_KeepsIntensityTargetScale()
        {
            try
            {
                Reimport(PqPatches, importer => { MakeReadableUncompressed(importer); importer.hdrReferenceWhiteNits = 0f; });
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PqPatches);
                Assert.AreEqual(0.0203f, Patch(tex, 4).r, 0.001f, "203 nits");
                Assert.AreEqual(1f, Patch(tex, 7).r, 0.01f, "10000 nits");
            }
            finally
            {
                RestoreDefaults(PqPatches);
            }
        }

        [Test]
        public void HlgPatches_DefaultReferenceWhite_AppliesOotfAndScale()
        {
            try
            {
                Reimport(HlgPatches, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HlgPatches);
                // libjxl: scene 1.0 -> 1.0 (1000 nits), scene 0.5 -> 0.5^1.2 = 0.435; importer scales by 1000/203.
                Assert.AreEqual(1000f / 203f, Patch(tex, 6).r, 0.05f, "scene 1.0");
                Assert.AreEqual(0.4353f * 1000f / 203f, Patch(tex, 4).r, 0.05f, "scene 0.5");
            }
            finally
            {
                RestoreDefaults(HlgPatches);
            }
        }

        [Test]
        public void Bt2111_SrgbPrimariesClamped_HasNoNegatives_AndRec2100_KeepsSmallNegativesWhenUnclamped()
        {
            try
            {
                Reimport(Bt2111Pq, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Bt2111Pq);
                Assert.AreEqual(1920, tex.width);
                float min = MinRgb(tex);
                Assert.GreaterOrEqual(min, 0f, "clamped sRGB import must not contain negatives");

                Reimport(Bt2111Pq, importer => { MakeReadableUncompressed(importer); importer.hdrPrimaries = JxlHdrPrimaries.Rec2100; importer.hdrClampNegative = false; });
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Bt2111Pq);
                min = MinRgb(tex);
                // Decoder output in Rec.2100 primaries has a -0.004 sub-black element; scaled by 10000/203 that is about -0.2.
                Assert.Greater(min, -0.3f, "Rec.2100 primaries should avoid the large out-of-gamut negatives of sRGB");
                Assert.Less(min, 0f, "unclamped import keeps the pattern's sub-black element");
            }
            finally
            {
                RestoreDefaults(Bt2111Pq);
            }
        }

        [Test]
        public void HdrLinearFloat_PassesThroughUnscaled()
        {
            const string path = RealDir + "hdr_linear_float.jxl"; // R = 16*x/255, G = y/127 (y from the bottom, PFM order), B = 0.5
            try
            {
                Reimport(path, MakeReadableUncompressed);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreEqual(TextureFormat.RGBAHalf, tex.format);
                Color c = tex.GetPixel(255, 127);
                Assert.AreEqual(16f, c.r, 0.02f, "R");
                Assert.AreEqual(1f, c.g, 0.01f, "G at top row");
                Assert.AreEqual(0.5f, c.b, 0.01f, "B");
                c = tex.GetPixel(0, 0);
                Assert.AreEqual(0f, c.r, 0.01f, "R at x=0");
                Assert.AreEqual(0f, c.g, 0.01f, "G at bottom row");
            }
            finally
            {
                RestoreDefaults(path);
            }
        }

        [Test]
        public void Decode_Bt2111_LinearRec2100_HasNoLargeNegatives()
        {
            var options = new JxlDecodeOptions { Output = JxlPixelOutput.RgbaFloat, ColorTarget = JxlColorTarget.LinearRec2100, FlipVertically = true };
            JxlDecodedImage image = JxlDecoder.Decode(File.ReadAllBytes(Bt2111Pq), options);
            Assert.IsTrue(image.IsLinear);
            var floats = new float[image.Pixels.Length / 4];
            Buffer.BlockCopy(image.Pixels, 0, floats, 0, image.Pixels.Length);
            float min = float.MaxValue;
            for (int i = 0; i < floats.Length; i++)
                if ((i & 3) != 3 && floats[i] < min) min = floats[i];
            Assert.Greater(min, -0.01f);
        }

        static float MinRgb(Texture2D tex)
        {
            Color[] px = tex.GetPixels();
            float min = float.MaxValue;
            foreach (Color c in px)
            {
                if (c.r < min) min = c.r;
                if (c.g < min) min = c.g;
                if (c.b < min) min = c.b;
            }
            return min;
        }

        // ---------------------------------------------------------------- g. orientation and colour

        // Pixels of djxl's 8-bit PNG of sun_in_the_sky_lossy.jxl (PNG coordinates, row 0 = top).
        // In the Unity texture (row 0 = bottom) they sit at y = 511 - pngY.
        static readonly int[][] SunReferencePixels =
        {
            //          pngX, pngY,   R,   G,   B
            new[] {  10,  10,  67,  73,  98 },
            new[] { 100,  40,  88,  87, 102 },
            new[] { 400, 100, 211, 172, 101 },
            new[] { 480, 470,  73,  77,  99 },
            new[] { 256,  20, 153, 132, 108 },
        };

        [Test]
        public void Sun_Uncompressed_PixelsMatchReference()
        {
            try
            {
                Reimport(Sun, importer =>
                {
                    importer.textureSettings.readable = true;
                    importer.defaultPlatformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                });

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                Assert.AreEqual(512, tex.width);
                Assert.AreEqual(512, tex.height);

                // GetPixel would return sRGB-decoded floats; compare the stored bytes instead.
                Color32[] pixels = tex.GetPixels32();
                const int tolerance = 3;
                foreach (int[] p in SunReferencePixels)
                {
                    int x = p[0], unityY = tex.height - 1 - p[1];
                    Color32 c = pixels[unityY * tex.width + x];
                    string where = $"PNG ({p[0]},{p[1]}) = Unity ({x},{unityY}): got {c}, expected ({p[2]},{p[3]},{p[4]})";
                    Assert.LessOrEqual(Math.Abs(c.r - p[2]), tolerance, "R at " + where);
                    Assert.LessOrEqual(Math.Abs(c.g - p[3]), tolerance, "G at " + where);
                    Assert.LessOrEqual(Math.Abs(c.b - p[4]), tolerance, "B at " + where);
                }
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        // ---------------------------------------------------------------- h. HDR keeps a float format when uncompressed

        [Test]
        public void HdrFixture_Uncompressed_IsRgbaHalf()
        {
            try
            {
                Reimport(HdrTestPq, importer => importer.defaultPlatformSettings.textureCompression = TextureImporterCompression.Uncompressed);

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrTestPq);
                Assert.IsNotNull(tex);
                Assert.AreEqual(TextureFormat.RGBAHalf, tex.format);
                Assert.IsFalse(tex.isDataSRGB, "HDR textures must be linear.");
            }
            finally
            {
                RestoreDefaults(HdrTestPq);
            }
        }

        // ---------------------------------------------------------------- i. Sprite texture type

        [Test]
        public void Sun_SpriteType_ProducesSpriteSubAsset()
        {
            try
            {
                Reimport(Sun, importer => importer.textureSettings.textureType = TextureImporterType.Sprite);

                var main = AssetDatabase.LoadMainAssetAtPath(Sun);
                Assert.IsInstanceOf<Texture2D>(main, "Main asset should stay a Texture2D.");

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sun);
                Assert.IsNotNull(sprite, "No Sprite sub-asset.");
                Assert.AreEqual(512f, sprite.rect.width, "sprite width");
                Assert.AreEqual(512f, sprite.rect.height, "sprite height");
                Assert.AreEqual(100f, sprite.pixelsPerUnit, "pixels per unit");
                Assert.AreSame(main, sprite.texture, "Sprite should reference the imported texture.");
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        // ---------------------------------------------------------------- j. max size

        // ToNearest first rounds 3840x2160 to 4096x2048, then Max Size halves both: 256x128.
        // With Non-Power of 2 = None the aspect ratio is kept: 256x144.
        [TestCase(TextureImporterNPOTScale.ToNearest, 256, 128)]
        [TestCase(TextureImporterNPOTScale.None, 256, 144)]
        public void HardwareAlpha_MaxSize256_IsDownscaled(TextureImporterNPOTScale npot, int width, int height)
        {
            try
            {
                Reimport(HardwareAlpha, importer =>
                {
                    importer.textureSettings.npotScale = npot;
                    importer.defaultPlatformSettings.maxTextureSize = 256;
                });

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HardwareAlpha);
                Assert.IsNotNull(tex);
                Assert.AreEqual(width, tex.width, "width");
                Assert.AreEqual(height, tex.height, "height");
            }
            finally
            {
                RestoreDefaults(HardwareAlpha);
            }
        }

        // ---------------------------------------------------------------- l. per-platform override

        [Test]
        public void Sun_OverrideForActiveTarget_IsApplied()
        {
            string platform = JxlImporter.GetPlatformName(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            try
            {
                Reimport(Sun, importer =>
                {
                    var entry = importer.platformOverrides.Find(p => p.name == platform);
                    Assert.IsNotNull(entry, $"No override entry named '{platform}'.");
                    entry.overridden = true;
                    entry.maxTextureSize = 128;
                    entry.textureCompression = TextureImporterCompression.Uncompressed;
                });

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                Assert.AreEqual(128, tex.width, "width");
                Assert.AreEqual(128, tex.height, "height");
                Assert.AreEqual(TextureFormat.RGB24, tex.format);
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        // ---------------------------------------------------------------- k. normal map

        [Test]
        public void Sun_NormalMapType_ImportsCompressed()
        {
            try
            {
                Reimport(Sun, importer => importer.textureSettings.textureType = TextureImporterType.NormalMap);

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.IsNotNull(tex);
                Assert.AreEqual(512, tex.width, "width (a 4x4 texture means the magenta placeholder)");
                Assert.AreEqual(512, tex.height, "height");
                Assert.IsTrue(GraphicsFormatUtility.IsCompressedFormat(tex.format), $"Expected a compressed format, got {tex.format}.");
                Assert.IsFalse(tex.isDataSRGB, "Normal maps must be linear.");
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }
    }
}
