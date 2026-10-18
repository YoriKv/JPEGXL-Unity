using System;
using System.Runtime.InteropServices;

namespace JpegXL.Editor
{
    /// <summary>Pixel layout of <see cref="JxlDecodedImage.Pixels"/>. Always 4 channels RGBA.</summary>
    internal enum JxlPixelOutput
    {
        /// <summary>1 byte per channel.</summary>
        Rgba8,
        /// <summary>4 bytes (float32, native endian) per channel.</summary>
        RgbaFloat,
    }

    /// <summary>Colour space the decoder should output.</summary>
    internal enum JxlColorTarget
    {
        /// <summary>Whatever libjxl outputs by default (the image's own colour space where possible).</summary>
        Original,
        /// <summary>Convert to sRGB primaries with the sRGB transfer curve (via jxl_cms).</summary>
        SRgb,
        /// <summary>Convert to sRGB primaries with a linear transfer (via jxl_cms).</summary>
        LinearSRgb,
        /// <summary>Convert to Rec.2100 (Rec.2020) primaries with a linear transfer (via jxl_cms). Keeps wide-gamut HDR colours without negative components.</summary>
        LinearRec2100,
    }

    /// <summary>Transfer function of the source image. <see cref="Icc"/>: only an ICC profile is available.</summary>
    internal enum JxlTransfer { Unknown, SRgb, Linear, PQ, HLG, Gamma, BT709, DCI, Icc }

    /// <summary>Colour primaries of the source image. <see cref="Icc"/>: only an ICC profile is available.</summary>
    internal enum JxlPrimaries { Unknown, SRgb, Rec2100, P3, Custom, Icc }

    /// <summary>How <see cref="JxlDecoder.Decode"/> lays out and converts its output.</summary>
    internal struct JxlDecodeOptions
    {
        public JxlPixelOutput Output;
        public JxlColorTarget ColorTarget;
        /// <summary>When true, row 0 of the output is the bottom row (Unity texture order).</summary>
        public bool FlipVertically;
        public bool UnpremultiplyAlpha;
        /// <summary>Peak display luminance in nits for libjxl tone mapping; 0 leaves it unset.</summary>
        public float DesiredIntensityTarget;

        /// <summary>Rgba8, Original, FlipVertically = true, UnpremultiplyAlpha = false, DesiredIntensityTarget = 0.</summary>
        public static JxlDecodeOptions Default => new JxlDecodeOptions
        {
            Output = JxlPixelOutput.Rgba8,
            ColorTarget = JxlColorTarget.Original,
            FlipVertically = true,
            UnpremultiplyAlpha = false,
            DesiredIntensityTarget = 0f,
        };
    }

    /// <summary>Header facts of a JPEG XL file (<see cref="JxlDecoder.ReadInfo"/>).</summary>
    internal sealed class JxlImageInfo
    {
        /// <summary>Width after orientation has been applied (libjxl applies orientation).</summary>
        public int Width { get; set; }
        /// <summary>Height after orientation has been applied (libjxl applies orientation).</summary>
        public int Height { get; set; }
        public int BitsPerSample { get; set; }
        /// <summary>0 for integer samples, otherwise the float exponent bits (e.g. 5 for half, 8 for float).</summary>
        public int ExponentBitsPerSample { get; set; }
        public bool HasAlpha { get; set; }
        public bool AlphaPremultiplied { get; set; }
        /// <summary>Extra channels including the main alpha channel.</summary>
        public int NumExtraChannels { get; set; }
        public bool HasAnimation { get; set; }
        /// <summary>Upper bound on intensity in nits, as reported by libjxl.</summary>
        public float IntensityTarget { get; set; }
        public bool UsesOriginalProfile { get; set; }
        public JxlTransfer Transfer { get; set; }
        public JxlPrimaries Primaries { get; set; }
        /// <summary>True when the colour profile is an attached ICC profile (no structured encoding available).</summary>
        public bool HasIccProfile { get; set; }
        /// <summary>Exif-style orientation 1-8. Always 1 here, because libjxl applies orientation to the pixels.</summary>
        public int Orientation { get; set; }
        /// <summary>Number of colour channels in the codestream (1 grey, 3 colour).</summary>
        public int NumColorChannels { get; set; }

        /// <summary>PQ or HLG transfer, or floating point samples.</summary>
        public bool IsHdr => Transfer == JxlTransfer.PQ || Transfer == JxlTransfer.HLG || ExponentBitsPerSample > 0;

        /// <summary>Primaries are known and not sRGB (Rec.2100, P3, custom or an ICC profile).</summary>
        public bool IsWideGamut => Primaries != JxlPrimaries.SRgb && Primaries != JxlPrimaries.Unknown;
    }

    /// <summary>Result of <see cref="JxlDecoder.Decode"/>.</summary>
    internal sealed class JxlDecodedImage
    {
        public JxlImageInfo Info { get; set; }
        public JxlPixelOutput Format { get; set; }
        /// <summary>True when the pixel values are linear light.</summary>
        public bool IsLinear { get; set; }
        /// <summary>
        /// RGBA, tightly packed, width * 4 * (1 or 4) bytes per row. Row 0 is the bottom row when
        /// <see cref="JxlDecodeOptions.FlipVertically"/> was set (Unity order), otherwise the top row.
        /// </summary>
        public byte[] Pixels { get; set; }
    }

    /// <summary>The file could not be decoded, or the native decoder is unavailable.</summary>
    internal sealed class JxlDecodeException : Exception
    {
        public JxlDecodeException(string message) : base(message) { }
        public JxlDecodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Safe wrapper over libjxl 0.12. Thread-safe: every call uses its own native decoder.</summary>
    internal static class JxlDecoder
    {
        private const uint ExpectedMinor = 12;

        private const int EventBasicInfo = (int)Native.JxlDecoderStatus.JXL_DEC_BASIC_INFO;
        private const int EventColorEncoding = (int)Native.JxlDecoderStatus.JXL_DEC_COLOR_ENCODING;
        private const int EventFullImage = (int)Native.JxlDecoderStatus.JXL_DEC_FULL_IMAGE;

        private static readonly IntPtr s_threadRunnerFn;
        private static readonly UIntPtr s_defaultThreads;

        /// <summary>True when the native libraries loaded and report version 0.12.x.</summary>
        public static bool IsAvailable { get; }

        /// <summary>"major.minor.patch" reported by jxl.dll, or null if it could not be loaded.</summary>
        public static string NativeVersion { get; }

        /// <summary>Why <see cref="IsAvailable"/> is false; null when available.</summary>
        public static string UnavailableReason { get; }

        /// <summary>True when libjxl's thread parallel runner could be resolved; otherwise decoding is single-threaded.</summary>
        public static bool IsMultithreaded => s_threadRunnerFn != IntPtr.Zero;

        static unsafe JxlDecoder()
        {
            string stage = "jxl";
            try
            {
                uint version = Native.JxlNative.JxlDecoderVersion();
                uint major = version / 1000000u;
                uint minor = (version / 1000u) % 1000u;
                uint patch = version % 1000u;
                NativeVersion = major + "." + minor + "." + patch;

                // The ABI pin: 0.12.x only. JxlBasicInfo and friends are layout-sensitive.
                if (version / 1000u != ExpectedMinor)
                {
                    UnavailableReason = "libjxl version " + NativeVersion + " (" + version +
                                          ") is not supported; these bindings are for 0.12.x. Replace the DLLs or update the bindings.";
                    return;
                }

                stage = "jxl_cms";
                if (Native.JxlNative.JxlGetDefaultCms() == null)
                    throw new JxlDecodeException("JxlGetDefaultCms returned null.");

                stage = "jxl_threads";
                s_defaultThreads = Native.JxlNative.JxlThreadParallelRunnerDefaultNumWorkerThreads();
                s_threadRunnerFn = Native.JxlNative.ResolveThreadRunnerFunction();

                IsAvailable = true;
            }
            catch (DllNotFoundException e)
            {
                UnavailableReason = "Native library '" + stage + "' could not be loaded (" + e.Message +
                                      "). Expected under Assets/JPEGXL/Plugins/Editor with an editor-only import setting for this host.";
            }
            catch (EntryPointNotFoundException e)
            {
                UnavailableReason = "Native library '" + stage + "' is missing an expected export (" + e.Message +
                                      "). The DLLs do not match the libjxl 0.12 bindings.";
            }
            catch (Exception e)
            {
                UnavailableReason = "Native library '" + stage + "' failed to initialise: " + e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>Reads header information (size, bit depth, colour encoding) without decoding pixels.</summary>
        public static unsafe JxlImageInfo ReadInfo(byte[] data)
        {
            EnsureAvailable();
            ValidateInput(data);

            IntPtr dec = CreateDecoder(EventBasicInfo | EventColorEncoding);
            try
            {
                fixed (byte* input = data)
                {
                    Check(Native.JxlNative.JxlDecoderSetInput(dec, input, (UIntPtr)(ulong)data.Length), "JxlDecoderSetInput");
                    Native.JxlNative.JxlDecoderCloseInput(dec);

                    JxlImageInfo info = null;
                    while (true)
                    {
                        Native.JxlDecoderStatus status = Native.JxlNative.JxlDecoderProcessInput(dec);
                        switch (status)
                        {
                            case Native.JxlDecoderStatus.JXL_DEC_BASIC_INFO:
                                info = ReadBasicInfo(dec);
                                break;

                            case Native.JxlDecoderStatus.JXL_DEC_COLOR_ENCODING:
                                if (info == null)
                                    throw new JxlDecodeException("JXL_DEC_COLOR_ENCODING arrived before JXL_DEC_BASIC_INFO.");
                                ClassifyColor(dec, info);
                                return info;

                            case Native.JxlDecoderStatus.JXL_DEC_SUCCESS:
                                if (info == null)
                                    throw new JxlDecodeException("Decoder reported JXL_DEC_SUCCESS without basic info.");
                                return info;

                            default:
                                throw StatusError(status, info == null ? "reading basic info" : "reading colour encoding");
                        }
                    }
                }
            }
            finally
            {
                Native.JxlNative.JxlDecoderDestroy(dec);
            }
        }

        /// <summary>Decodes the first frame (animations: first displayed frame) to 4-channel RGBA.</summary>
        public static unsafe JxlDecodedImage Decode(byte[] data, JxlDecodeOptions options)
        {
            EnsureAvailable();
            ValidateInput(data);

            int bytesPerChannel = options.Output == JxlPixelOutput.RgbaFloat ? 4 : 1;
            Native.JxlPixelFormat format = new Native.JxlPixelFormat
            {
                num_channels = 4,
                data_type = options.Output == JxlPixelOutput.RgbaFloat
                    ? Native.JxlDataType.JXL_TYPE_FLOAT
                    : Native.JxlDataType.JXL_TYPE_UINT8,
                endianness = Native.JxlEndianness.JXL_NATIVE_ENDIAN,
                align = UIntPtr.Zero,
            };

            IntPtr dec = IntPtr.Zero;
            IntPtr runner = IntPtr.Zero;
            GCHandle outHandle = default;
            byte[] pixels = null;
            JxlImageInfo info = null;
            bool done = false;

            try
            {
                dec = CreateDecoder(EventBasicInfo | EventColorEncoding | EventFullImage);
                if (s_threadRunnerFn != IntPtr.Zero)
                {
                    runner = Native.JxlNative.JxlThreadParallelRunnerCreate(IntPtr.Zero, s_defaultThreads);
                    if (runner == IntPtr.Zero)
                        throw new JxlDecodeException("JxlThreadParallelRunnerCreate failed.");
                    Check(Native.JxlNative.JxlDecoderSetParallelRunner(dec, s_threadRunnerFn, runner), "JxlDecoderSetParallelRunner");
                }

                Check(Native.JxlNative.JxlDecoderSetUnpremultiplyAlpha(dec,
                        options.UnpremultiplyAlpha ? Native.JxlNative.JXL_TRUE : Native.JxlNative.JXL_FALSE),
                    "JxlDecoderSetUnpremultiplyAlpha");
                if (options.DesiredIntensityTarget > 0f)
                    Check(Native.JxlNative.JxlDecoderSetDesiredIntensityTarget(dec, options.DesiredIntensityTarget), "JxlDecoderSetDesiredIntensityTarget");

                fixed (byte* input = data)
                {
                    Check(Native.JxlNative.JxlDecoderSetInput(dec, input, (UIntPtr)(ulong)data.Length), "JxlDecoderSetInput");
                    Native.JxlNative.JxlDecoderCloseInput(dec);

                    while (!done)
                    {
                        Native.JxlDecoderStatus status = Native.JxlNative.JxlDecoderProcessInput(dec);
                        switch (status)
                        {
                            case Native.JxlDecoderStatus.JXL_DEC_BASIC_INFO:
                                info = ReadBasicInfo(dec);
                                break;

                            case Native.JxlDecoderStatus.JXL_DEC_COLOR_ENCODING:
                                if (info == null)
                                    throw new JxlDecodeException("JXL_DEC_COLOR_ENCODING arrived before JXL_DEC_BASIC_INFO.");
                                ClassifyColor(dec, info);
                                if (options.ColorTarget != JxlColorTarget.Original)
                                    SetOutputColor(dec, options.ColorTarget, info.NumColorChannels == 1);
                                break;

                            case Native.JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER:
                            {
                                if (info == null)
                                    throw new JxlDecodeException("JXL_DEC_NEED_IMAGE_OUT_BUFFER arrived before JXL_DEC_BASIC_INFO.");
                                if (pixels != null)
                                    throw new JxlDecodeException("Decoder asked for a second image output buffer.");

                                UIntPtr size;
                                Check(Native.JxlNative.JxlDecoderImageOutBufferSize(dec, &format, &size), "JxlDecoderImageOutBufferSize");
                                ulong expected = (ulong)info.Width * (ulong)info.Height * 4ul * (ulong)bytesPerChannel;
                                ulong actual = size.ToUInt64();
                                if (actual != expected)
                                    throw new JxlDecodeException("Unexpected output buffer size " + actual + " for " +
                                                                 info.Width + "x" + info.Height + " (expected " + expected + ").");
                                if (actual > int.MaxValue)
                                    throw new JxlDecodeException("Image too large to decode into a managed buffer (" + actual + " bytes).");

                                pixels = new byte[(int)actual];
                                outHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                                Check(Native.JxlNative.JxlDecoderSetImageOutBuffer(dec, &format,
                                        (void*)outHandle.AddrOfPinnedObject(), size),
                                    "JxlDecoderSetImageOutBuffer");
                                break;
                            }

                            case Native.JxlDecoderStatus.JXL_DEC_FULL_IMAGE:
                            case Native.JxlDecoderStatus.JXL_DEC_SUCCESS:
                                if (pixels == null)
                                    throw new JxlDecodeException("Decoder reported " + status + " before any pixels were produced.");
                                done = true; // first frame only
                                break;

                            default:
                                throw StatusError(status, info == null ? "reading basic info" : pixels == null ? "reading headers" : "decoding pixels");
                        }
                    }
                }
            }
            finally
            {
                if (dec != IntPtr.Zero)
                    Native.JxlNative.JxlDecoderDestroy(dec);
                if (runner != IntPtr.Zero)
                    Native.JxlNative.JxlThreadParallelRunnerDestroy(runner);
                if (outHandle.IsAllocated)
                    outHandle.Free();
            }

            if (options.FlipVertically)
                FlipRows(pixels, info.Width * 4 * bytesPerChannel, info.Height);

            bool isLinear = options.ColorTarget == JxlColorTarget.LinearSRgb || options.ColorTarget == JxlColorTarget.LinearRec2100 ||
                            (options.ColorTarget == JxlColorTarget.Original && info.Transfer == JxlTransfer.Linear);

            return new JxlDecodedImage
            {
                Info = info,
                Format = options.Output,
                IsLinear = isLinear,
                Pixels = pixels,
            };
        }

        // ------------------------------------------------------------------ helpers

        private static void EnsureAvailable()
        {
            if (!IsAvailable)
                throw new JxlDecodeException("JPEG XL decoder unavailable: " + UnavailableReason);
        }

        private static void ValidateInput(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length == 0)
                throw new JxlDecodeException("Input is empty.");
        }

        /// <summary>A native decoder subscribed to <paramref name="events"/> that applies the file's orientation. The caller destroys it.</summary>
        private static IntPtr CreateDecoder(int events)
        {
            IntPtr dec = Native.JxlNative.JxlDecoderCreate(IntPtr.Zero);
            if (dec == IntPtr.Zero)
                throw new JxlDecodeException("JxlDecoderCreate failed.");
            try
            {
                Check(Native.JxlNative.JxlDecoderSubscribeEvents(dec, events), "JxlDecoderSubscribeEvents");
                Check(Native.JxlNative.JxlDecoderSetKeepOrientation(dec, Native.JxlNative.JXL_FALSE), "JxlDecoderSetKeepOrientation");
                return dec;
            }
            catch
            {
                Native.JxlNative.JxlDecoderDestroy(dec);
                throw;
            }
        }

        private static void Check(Native.JxlDecoderStatus status, string call)
        {
            if (status != Native.JxlDecoderStatus.JXL_DEC_SUCCESS)
                throw new JxlDecodeException(call + " failed with " + status + ".");
        }

        private static JxlDecodeException StatusError(Native.JxlDecoderStatus status, string stage)
        {
            switch (status)
            {
                case Native.JxlDecoderStatus.JXL_DEC_ERROR:
                    return new JxlDecodeException("libjxl returned JXL_DEC_ERROR while " + stage + " (corrupt or unsupported file).");
                case Native.JxlDecoderStatus.JXL_DEC_NEED_MORE_INPUT:
                    return new JxlDecodeException("libjxl returned JXL_DEC_NEED_MORE_INPUT while " + stage + " (file is truncated).");
                default:
                    return new JxlDecodeException("Unexpected decoder status " + status + " (" + (int)status + ") while " + stage + ".");
            }
        }

        private static unsafe JxlImageInfo ReadBasicInfo(IntPtr dec)
        {
            Native.JxlBasicInfo basic;
            Check(Native.JxlNative.JxlDecoderGetBasicInfo(dec, &basic), "JxlDecoderGetBasicInfo");

            if (basic.xsize == 0 || basic.ysize == 0 || basic.xsize > int.MaxValue || basic.ysize > int.MaxValue)
                throw new JxlDecodeException("Invalid image dimensions " + basic.xsize + "x" + basic.ysize + ".");

            return new JxlImageInfo
            {
                Width = (int)basic.xsize,
                Height = (int)basic.ysize,
                BitsPerSample = (int)basic.bits_per_sample,
                ExponentBitsPerSample = (int)basic.exponent_bits_per_sample,
                HasAlpha = basic.alpha_bits != 0,
                AlphaPremultiplied = basic.alpha_bits != 0 && basic.alpha_premultiplied != 0,
                NumExtraChannels = (int)basic.num_extra_channels,
                HasAnimation = basic.have_animation != 0,
                IntensityTarget = basic.intensity_target,
                UsesOriginalProfile = basic.uses_original_profile != 0,
                Orientation = (int)basic.orientation,
                NumColorChannels = (int)basic.num_color_channels,
                Transfer = JxlTransfer.Unknown,
                Primaries = JxlPrimaries.Unknown,
                HasIccProfile = false,
            };
        }

        /// <summary>
        /// Classifies the image's own (ORIGINAL, i.e. metadata) colour profile. TARGET_DATA would describe
        /// the decoder's output instead, which depends on the requested output and is not what the
        /// importer reports as the source colour space.
        /// </summary>
        private static unsafe void ClassifyColor(IntPtr dec, JxlImageInfo info)
        {
            Native.JxlColorEncoding enc;
            Native.JxlDecoderStatus status = Native.JxlNative.JxlDecoderGetColorAsEncodedProfile(
                dec, Native.JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, &enc);

            if (status == Native.JxlDecoderStatus.JXL_DEC_SUCCESS)
            {
                info.HasIccProfile = false;
                info.Transfer = MapTransfer(enc.transfer_function);
                // Primaries are meaningless for grey and XYB colour spaces (color_encoding.h).
                info.Primaries = enc.color_space == Native.JxlColorSpace.JXL_COLOR_SPACE_RGB
                    ? MapPrimaries(enc.primaries)
                    : JxlPrimaries.Unknown;
                return;
            }

            // No structured encoding: the codestream carries an ICC profile.
            UIntPtr iccSize;
            if (Native.JxlNative.JxlDecoderGetICCProfileSize(
                    dec, Native.JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, &iccSize) == Native.JxlDecoderStatus.JXL_DEC_SUCCESS
                && iccSize != UIntPtr.Zero)
            {
                info.HasIccProfile = true;
                info.Transfer = JxlTransfer.Icc;
                info.Primaries = JxlPrimaries.Icc;
            }
            else
            {
                info.HasIccProfile = false;
                info.Transfer = JxlTransfer.Unknown;
                info.Primaries = JxlPrimaries.Unknown;
            }
        }

        private static unsafe void SetOutputColor(IntPtr dec, JxlColorTarget target, bool isGray)
        {
            // Without a CMS libjxl only converts XYB images; the CMS makes conversion work for all inputs.
            // SetCms must precede SetPreferredColorProfile (decode.h). The CMS struct is a static owned by jxl_cms.
            Native.JxlCmsInterface* cms = Native.JxlNative.JxlGetDefaultCms();
            if (cms == null)
                throw new JxlDecodeException("JxlGetDefaultCms returned null.");
            Check(Native.JxlNative.JxlDecoderSetCms(dec, *cms), "JxlDecoderSetCms");

            Native.JxlColorEncoding enc;
            int gray = isGray ? Native.JxlNative.JXL_TRUE : Native.JxlNative.JXL_FALSE;
            if (target == JxlColorTarget.LinearSRgb)
                Native.JxlNative.JxlColorEncodingSetToLinearSRGB(&enc, gray);
            else if (target == JxlColorTarget.LinearRec2100)
            {
                // Same as SetToLinearSRGB but with Rec.2100 primaries (color_encoding.h enum values).
                Native.JxlNative.JxlColorEncodingSetToLinearSRGB(&enc, gray);
                if (!isGray)
                    enc.primaries = Native.JxlPrimaries.JXL_PRIMARIES_2100;
            }
            else
                Native.JxlNative.JxlColorEncodingSetToSRGB(&enc, gray);

            Check(Native.JxlNative.JxlDecoderSetPreferredColorProfile(dec, &enc), "JxlDecoderSetPreferredColorProfile");
        }

        private static JxlTransfer MapTransfer(Native.JxlTransferFunction tf)
        {
            switch (tf)
            {
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_PQ: return JxlTransfer.PQ;
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_HLG: return JxlTransfer.HLG;
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_LINEAR: return JxlTransfer.Linear;
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_SRGB: return JxlTransfer.SRgb;
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_709: return JxlTransfer.BT709;
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_DCI: return JxlTransfer.DCI;
                case Native.JxlTransferFunction.JXL_TRANSFER_FUNCTION_GAMMA: return JxlTransfer.Gamma;
                default: return JxlTransfer.Unknown;
            }
        }

        private static JxlPrimaries MapPrimaries(Native.JxlPrimaries p)
        {
            switch (p)
            {
                case Native.JxlPrimaries.JXL_PRIMARIES_SRGB: return JxlPrimaries.SRgb;
                case Native.JxlPrimaries.JXL_PRIMARIES_2100: return JxlPrimaries.Rec2100;
                case Native.JxlPrimaries.JXL_PRIMARIES_P3: return JxlPrimaries.P3;
                case Native.JxlPrimaries.JXL_PRIMARIES_CUSTOM: return JxlPrimaries.Custom;
                default: return JxlPrimaries.Unknown;
            }
        }

        /// <summary>In-place vertical flip: libjxl writes rows top-down, Unity textures are bottom-up.</summary>
        private static void FlipRows(byte[] pixels, int stride, int height)
        {
            if (height < 2)
                return;
            byte[] tmp = new byte[stride];
            for (int top = 0, bottom = height - 1; top < bottom; top++, bottom--)
            {
                int a = top * stride;
                int b = bottom * stride;
                Buffer.BlockCopy(pixels, a, tmp, 0, stride);
                Buffer.BlockCopy(pixels, b, pixels, a, stride);
                Buffer.BlockCopy(tmp, 0, pixels, b, stride);
            }
        }
    }
}
