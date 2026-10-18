using System;
using System.Collections.Generic;
using JpegXL.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JpegXL.Tests
{
    /// <summary>Fixture paths and helpers shared by the EditMode tests.</summary>
    static class JxlTestUtil
    {
        internal const string SamplesDir = "Assets/JPEGXL/Tests/Samples/";
        internal const string SdrDir = SamplesDir + "SDR/";
        internal const string HdrDir = SamplesDir + "HDR/";
        internal const string RealDir = SamplesDir + "Real/";

        internal const string Sun = SdrDir + "sun_in_the_sky_lossy.jxl";
        internal const string Bt2111Pq = HdrDir + "bt2111_test_pattern_pq.jxl";

        /// <summary>
        /// Pixels of djxl's 8-bit PNG of <see cref="Sun"/> as { pngX, pngY, R, G, B } (PNG coordinates, row 0 = top).
        /// </summary>
        internal static readonly int[][] SunReferencePixels =
        {
            new[] {  10,  10,  67,  73,  98 },
            new[] { 100,  40,  88,  87, 102 },
            new[] { 400, 100, 211, 172, 101 },
            new[] { 480, 470,  73,  77,  99 },
            new[] { 256,  20, 153, 132, 108 },
        };

        internal static JxlImporter GetImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as JxlImporter;
            Assert.IsNotNull(importer, $"{path} is not imported by JxlImporter.");
            return importer;
        }

        /// <summary>
        /// Applies <paramref name="change"/> to a freshly fetched importer and reimports. Changing importer fields from
        /// code needs SetDirty before SaveAndReimport; callers must not keep importer references across calls.
        /// </summary>
        internal static void Reimport(string path, Action<JxlImporter> change)
        {
            JxlImporter importer = GetImporter(path);
            change(importer);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        /// <summary>Puts every setting back to the importer defaults so test order does not matter.</summary>
        internal static void RestoreDefaults(string path)
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
                importer.spriteSheet = new List<JxlSpriteRect>();
            });
        }

        /// <summary>Read/Write on, no NPOT scaling, no mipmaps, uncompressed: texels can be compared with source pixels.</summary>
        internal static void MakeReadableUncompressed(JxlImporter importer)
        {
            importer.textureSettings.readable = true;
            importer.textureSettings.npotScale = TextureImporterNPOTScale.None;
            importer.textureSettings.mipmapEnabled = false;
            importer.defaultPlatformSettings.textureCompression = TextureImporterCompression.Uncompressed;
        }

        /// <summary>The texel at PNG coordinates (row 0 = top) of a bottom-up Unity texture's <paramref name="pixels"/>.</summary>
        internal static Color32 PngPixel(Texture2D tex, Color32[] pixels, int pngX, int pngY) =>
            pixels[(tex.height - 1 - pngY) * tex.width + pngX];

        /// <summary>The importer's failure placeholder is a 4x4 texture.</summary>
        internal static bool IsPlaceholder(Texture2D tex) => tex.width == 4 && tex.height == 4;

        internal static void AssertChannel(int expected, int actual, int tolerance, string what) =>
            Assert.LessOrEqual(Math.Abs(expected - actual), tolerance, $"{what}: expected {expected}, got {actual}");

        /// <summary>RGBA float32 decoder output as a float array.</summary>
        internal static float[] ToFloats(byte[] pixels)
        {
            var floats = new float[pixels.Length / 4];
            Buffer.BlockCopy(pixels, 0, floats, 0, pixels.Length);
            return floats;
        }
    }
}
