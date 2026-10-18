using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace JpegXL.Editor
{
    /// <summary>Tools/JPEG XL/Self Test: decodes every sample under Assets/JPEGXL/Tests/Samples and logs the results.</summary>
    internal static class JxlSelfTest
    {
        private const string SamplesFolder = "Assets/JPEGXL/Tests/Samples";

        [MenuItem("Tools/JPEG XL/Self Test")]
        private static void Run()
        {
            Debug.Log("JPEG XL self test: native libjxl version " + (JxlDecoder.NativeVersion ?? "<not loaded>") +
                      ", available=" + JxlDecoder.IsAvailable + ", multithreaded=" + JxlDecoder.IsMultithreaded);
            if (!JxlDecoder.IsAvailable)
                Debug.LogError("JPEG XL self test: decoder unavailable: " + JxlDecoder.UnavailableReason);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string samplesDir = Path.Combine(projectRoot, SamplesFolder);
            if (!Directory.Exists(samplesDir))
            {
                Debug.LogError("JPEG XL self test: samples folder not found: " + SamplesFolder);
                Debug.LogError("JPEG XL self test: 0 ok, 0 failed");
                return;
            }

            string[] files = Directory.GetFiles(samplesDir, "*.jxl", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0)
                Debug.LogWarning("JPEG XL self test: no .jxl files under " + SamplesFolder);

            int ok = 0;
            int failed = 0;
            foreach (string file in files)
            {
                string display = ToAssetPath(projectRoot, file);
                try
                {
                    byte[] data = File.ReadAllBytes(file);
                    JxlImageInfo info = JxlDecoder.ReadInfo(data);

                    JxlDecodeOptions options = JxlDecodeOptions.Default;
                    if (info.IsHdr)
                    {
                        options.Output = JxlPixelOutput.RgbaFloat;
                        options.ColorTarget = JxlColorTarget.LinearSRgb;
                    }
                    else
                    {
                        options.Output = JxlPixelOutput.Rgba8;
                        options.ColorTarget = JxlColorTarget.Original;
                    }

                    Stopwatch sw = Stopwatch.StartNew();
                    JxlDecodedImage image = JxlDecoder.Decode(data, options);
                    sw.Stop();

                    int bytesPerChannel = image.Format == JxlPixelOutput.RgbaFloat ? 4 : 1;
                    long expectedBytes = (long)image.Info.Width * image.Info.Height * 4 * bytesPerChannel;
                    if (image.Pixels == null || image.Pixels.LongLength != expectedBytes)
                        throw new JxlDecodeException("Pixel buffer has " + (image.Pixels == null ? 0 : image.Pixels.LongLength) +
                                                     " bytes, expected " + expectedBytes + ".");

                    string line = string.Format(CultureInfo.InvariantCulture,
                        "JPEG XL self test: OK {0}: {1}x{2}, {3} bit{4}, transfer {5}, primaries {6}{7}, alpha {8}{9}, " +
                        "HDR {10}, wide gamut {11}, intensity target {12:0.##} nits, output {13}/{14} (linear {15}), decode {16:0.00} ms",
                        display, image.Info.Width, image.Info.Height, image.Info.BitsPerSample,
                        image.Info.ExponentBitsPerSample > 0 ? " float (exp " + image.Info.ExponentBitsPerSample + ")" : "",
                        image.Info.Transfer, image.Info.Primaries, image.Info.HasIccProfile ? " (ICC)" : "",
                        image.Info.HasAlpha ? "yes" : "no", image.Info.AlphaPremultiplied ? " (premultiplied)" : "",
                        image.Info.IsHdr, image.Info.IsWideGamut, image.Info.IntensityTarget,
                        image.Format, options.ColorTarget, image.IsLinear, sw.Elapsed.TotalMilliseconds);

                    if (image.Format == JxlPixelOutput.RgbaFloat)
                    {
                        RgbRange(image.Pixels, out float min, out float max, out long nonFinite);
                        line += string.Format(CultureInfo.InvariantCulture, ", RGB min {0:0.######} max {1:0.######}", min, max);
                        if (nonFinite > 0)
                            line += ", " + nonFinite + " non-finite values";
                    }

                    Debug.Log(line);
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError("JPEG XL self test: FAILED " + display + ": " + e.GetType().Name + ": " + e.Message);
                }
            }

            string summary = "JPEG XL self test: " + ok + " ok, " + failed + " failed";
            if (failed > 0)
                Debug.LogError(summary);
            else
                Debug.Log(summary);
        }

        private static string ToAssetPath(string projectRoot, string fullPath)
        {
            string path = fullPath;
            if (path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(projectRoot.Length).TrimStart('/', '\\');
            return path.Replace('\\', '/');
        }

        /// <summary>Min and max over the R, G and B channels of tightly packed float32 RGBA; alpha is ignored.</summary>
        private static unsafe void RgbRange(byte[] pixels, out float min, out float max, out long nonFinite)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            nonFinite = 0;
            long count = pixels.LongLength / 4;
            fixed (byte* p = pixels)
            {
                float* f = (float*)p;
                for (long i = 0; i < count; i++)
                {
                    if ((i & 3) == 3)
                        continue; // alpha
                    float v = f[i];
                    if (float.IsNaN(v) || float.IsInfinity(v))
                    {
                        nonFinite++;
                        continue;
                    }
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
        }
    }
}
