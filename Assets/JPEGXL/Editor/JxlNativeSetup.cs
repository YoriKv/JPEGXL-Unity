using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>
    /// Editor-load setup for the native decoder. On macOS and Linux it tells the binding where the libjxl libraries
    /// live, so the thread-runner lookup can dlopen them by full path (Windows resolves the loaded module by name).
    /// It also registers <see cref="DecoderDependency"/>, a custom dependency on the loaded libjxl version, so that
    /// replacing the libraries (or fixing a failed load) reimports every .jxl asset.
    /// </summary>
    [InitializeOnLoad]
    static class JxlNativeSetup
    {
        /// <summary>Custom dependency name every <see cref="JxlImporter"/> import depends on.</summary>
        internal const string DecoderDependency = "JpegXL/NativeDecoder";

        static JxlNativeSetup()
        {
            // Import worker processes skip both steps: the asset database is not ours to query or modify there.
            // Without the directory hint, dlopen falls back to the bare library name; at worst decoding is
            // single-threaded, which produces the same pixels.
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;

            // Must run before anything touches JxlDecoder: its static constructor resolves the thread runner.
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Native.JxlNative.NativeLibraryDirectory = FindLibraryDirectory();

            string decoder = JxlDecoder.IsAvailable ? "libjxl " + JxlDecoder.NativeVersion : "unavailable " + JxlDecoder.NativeVersion;
            try
            {
                AssetDatabase.RegisterCustomDependency(DecoderDependency, Hash128.Compute(decoder));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"JPEG XL: could not register the '{DecoderDependency}' import dependency: {e.Message}");
            }
        }

        /// <summary>Full path of the editor-compatible folder holding jxl_threads for this host, or null.</summary>
        static string FindLibraryDirectory()
        {
            string extension = Application.platform == RuntimePlatform.OSXEditor ? ".dylib" : ".so";
            foreach (string guid in AssetDatabase.FindAssets("jxl_threads"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) &&
                    AssetImporter.GetAtPath(path) is PluginImporter importer && importer.GetCompatibleWithEditor())
                    return Path.GetFullPath(Path.GetDirectoryName(path));
            }
            return null;
        }
    }
}
