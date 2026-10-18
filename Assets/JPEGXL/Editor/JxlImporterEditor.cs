using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>Inspector for <see cref="JxlImporter"/>: grouped import settings plus read-only image information.</summary>
    [CustomEditor(typeof(JxlImporter))]
    [CanEditMultipleObjects]
    class JxlImporterEditor : ScriptedImporterEditor
    {
        SerializedProperty m_SRGBTexture;
        SerializedProperty m_GenerateMipMaps;
        SerializedProperty m_FilterMode;
        SerializedProperty m_WrapMode;
        SerializedProperty m_AnisoLevel;
        SerializedProperty m_AlphaSource;
        SerializedProperty m_AlphaIsTransparency;
        SerializedProperty m_UnpremultiplyAlpha;
        SerializedProperty m_ConvertToSRGB;
        SerializedProperty m_HdrPrecision;
        SerializedProperty m_Compression;
        SerializedProperty m_IsReadable;

        JxlImageInfo m_Info;
        string m_InfoError;

        bool m_ShowInfo = true;

        public override void OnEnable()
        {
            base.OnEnable();

            m_SRGBTexture = serializedObject.FindProperty(nameof(JxlImporter.sRGBTexture));
            m_GenerateMipMaps = serializedObject.FindProperty(nameof(JxlImporter.generateMipMaps));
            m_FilterMode = serializedObject.FindProperty(nameof(JxlImporter.filterMode));
            m_WrapMode = serializedObject.FindProperty(nameof(JxlImporter.wrapMode));
            m_AnisoLevel = serializedObject.FindProperty(nameof(JxlImporter.anisoLevel));
            m_AlphaSource = serializedObject.FindProperty(nameof(JxlImporter.alphaSource));
            m_AlphaIsTransparency = serializedObject.FindProperty(nameof(JxlImporter.alphaIsTransparency));
            m_UnpremultiplyAlpha = serializedObject.FindProperty(nameof(JxlImporter.unpremultiplyAlpha));
            m_ConvertToSRGB = serializedObject.FindProperty(nameof(JxlImporter.convertToSRGB));
            m_HdrPrecision = serializedObject.FindProperty(nameof(JxlImporter.hdrPrecision));
            m_Compression = serializedObject.FindProperty(nameof(JxlImporter.compression));
            m_IsReadable = serializedObject.FindProperty(nameof(JxlImporter.isReadable));

            ReadImageInfo();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Header("Texture");
            Field(m_SRGBTexture, "sRGB (Color Texture)");
            Field(m_GenerateMipMaps, "Generate Mip Maps");
            Field(m_FilterMode, "Filter Mode");
            Field(m_WrapMode, "Wrap Mode");
            Field(m_AnisoLevel, "Aniso Level");

            Header("Alpha");
            Field(m_AlphaSource, "Alpha Source");
            Field(m_AlphaIsTransparency, "Alpha Is Transparency");
            Field(m_UnpremultiplyAlpha, "Unpremultiply Alpha");

            Header("Color");
            Field(m_ConvertToSRGB, "Convert Wide Gamut To sRGB");

            Header("HDR");
            Field(m_HdrPrecision, "HDR Precision");

            Header("Compression");
            Field(m_Compression, "Compression");

            Header("Advanced");
            Field(m_IsReadable, "Read/Write");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawImageInfo();

            ApplyRevertGUI();
        }

        /// <summary>Reads the file header through libjxl (no pixel decode) so the inspector can show image facts.</summary>
        void ReadImageInfo()
        {
            m_Info = null;
            m_InfoError = null;
            if (targets.Length != 1 || !(target is JxlImporter importer))
                return;
            try
            {
                if (!JxlDecoder.IsAvailable)
                    m_InfoError = "Decoder not available.";
                else
                    m_Info = JxlDecoder.ReadInfo(File.ReadAllBytes(importer.assetPath));
            }
            catch (Exception e)
            {
                m_InfoError = e.Message;
            }
        }

        void DrawImageInfo()
        {
            m_ShowInfo = EditorGUILayout.Foldout(m_ShowInfo, "Image info", true);
            if (!m_ShowInfo)
                return;

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
            {
                if (serializedObject.isEditingMultipleObjects)
                {
                    EditorGUILayout.LabelField("Select a single asset to see its image info.");
                    return;
                }

                if (m_Info == null)
                {
                    EditorGUILayout.LabelField(m_InfoError ?? "No image info available.");
                }
                else
                {
                    EditorGUILayout.LabelField("Size", $"{m_Info.Width} x {m_Info.Height}");
                    EditorGUILayout.LabelField("Bits Per Sample", m_Info.ExponentBitsPerSample > 0 ? $"{m_Info.BitsPerSample} (float)" : m_Info.BitsPerSample.ToString());
                    EditorGUILayout.LabelField("Transfer", m_Info.Transfer.ToString());
                    EditorGUILayout.LabelField("Primaries", m_Info.Primaries.ToString() + (m_Info.IsWideGamut ? " (wide gamut)" : ""));
                    EditorGUILayout.LabelField("Alpha", m_Info.HasAlpha ? (m_Info.AlphaPremultiplied ? "Yes (premultiplied)" : "Yes") : "No");
                    EditorGUILayout.LabelField("HDR", m_Info.IsHdr ? "Yes (imported linear)" : "No");
                    if (m_Info.NumExtraChannels > 0 || m_Info.HasAnimation)
                        EditorGUILayout.LabelField("Note", (m_Info.HasAnimation ? "Animated (first frame only). " : "") + (m_Info.NumExtraChannels > 0 ? $"{m_Info.NumExtraChannels} extra channel(s) ignored." : ""));
                }

                if (assetTarget is Texture2D tex)
                {
                    EditorGUILayout.LabelField("Texture Format", tex.format.ToString());
                    EditorGUILayout.LabelField("Mip Count", tex.mipmapCount.ToString());
                }

                EditorGUILayout.LabelField("Decoder", JxlDecoder.IsAvailable ? "libjxl " + JxlDecoder.NativeVersion : "Not available");
            }
        }

        static void Header(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static void Field(SerializedProperty property, string label)
        {
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }
    }
}
