using System.IO;
using UnityEditor;

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
            foreach (string guid in AssetDatabase.FindAssets("jxl_threads"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is PluginImporter importer && importer.GetCompatibleWithEditor())
                {
                    Native.JxlNative.NativeLibraryDirectory = Path.GetFullPath(Path.GetDirectoryName(path));
                    break;
                }
            }
        }
    }
}
