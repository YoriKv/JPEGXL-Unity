// Minimal hand-written P/Invoke bindings for libjxl v0.12.0 (decoder, default CMS, thread runner).
// Struct layouts and enum values are taken from the v0.12.0 C headers (decode.h, types.h,
// codestream_header.h, color_encoding.h, cms_interface.h, thread_parallel_runner.h).
// These are layout-sensitive: bump together with the DLLs and re-check every struct.
//
// C type mapping used throughout:
//   JXL_BOOL      -> int (32-bit)
//   C enums       -> 32-bit int-backed C# enums
//   size_t        -> UIntPtr
//   T* / void*    -> raw pointers or IntPtr

using System;
using System.Runtime.InteropServices;

namespace JpegXL.Editor.Native
{
    // ---------------------------------------------------------------- types.h

    internal enum JxlDataType : int
    {
        JXL_TYPE_FLOAT = 0,
        JXL_TYPE_UINT8 = 2,
        JXL_TYPE_UINT16 = 3,
        JXL_TYPE_FLOAT16 = 5,
    }

    internal enum JxlEndianness : int
    {
        JXL_NATIVE_ENDIAN = 0,
        JXL_LITTLE_ENDIAN = 1,
        JXL_BIG_ENDIAN = 2,
    }

    /// <summary>types.h JxlPixelFormat. x64 size 24 (size_t align at offset 16).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JxlPixelFormat
    {
        public uint num_channels;
        public JxlDataType data_type;
        public JxlEndianness endianness;
        public UIntPtr align;
    }

    // ---------------------------------------------------- codestream_header.h

    internal enum JxlOrientation : int
    {
        JXL_ORIENT_IDENTITY = 1,
        JXL_ORIENT_FLIP_HORIZONTAL = 2,
        JXL_ORIENT_ROTATE_180 = 3,
        JXL_ORIENT_FLIP_VERTICAL = 4,
        JXL_ORIENT_TRANSPOSE = 5,
        JXL_ORIENT_ROTATE_90_CW = 6,
        JXL_ORIENT_ANTI_TRANSPOSE = 7,
        JXL_ORIENT_ROTATE_90_CCW = 8,
    }

    /// <summary>codestream_header.h JxlPreviewHeader. Size 8.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JxlPreviewHeader
    {
        public uint xsize;
        public uint ysize;
    }

    /// <summary>codestream_header.h JxlAnimationHeader. Size 16.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JxlAnimationHeader
    {
        public uint tps_numerator;
        public uint tps_denominator;
        public uint num_loops;
        public int have_timecodes; // JXL_BOOL
    }

    /// <summary>
    /// codestream_header.h JxlBasicInfo. All members are 4 bytes wide, so there is no internal padding:
    /// 18 scalars (72) + preview (8) + animation (16) + intrinsic size (8) + padding[100] = 204 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct JxlBasicInfo
    {
        public int have_container;            // JXL_BOOL
        public uint xsize;
        public uint ysize;
        public uint bits_per_sample;
        public uint exponent_bits_per_sample;
        public float intensity_target;
        public float min_nits;
        public int relative_to_max_display;   // JXL_BOOL
        public float linear_below;
        public int uses_original_profile;     // JXL_BOOL
        public int have_preview;              // JXL_BOOL
        public int have_animation;            // JXL_BOOL
        public JxlOrientation orientation;
        public uint num_color_channels;
        public uint num_extra_channels;
        public uint alpha_bits;
        public uint alpha_exponent_bits;
        public int alpha_premultiplied;       // JXL_BOOL
        public JxlPreviewHeader preview;
        public JxlAnimationHeader animation;
        public uint intrinsic_xsize;
        public uint intrinsic_ysize;
        public fixed byte padding[100];
    }

    // ------------------------------------------------------- color_encoding.h

    internal enum JxlColorSpace : int
    {
        JXL_COLOR_SPACE_RGB = 0,
        JXL_COLOR_SPACE_GRAY = 1,
        JXL_COLOR_SPACE_XYB = 2,
        JXL_COLOR_SPACE_UNKNOWN = 3,
    }

    internal enum JxlWhitePoint : int
    {
        JXL_WHITE_POINT_D65 = 1,
        JXL_WHITE_POINT_CUSTOM = 2,
        JXL_WHITE_POINT_E = 10,
        JXL_WHITE_POINT_DCI = 11,
    }

    internal enum JxlPrimaries : int
    {
        JXL_PRIMARIES_SRGB = 1,
        JXL_PRIMARIES_CUSTOM = 2,
        JXL_PRIMARIES_2100 = 9,
        JXL_PRIMARIES_P3 = 11,
    }

    internal enum JxlTransferFunction : int
    {
        JXL_TRANSFER_FUNCTION_709 = 1,
        JXL_TRANSFER_FUNCTION_UNKNOWN = 2,
        JXL_TRANSFER_FUNCTION_LINEAR = 8,
        JXL_TRANSFER_FUNCTION_SRGB = 13,
        JXL_TRANSFER_FUNCTION_PQ = 16,
        JXL_TRANSFER_FUNCTION_DCI = 17,
        JXL_TRANSFER_FUNCTION_HLG = 18,
        JXL_TRANSFER_FUNCTION_GAMMA = 65535,
    }

    internal enum JxlRenderingIntent : int
    {
        JXL_RENDERING_INTENT_PERCEPTUAL = 0,
        JXL_RENDERING_INTENT_RELATIVE = 1,
        JXL_RENDERING_INTENT_SATURATION = 2,
        JXL_RENDERING_INTENT_ABSOLUTE = 3,
    }

    /// <summary>
    /// color_encoding.h JxlColorEncoding. The double[2] arrays are spelled out as pairs of doubles
    /// (identical layout, no fixed-buffer alignment questions). x64 offsets:
    /// color_space 0, white_point 4, white_point_xy 8, primaries 24, (pad 4), red 32, green 48,
    /// blue 64, transfer_function 80, (pad 4), gamma 88, rendering_intent 96, (tail pad 4) = 104 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JxlColorEncoding
    {
        public JxlColorSpace color_space;
        public JxlWhitePoint white_point;
        public double white_point_x;
        public double white_point_y;
        public JxlPrimaries primaries;
        public double primaries_red_x;
        public double primaries_red_y;
        public double primaries_green_x;
        public double primaries_green_y;
        public double primaries_blue_x;
        public double primaries_blue_y;
        public JxlTransferFunction transfer_function;
        public double gamma;
        public JxlRenderingIntent rendering_intent;
    }

    // -------------------------------------------------------- cms_interface.h

    /// <summary>
    /// cms_interface.h JxlCmsInterface: eight pointer-sized members (data pointers and function
    /// pointers), 64 bytes on x64. Passed by value to JxlDecoderSetCms; only ever copied from
    /// JxlGetDefaultCms(), never filled in managed code.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JxlCmsInterface
    {
        public IntPtr set_fields_data;
        public IntPtr set_fields_from_icc;
        public IntPtr init_data;
        public IntPtr init;
        public IntPtr get_src_buf;
        public IntPtr get_dst_buf;
        public IntPtr run;
        public IntPtr destroy;
    }

    // --------------------------------------------------------------- decode.h

    internal enum JxlDecoderStatus : int
    {
        JXL_DEC_SUCCESS = 0,
        JXL_DEC_ERROR = 1,
        JXL_DEC_NEED_MORE_INPUT = 2,
        JXL_DEC_NEED_PREVIEW_OUT_BUFFER = 3,
        JXL_DEC_NEED_IMAGE_OUT_BUFFER = 5,
        JXL_DEC_JPEG_NEED_MORE_OUTPUT = 6,
        JXL_DEC_BOX_NEED_MORE_OUTPUT = 7,
        JXL_DEC_BASIC_INFO = 0x40,
        JXL_DEC_COLOR_ENCODING = 0x100,
        JXL_DEC_PREVIEW_IMAGE = 0x200,
        JXL_DEC_FRAME = 0x400,
        JXL_DEC_FULL_IMAGE = 0x1000,
        JXL_DEC_JPEG_RECONSTRUCTION = 0x2000,
        JXL_DEC_BOX = 0x4000,
        JXL_DEC_FRAME_PROGRESSION = 0x8000,
        JXL_DEC_BOX_COMPLETE = 0x10000,
    }

    internal enum JxlColorProfileTarget : int
    {
        JXL_COLOR_PROFILE_TARGET_ORIGINAL = 0,
        JXL_COLOR_PROFILE_TARGET_DATA = 1,
    }

    internal static unsafe class JxlNative
    {
        private const string JxlLib = "jxl";
        private const string JxlCmsLib = "jxl_cms";
        private const string JxlThreadsLib = "jxl_threads";

        internal const int JXL_TRUE = 1;
        internal const int JXL_FALSE = 0;

        // ---- decode.h

        /// <returns>MAJOR * 1000000 + MINOR * 1000 + PATCH (0.12.0 -> 12000).</returns>
        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern uint JxlDecoderVersion();

        /// <param name="memoryManager">const JxlMemoryManager*, IntPtr.Zero for the default allocator.</param>
        /// <returns>JxlDecoder*, or IntPtr.Zero on failure.</returns>
        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr JxlDecoderCreate(IntPtr memoryManager);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void JxlDecoderDestroy(IntPtr dec);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSubscribeEvents(IntPtr dec, int eventsWanted);

        /// <param name="parallelRunner">JxlParallelRunner function pointer.</param>
        /// <param name="parallelRunnerOpaque">Runner instance from JxlThreadParallelRunnerCreate.</param>
        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetParallelRunner(IntPtr dec, IntPtr parallelRunner, IntPtr parallelRunnerOpaque);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetKeepOrientation(IntPtr dec, int skipReorientation);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetUnpremultiplyAlpha(IntPtr dec, int unpremulAlpha);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderProcessInput(IntPtr dec);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetInput(IntPtr dec, byte* data, UIntPtr size);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void JxlDecoderCloseInput(IntPtr dec);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderGetBasicInfo(IntPtr dec, JxlBasicInfo* info);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderGetColorAsEncodedProfile(IntPtr dec, JxlColorProfileTarget target, JxlColorEncoding* colorEncoding);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderGetICCProfileSize(IntPtr dec, JxlColorProfileTarget target, UIntPtr* size);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetPreferredColorProfile(IntPtr dec, JxlColorEncoding* colorEncoding);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetDesiredIntensityTarget(IntPtr dec, float desiredIntensityTarget);

        /// <summary>Takes the JxlCmsInterface struct by value (64 bytes; x64 passes it via a hidden copy pointer).</summary>
        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetCms(IntPtr dec, JxlCmsInterface cms);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderImageOutBufferSize(IntPtr dec, JxlPixelFormat* format, UIntPtr* size);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlDecoderStatus JxlDecoderSetImageOutBuffer(IntPtr dec, JxlPixelFormat* format, void* buffer, UIntPtr size);

        // ---- encode.h (colour encoding helpers, exported from jxl.dll)

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void JxlColorEncodingSetToSRGB(JxlColorEncoding* colorEncoding, int isGray);

        [DllImport(JxlLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void JxlColorEncodingSetToLinearSRGB(JxlColorEncoding* colorEncoding, int isGray);

        // ---- cms.h (jxl_cms.dll)

        /// <returns>const JxlCmsInterface* to a static instance owned by the library.</returns>
        [DllImport(JxlCmsLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern JxlCmsInterface* JxlGetDefaultCms();

        // ---- thread_parallel_runner.h (jxl_threads.dll)

        /// <param name="memoryManager">const JxlMemoryManager*, IntPtr.Zero for the default allocator.</param>
        [DllImport(JxlThreadsLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr JxlThreadParallelRunnerCreate(IntPtr memoryManager, UIntPtr numWorkerThreads);

        [DllImport(JxlThreadsLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void JxlThreadParallelRunnerDestroy(IntPtr runnerOpaque);

        [DllImport(JxlThreadsLib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern UIntPtr JxlThreadParallelRunnerDefaultNumWorkerThreads();

        // ---- Resolving the native address of JxlThreadParallelRunner.
        // JxlDecoderSetParallelRunner wants the C function pointer of the runner callback, which is never called
        // from managed code. .NET Standard 2.1 has no NativeLibrary, so on Windows ask kernel32 for the export of
        // the already-loaded jxl_threads.dll. Callers must make a jxl_threads P/Invoke first so the module is loaded.

        [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, EntryPoint = "GetProcAddress")]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        // On macOS and Linux the equivalent is dlopen/dlsym. A bare file name only resolves if the
        // dynamic linker already knows the library by that name, so the editor supplies the plugin
        // folder through NativeLibraryDirectory (set by JxlNativeSetup at editor load).

        /// <summary>Directory containing the libjxl native libraries; used for dlopen on non-Windows hosts.</summary>
        internal static string NativeLibraryDirectory { get; set; }

        private const int RTLD_NOW = 2;

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen_linux(string path, int flags);
        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        private static extern IntPtr dlsym_linux(IntPtr handle, string symbol);
        [DllImport("libdl.dylib", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen_mac(string path, int flags);
        [DllImport("libdl.dylib", EntryPoint = "dlsym")]
        private static extern IntPtr dlsym_mac(IntPtr handle, string symbol);

        /// <returns>Address of JxlThreadParallelRunner, or IntPtr.Zero when it cannot be resolved
        /// (module not loaded, unknown host). Zero means: decode without a runner (single-threaded).</returns>
        internal static IntPtr ResolveThreadRunnerFunction()
        {
            const string symbol = "JxlThreadParallelRunner";
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    IntPtr module = GetModuleHandleW("jxl_threads.dll");
                    return module == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(module, symbol);
                }

                bool mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                string file = mac ? "libjxl_threads.dylib" : "libjxl_threads.so";
                string path = string.IsNullOrEmpty(NativeLibraryDirectory) ? file : System.IO.Path.Combine(NativeLibraryDirectory, file);
                IntPtr handle = mac ? dlopen_mac(path, RTLD_NOW) : dlopen_linux(path, RTLD_NOW);
                if (handle == IntPtr.Zero && path != file)
                    handle = mac ? dlopen_mac(file, RTLD_NOW) : dlopen_linux(file, RTLD_NOW);
                if (handle == IntPtr.Zero)
                    return IntPtr.Zero;
                return mac ? dlsym_mac(handle, symbol) : dlsym_linux(handle, symbol);
            }
            catch (Exception)
            {
                return IntPtr.Zero; // DllNotFoundException for libdl variants, or anything else: fall back to single-threaded.
            }
        }
    }
}
