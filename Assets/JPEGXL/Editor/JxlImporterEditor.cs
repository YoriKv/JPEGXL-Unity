using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace JpegXL.Editor
{
    /// <summary>
    /// Inspector for <see cref="JxlImporter"/>. The layout follows the built-in Texture import inspector (texture type,
    /// per-type section, Advanced, wrap/filter/aniso, platform tabs) and edits <see cref="TextureImporterSettings"/> and
    /// <see cref="TextureImporterPlatformSettings"/> through SerializedProperty so Apply/Revert and multi-editing work.
    /// Most native-backed fields serialize as ints (booleans and enums included), hence the int-based helpers below.
    /// </summary>
    [CustomEditor(typeof(JxlImporter))]
    [CanEditMultipleObjects]
    class JxlImporterEditor : ScriptedImporterEditor
    {
        static readonly GUIContent[] s_TypeLabels =
        {
            new GUIContent("Default"),
            new GUIContent("Normal map"),
            new GUIContent("Sprite (2D and UI)"),
            new GUIContent("Single Channel"),
        };

        static readonly int[] s_TypeValues =
        {
            (int)TextureImporterType.Default,
            (int)TextureImporterType.NormalMap,
            (int)TextureImporterType.Sprite,
            (int)TextureImporterType.SingleChannel,
        };

        static readonly int[] s_MaxSizeValues = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
        static readonly GUIContent[] s_MaxSizeLabels = Array.ConvertAll(s_MaxSizeValues, v => new GUIContent(v.ToString()));

        // Display names for the platform tabs; index 0 is the default settings, the rest follow JxlImporter.OverridePlatformNames.
        static readonly string[] s_PlatformTabs = { "Default", "PC, Mac & Linux Standalone", "Android", "iOS", "WebGL" };
        static readonly string[] s_PlatformTabIcons = { null, "BuildSettings.Standalone.Small", "BuildSettings.Android.Small", "BuildSettings.iPhone.Small", "BuildSettings.WebGL.Small" };
        static readonly string[] s_PlatformTabShort = { "Default", "PC", "Android", "iOS", "WebGL" };
        static GUIContent[] s_PlatformTabContents;

        // TextureImporterSettings (serialized field names of UnityEditor.TextureImporterSettings)
        SerializedProperty m_TextureType;
        SerializedProperty m_sRGBTexture;
        SerializedProperty m_AlphaSource;
        SerializedProperty m_AlphaIsTransparency;
        SerializedProperty m_NPOTScale;
        SerializedProperty m_IsReadable;
        SerializedProperty m_StreamingMipmaps;
        SerializedProperty m_StreamingMipmapsPriority;
        SerializedProperty m_EnableMipMap;
        SerializedProperty m_MipMapMode;
        SerializedProperty m_BorderMipMap;
        SerializedProperty m_MipMapsPreserveCoverage;
        SerializedProperty m_AlphaTestReferenceValue;
        SerializedProperty m_FadeOut;
        SerializedProperty m_MipMapFadeDistanceStart;
        SerializedProperty m_MipMapFadeDistanceEnd;
        SerializedProperty m_FilterMode;
        SerializedProperty m_Aniso;
        SerializedProperty m_WrapU;
        SerializedProperty m_WrapV;
        SerializedProperty m_WrapW;
        SerializedProperty m_SpriteMode;
        SerializedProperty m_SpritePixelsToUnits;
        SerializedProperty m_SpriteMeshType;
        SerializedProperty m_SpriteExtrude;
        SerializedProperty m_Alignment;
        SerializedProperty m_SpritePivot;
        SerializedProperty m_SpriteGenerateFallbackPhysicsShape;
        SerializedProperty m_ConvertToNormalMap;
        SerializedProperty m_HeightScale;
        SerializedProperty m_NormalMapFilter;
        SerializedProperty m_FlipGreenChannel;
        SerializedProperty m_SingleChannelComponent;

        // JXL-specific
        SerializedProperty m_ConvertToSRGB;
        SerializedProperty m_UnpremultiplyAlpha;
        SerializedProperty m_HdrReferenceWhiteNits;
        SerializedProperty m_HdrPrimaries;
        SerializedProperty m_HdrClampNegative;

        // Platform settings
        SerializedProperty m_DefaultPlatform;
        SerializedProperty m_PlatformOverrides;

        JxlImageInfo m_Info;
        string m_InfoError;

        bool m_ShowAdvanced;
        bool m_ShowInfo = true;
        int m_PlatformTab;

        public override void OnEnable()
        {
            base.OnEnable();

            m_TextureType = Tex("m_TextureType");
            m_sRGBTexture = Tex("m_sRGBTexture");
            m_AlphaSource = Tex("m_AlphaSource");
            m_AlphaIsTransparency = Tex("m_AlphaIsTransparency");
            m_NPOTScale = Tex("m_NPOTScale");
            m_IsReadable = Tex("m_IsReadable");
            m_StreamingMipmaps = Tex("m_StreamingMipmaps");
            m_StreamingMipmapsPriority = Tex("m_StreamingMipmapsPriority");
            m_EnableMipMap = Tex("m_EnableMipMap");
            m_MipMapMode = Tex("m_MipMapMode");
            m_BorderMipMap = Tex("m_BorderMipMap");
            m_MipMapsPreserveCoverage = Tex("m_MipMapsPreserveCoverage");
            m_AlphaTestReferenceValue = Tex("m_AlphaTestReferenceValue");
            m_FadeOut = Tex("m_FadeOut");
            m_MipMapFadeDistanceStart = Tex("m_MipMapFadeDistanceStart");
            m_MipMapFadeDistanceEnd = Tex("m_MipMapFadeDistanceEnd");
            m_FilterMode = Tex("m_FilterMode");
            m_Aniso = Tex("m_Aniso");
            m_WrapU = Tex("m_WrapU");
            m_WrapV = Tex("m_WrapV");
            m_WrapW = Tex("m_WrapW");
            m_SpriteMode = Tex("m_SpriteMode");
            m_SpritePixelsToUnits = Tex("m_SpritePixelsToUnits");
            m_SpriteMeshType = Tex("m_SpriteMeshType");
            m_SpriteExtrude = Tex("m_SpriteExtrude");
            m_Alignment = Tex("m_Alignment");
            m_SpritePivot = Tex("m_SpritePivot");
            m_SpriteGenerateFallbackPhysicsShape = Tex("m_SpriteGenerateFallbackPhysicsShape");
            m_ConvertToNormalMap = Tex("m_ConvertToNormalMap");
            m_HeightScale = Tex("m_HeightScale");
            m_NormalMapFilter = Tex("m_NormalMapFilter");
            m_FlipGreenChannel = Tex("m_FlipGreenChannel");
            m_SingleChannelComponent = Tex("m_SingleChannelComponent");

            m_ConvertToSRGB = serializedObject.FindProperty(nameof(JxlImporter.convertToSRGB));
            m_UnpremultiplyAlpha = serializedObject.FindProperty(nameof(JxlImporter.unpremultiplyAlpha));
            m_HdrReferenceWhiteNits = serializedObject.FindProperty(nameof(JxlImporter.hdrReferenceWhiteNits));
            m_HdrPrimaries = serializedObject.FindProperty(nameof(JxlImporter.hdrPrimaries));
            m_HdrClampNegative = serializedObject.FindProperty(nameof(JxlImporter.hdrClampNegative));
            m_DefaultPlatform = serializedObject.FindProperty(nameof(JxlImporter.defaultPlatformSettings));
            m_PlatformOverrides = serializedObject.FindProperty(nameof(JxlImporter.platformOverrides));

            ReadImageInfo();
        }

        SerializedProperty Tex(string name) => serializedObject.FindProperty(nameof(JxlImporter.textureSettings) + "." + name);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTextureType();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.LabelField(new GUIContent("Texture Shape", "JPEG XL files always import as 2D textures."), new GUIContent("2D"));

            EditorGUILayout.Space();
            DrawTypeSection();

            EditorGUILayout.Space();
            DrawAdvanced();

            EditorGUILayout.Space();
            DrawWrapMode();
            EnumField<FilterMode>(m_FilterMode, "Filter Mode");
            IntSlider(m_Aniso, "Aniso Level", 0, 16);

            EditorGUILayout.Space();
            DrawPlatformSettings();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawImageInfo();

            ApplyRevertGUI();
        }

        // ------------------------------------------------------------------ texture type

        void DrawTextureType()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = m_TextureType.hasMultipleDifferentValues;
            int value = EditorGUILayout.IntPopup(new GUIContent("Texture Type"), m_TextureType.intValue, s_TypeLabels, s_TypeValues);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                m_TextureType.intValue = value;
                ApplyTypeDefaults((TextureImporterType)value);
            }
        }

        /// <summary>Mirrors <see cref="TextureImporterSettings.ApplyTextureType"/>, which the built-in inspector runs on a type change.</summary>
        void ApplyTypeDefaults(TextureImporterType type)
        {
            switch (type)
            {
                case TextureImporterType.Default:
                    m_sRGBTexture.intValue = 1;
                    m_NPOTScale.intValue = (int)TextureImporterNPOTScale.ToNearest;
                    break;
                case TextureImporterType.NormalMap:
                case TextureImporterType.SingleChannel:
                    m_sRGBTexture.intValue = 0;
                    m_NPOTScale.intValue = (int)TextureImporterNPOTScale.ToNearest;
                    break;
                case TextureImporterType.Sprite:
                    m_SpriteMode.intValue = (int)SpriteImportMode.Single;
                    m_EnableMipMap.intValue = 0;
                    m_NPOTScale.intValue = (int)TextureImporterNPOTScale.None;
                    m_AlphaIsTransparency.intValue = 1;
                    m_WrapU.intValue = m_WrapV.intValue = m_WrapW.intValue = (int)TextureWrapMode.Clamp;
                    break;
            }
        }

        void DrawTypeSection()
        {
            if (m_TextureType.hasMultipleDifferentValues)
                return;

            switch ((TextureImporterType)m_TextureType.intValue)
            {
                case TextureImporterType.Sprite:
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(new GUIContent("Sprite Mode", "JPEG XL files import as a single sprite covering the whole image."), new GUIContent("Single"));
                    using (new EditorGUI.IndentLevelScope())
                    {
                        FloatField(m_SpritePixelsToUnits, "Pixels Per Unit", 0.001f);
                        EnumField<SpriteMeshType>(m_SpriteMeshType, "Mesh Type");
                        IntSlider(m_SpriteExtrude, "Extrude Edges", 0, 32);
                        EnumField<SpriteAlignment>(m_Alignment, "Pivot");
                        if (!m_Alignment.hasMultipleDifferentValues && m_Alignment.intValue == (int)SpriteAlignment.Custom)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                EditorGUILayout.PropertyField(m_SpritePivot, new GUIContent("Custom Pivot"));
                        }
                        Toggle(m_SpriteGenerateFallbackPhysicsShape, "Generate Physics Shape");
                    }
                    break;

                case TextureImporterType.NormalMap:
                    Toggle(m_ConvertToNormalMap, "Create from Grayscale");
                    if (m_ConvertToNormalMap.hasMultipleDifferentValues || m_ConvertToNormalMap.intValue != 0)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            FloatSlider(m_HeightScale, "Bumpiness", 0f, 0.3f);
                            EnumField<TextureImporterNormalFilter>(m_NormalMapFilter, "Filtering");
                        }
                    }
                    Toggle(m_FlipGreenChannel, "Flip Green Channel");
                    break;

                case TextureImporterType.SingleChannel:
                    EnumField<TextureImporterSingleChannelComponent>(m_SingleChannelComponent, "Channel");
                    break;
            }
        }

        // ------------------------------------------------------------------ advanced

        void DrawAdvanced()
        {
            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "Advanced", true);
            if (!m_ShowAdvanced)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                bool colorType = m_TextureType.hasMultipleDifferentValues ||
                                 (TextureImporterType)m_TextureType.intValue == TextureImporterType.Default ||
                                 (TextureImporterType)m_TextureType.intValue == TextureImporterType.Sprite;
                if (colorType)
                    Toggle(m_sRGBTexture, "sRGB (Color Texture)", "Ignored for HDR files, which always import linear.");
                EnumField<TextureImporterAlphaSource>(m_AlphaSource, "Alpha Source");
                Toggle(m_AlphaIsTransparency, "Alpha Is Transparency");
                EnumField<TextureImporterNPOTScale>(m_NPOTScale, "Non-Power of 2");
                Toggle(m_IsReadable, "Read/Write");

                Toggle(m_StreamingMipmaps, "Streaming Mipmaps");
                if (m_StreamingMipmaps.hasMultipleDifferentValues || m_StreamingMipmaps.intValue != 0)
                {
                    using (new EditorGUI.IndentLevelScope())
                        IntField(m_StreamingMipmapsPriority, "Priority");
                }

                Toggle(m_EnableMipMap, "Generate Mipmaps");
                if (m_EnableMipMap.hasMultipleDifferentValues || m_EnableMipMap.intValue != 0)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        Toggle(m_BorderMipMap, "Border Mip Maps");
                        EnumField<TextureImporterMipFilter>(m_MipMapMode, "Mipmap Filtering");
                        Toggle(m_MipMapsPreserveCoverage, "Preserve Coverage");
                        if (m_MipMapsPreserveCoverage.hasMultipleDifferentValues || m_MipMapsPreserveCoverage.intValue != 0)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                FloatSlider(m_AlphaTestReferenceValue, "Alpha Cutoff Value", 0f, 1f);
                        }
                        Toggle(m_FadeOut, "Fadeout Mip Maps");
                        if (m_FadeOut.hasMultipleDifferentValues || m_FadeOut.intValue != 0)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                IntSlider(m_MipMapFadeDistanceStart, "Fade Start", 0, 10);
                                IntSlider(m_MipMapFadeDistanceEnd, "Fade End", 0, 10);
                            }
                        }
                    }
                }

                EditorGUILayout.PropertyField(m_ConvertToSRGB, new GUIContent("Convert Wide Gamut to sRGB", m_ConvertToSRGB.tooltip));
                EditorGUILayout.PropertyField(m_UnpremultiplyAlpha, new GUIContent("Unpremultiply Alpha", m_UnpremultiplyAlpha.tooltip));

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("HDR (PQ / HLG / float files)", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(m_HdrReferenceWhiteNits, new GUIContent("Reference White (nits)", m_HdrReferenceWhiteNits.tooltip));
                EditorGUILayout.PropertyField(m_HdrPrimaries, new GUIContent("Primaries", m_HdrPrimaries.tooltip));
                EditorGUILayout.PropertyField(m_HdrClampNegative, new GUIContent("Clamp Negative", m_HdrClampNegative.tooltip));
            }
        }

        void DrawWrapMode()
        {
            bool mixed = m_WrapU.hasMultipleDifferentValues || m_WrapV.hasMultipleDifferentValues ||
                         m_WrapU.intValue != m_WrapV.intValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = mixed;
            var value = (TextureWrapMode)EditorGUILayout.EnumPopup(new GUIContent("Wrap Mode"), (TextureWrapMode)m_WrapU.intValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                m_WrapU.intValue = m_WrapV.intValue = m_WrapW.intValue = (int)value;
        }

        // ------------------------------------------------------------------ platforms

        void DrawPlatformSettings()
        {
            m_PlatformTab = GUILayout.Toolbar(m_PlatformTab, PlatformTabContents());

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (m_PlatformTab == 0)
                {
                    DrawPlatformFields(m_DefaultPlatform, true);
                    return;
                }

                string platformName = JxlImporter.OverridePlatformNames[m_PlatformTab - 1];
                SerializedProperty entry = FindOverride(platformName);
                if (entry == null)
                {
                    if (serializedObject.isEditingMultipleObjects)
                    {
                        EditorGUILayout.HelpBox($"Some selected assets have no '{platformName}' entry; select them one at a time to add it.", MessageType.Info);
                        return;
                    }
                    bool add = EditorGUILayout.Toggle($"Override for {s_PlatformTabs[m_PlatformTab]}", false);
                    if (add)
                        entry = AddOverride(platformName);
                    else
                        return;
                }

                SerializedProperty overridden = entry.FindPropertyRelative("m_Overridden");
                Toggle(overridden, $"Override for {s_PlatformTabs[m_PlatformTab]}");
                bool enabled = overridden.hasMultipleDifferentValues || overridden.intValue != 0;
                using (new EditorGUI.DisabledScope(!enabled))
                    DrawPlatformFields(enabled ? entry : m_DefaultPlatform, enabled);
            }
        }

        void DrawPlatformFields(SerializedProperty platform, bool editable)
        {
            using (new EditorGUI.DisabledScope(!editable))
            {
                SerializedProperty maxSize = platform.FindPropertyRelative("m_MaxTextureSize");
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = maxSize.hasMultipleDifferentValues;
                int size = EditorGUILayout.IntPopup(new GUIContent("Max Size", "Textures larger than this are downscaled on import."), maxSize.intValue, s_MaxSizeLabels, s_MaxSizeValues);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                    maxSize.intValue = size;

                EnumField<TextureResizeAlgorithm>(platform.FindPropertyRelative("m_ResizeAlgorithm"), "Resize Algorithm");
                EnumField<TextureImporterFormat>(platform.FindPropertyRelative("m_TextureFormat"), "Format");
                EnumField<TextureImporterCompression>(platform.FindPropertyRelative("m_TextureCompression"), "Compression");

                SerializedProperty crunch = platform.FindPropertyRelative("m_CrunchedCompression");
                Toggle(crunch, "Use Crunch Compression");
                if (crunch.hasMultipleDifferentValues || crunch.intValue != 0)
                    IntSlider(platform.FindPropertyRelative("m_CompressionQuality"), "Compressor Quality", 0, 100);
            }
        }

        /// <summary>Build-target icons like the built-in inspector, falling back to short text when an icon is missing.</summary>
        static GUIContent[] PlatformTabContents()
        {
            if (s_PlatformTabContents != null)
                return s_PlatformTabContents;
            s_PlatformTabContents = new GUIContent[s_PlatformTabs.Length];
            for (int i = 0; i < s_PlatformTabs.Length; i++)
            {
                Texture icon = s_PlatformTabIcons[i] != null ? EditorGUIUtility.FindTexture(s_PlatformTabIcons[i]) : null;
                s_PlatformTabContents[i] = icon != null
                    ? new GUIContent(icon, s_PlatformTabs[i])
                    : new GUIContent(s_PlatformTabShort[i], s_PlatformTabs[i]);
            }
            return s_PlatformTabContents;
        }

        SerializedProperty FindOverride(string platformName)
        {
            if (m_PlatformOverrides == null || (serializedObject.isEditingMultipleObjects && !SameOverrideLayout()))
                return null;
            for (int i = 0; i < m_PlatformOverrides.arraySize; i++)
            {
                SerializedProperty element = m_PlatformOverrides.GetArrayElementAtIndex(i);
                SerializedProperty name = element.FindPropertyRelative("m_Name");
                if (name != null && !name.hasMultipleDifferentValues && name.stringValue == platformName)
                    return element;
            }
            return null;
        }

        /// <summary>Multi-editing array elements by index is only safe when every target lists the platforms in the same order.</summary>
        bool SameOverrideLayout()
        {
            JxlImporter first = targets[0] as JxlImporter;
            foreach (var t in targets)
            {
                var other = t as JxlImporter;
                if (first == null || other == null || other.platformOverrides == null || first.platformOverrides == null ||
                    other.platformOverrides.Count != first.platformOverrides.Count)
                    return false;
                for (int i = 0; i < first.platformOverrides.Count; i++)
                {
                    if (first.platformOverrides[i]?.name != other.platformOverrides[i]?.name)
                        return false;
                }
            }
            return true;
        }

        SerializedProperty AddOverride(string platformName)
        {
            int index = m_PlatformOverrides.arraySize;
            m_PlatformOverrides.InsertArrayElementAtIndex(index);
            SerializedProperty element = m_PlatformOverrides.GetArrayElementAtIndex(index);
            var defaults = JxlImporter.CreatePlatformSettings(platformName);
            element.FindPropertyRelative("m_Name").stringValue = platformName;
            element.FindPropertyRelative("m_Overridden").intValue = 1;
            element.FindPropertyRelative("m_MaxTextureSize").intValue = defaults.maxTextureSize;
            element.FindPropertyRelative("m_ResizeAlgorithm").intValue = (int)defaults.resizeAlgorithm;
            element.FindPropertyRelative("m_TextureFormat").intValue = (int)defaults.format;
            element.FindPropertyRelative("m_TextureCompression").intValue = (int)defaults.textureCompression;
            element.FindPropertyRelative("m_CrunchedCompression").intValue = 0;
            element.FindPropertyRelative("m_CompressionQuality").intValue = defaults.compressionQuality;
            return element;
        }

        // ------------------------------------------------------------------ image info

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
                    EditorGUILayout.LabelField("Texture Size", $"{tex.width} x {tex.height}");
                    EditorGUILayout.LabelField("Mip Count", tex.mipmapCount.ToString());
                }

                EditorGUILayout.LabelField("Decoder", JxlDecoder.IsAvailable ? "libjxl " + JxlDecoder.NativeVersion : "Not available");
            }
        }

        // ------------------------------------------------------------------ int-backed field helpers

        static bool ReadBool(SerializedProperty p) => p.propertyType == SerializedPropertyType.Boolean ? p.boolValue : p.intValue != 0;

        static void WriteBool(SerializedProperty p, bool value)
        {
            if (p.propertyType == SerializedPropertyType.Boolean)
                p.boolValue = value;
            else
                p.intValue = value ? 1 : 0;
        }

        static void Toggle(SerializedProperty p, string label, string tooltip = null)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            bool value = EditorGUILayout.Toggle(new GUIContent(label, tooltip), ReadBool(p));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                WriteBool(p, value);
        }

        static void EnumField<T>(SerializedProperty p, string label) where T : Enum
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            var current = (T)Enum.ToObject(typeof(T), p.intValue);
            Enum value = EditorGUILayout.EnumPopup(new GUIContent(label), current);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.intValue = Convert.ToInt32(value);
        }

        static void IntSlider(SerializedProperty p, string label, int min, int max)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            int value = EditorGUILayout.IntSlider(new GUIContent(label), p.intValue, min, max);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.intValue = value;
        }

        static void IntField(SerializedProperty p, string label)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            int value = EditorGUILayout.IntField(new GUIContent(label), p.intValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.intValue = value;
        }

        static void FloatField(SerializedProperty p, string label, float min)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            float value = EditorGUILayout.FloatField(new GUIContent(label), p.floatValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.floatValue = Mathf.Max(min, value);
        }

        static void FloatSlider(SerializedProperty p, string label, float min, float max)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            float value = EditorGUILayout.Slider(new GUIContent(label), p.floatValue, min, max);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.floatValue = value;
        }
    }
}
