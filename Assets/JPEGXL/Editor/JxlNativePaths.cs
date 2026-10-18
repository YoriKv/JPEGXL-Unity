using System.IO;
using UnityEditor;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>
    /// Tells the native binding where the libjxl libraries live, so the thread-runner lookup can dlopen them
    /// by full path on macOS and Linux. Windows resolves the already-loaded module by name instead.
    /// Runs on editor load, before anything touches <see cref="JxlDecoder"/>.
    /// </summary>
    [InitializeOnLoad]
    static class JxlNativePaths
    {
        static JxlNativePaths()
        {
            // One jxl_threads library per host OS lives under Plugins/Editor; pick the one built for this host.
            string extension = Application.platform == RuntimePlatform.OSXEditor ? ".dylib"
                : Application.platform == RuntimePlatform.LinuxEditor ? ".so" : ".dll";
            foreach (string guid in AssetDatabase.FindAssets("jxl_threads"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(extension, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (AssetImporter.GetAtPath(path) is PluginImporter importer && importer.GetCompatibleWithEditor())
                {
                    Native.JxlNative.NativeLibraryDirectory = Path.GetFullPath(Path.GetDirectoryName(path));
                    break;
                }
            }
        }
    }
}
