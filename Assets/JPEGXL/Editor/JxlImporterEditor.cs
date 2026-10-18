using System;
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
        static readonly int[] s_TypeValues =
        {
            (int)TextureImporterType.Default,
            (int)TextureImporterType.NormalMap,
            (int)TextureImporterType.Sprite,
            (int)TextureImporterType.SingleChannel,
        };

        static readonly int[] s_SpriteModeValues = { (int)SpriteImportMode.Single, (int)SpriteImportMode.Multiple };

        static readonly int[] s_MaxSizeValues = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };

        /// <summary>Labels are built once; IMGUI runs every repaint.</summary>
        static class Styles
        {
            public static readonly GUIContent TextureType = new GUIContent("Texture Type");
            public static readonly GUIContent TextureShape = new GUIContent("Texture Shape", "JPEG XL files always import as 2D textures.");
            public static readonly GUIContent TextureShapeValue = new GUIContent("2D");
            public static readonly GUIContent FilterMode = new GUIContent("Filter Mode");
            public static readonly GUIContent AnisoLevel = new GUIContent("Aniso Level");
            public static readonly GUIContent WrapMode = new GUIContent("Wrap Mode");

            public static readonly GUIContent SpriteMode = new GUIContent("Sprite Mode", "Single: one sprite covering the whole image. Multiple: several sprites cut out with the Sprite Editor.");
            public static readonly GUIContent PixelsPerUnit = new GUIContent("Pixels Per Unit");
            public static readonly GUIContent MeshType = new GUIContent("Mesh Type");
            public static readonly GUIContent ExtrudeEdges = new GUIContent("Extrude Edges");
            public static readonly GUIContent Pivot = new GUIContent("Pivot");
            public static readonly GUIContent CustomPivot = new GUIContent("Custom Pivot");
            public static readonly GUIContent GeneratePhysicsShape = new GUIContent("Generate Physics Shape");
            public static readonly GUIContent OpenSpriteEditor = new GUIContent("Open Sprite Editor", "Slice the texture into sprites and edit names, pivots and borders.");

            public static readonly GUIContent CreateFromGrayscale = new GUIContent("Create from Grayscale");
            public static readonly GUIContent Bumpiness = new GUIContent("Bumpiness");
            public static readonly GUIContent Filtering = new GUIContent("Filtering");
            public static readonly GUIContent FlipGreenChannel = new GUIContent("Flip Green Channel");
            public static readonly GUIContent Channel = new GUIContent("Channel");

            public static readonly GUIContent Advanced = new GUIContent("Advanced");
            public static readonly GUIContent SrgbTexture = new GUIContent("sRGB (Color Texture)", "Ignored for HDR files, which always import linear.");
            public static readonly GUIContent AlphaSource = new GUIContent("Alpha Source");
            public static readonly GUIContent AlphaIsTransparency = new GUIContent("Alpha Is Transparency");
            public static readonly GUIContent NonPowerOf2 = new GUIContent("Non-Power of 2");
            public static readonly GUIContent ReadWrite = new GUIContent("Read/Write");
            public static readonly GUIContent StreamingMipmaps = new GUIContent("Streaming Mipmaps");
            public static readonly GUIContent Priority = new GUIContent("Priority");
            public static readonly GUIContent GenerateMipmaps = new GUIContent("Generate Mipmaps");
            public static readonly GUIContent BorderMipMaps = new GUIContent("Border Mip Maps");
            public static readonly GUIContent MipmapFiltering = new GUIContent("Mipmap Filtering");
            public static readonly GUIContent PreserveCoverage = new GUIContent("Preserve Coverage");
            public static readonly GUIContent AlphaCutoffValue = new GUIContent("Alpha Cutoff Value");
            public static readonly GUIContent FadeoutMipMaps = new GUIContent("Fadeout Mip Maps");
            public static readonly GUIContent FadeStart = new GUIContent("Fade Start");
            public static readonly GUIContent FadeEnd = new GUIContent("Fade End");
            public static readonly GUIContent HdrHeader = new GUIContent("HDR (PQ / HLG / float files)");

            public static readonly GUIContent MaxSize = new GUIContent("Max Size", "Textures larger than this are downscaled on import.");
            public static readonly GUIContent ResizeAlgorithm = new GUIContent("Resize Algorithm");
            public static readonly GUIContent Format = new GUIContent("Format");
            public static readonly GUIContent Compression = new GUIContent("Compression");
            public static readonly GUIContent UseCrunchCompression = new GUIContent("Use Crunch Compression");
            public static readonly GUIContent CompressorQuality = new GUIContent("Compressor Quality");

            public static readonly GUIContent ImageInfo = new GUIContent("Image info");
            public static readonly GUIContent MultiEditInfo = new GUIContent("Select a single asset to see its image info.");
            public static readonly GUIContent TextureFormat = new GUIContent("Texture Format");
            public static readonly GUIContent TextureSize = new GUIContent("Texture Size");
            public static readonly GUIContent MipCount = new GUIContent("Mip Count");
            public static readonly GUIContent Decoder = new GUIContent("Decoder");
            public static readonly GUIContent DecoderValue = new GUIContent(JxlDecoder.IsAvailable ? "libjxl " + JxlDecoder.NativeVersion : "Not available");

            public static readonly GUIContent[] TypeLabels =
            {
                new GUIContent("Default"),
                new GUIContent("Normal map"),
                new GUIContent("Sprite (2D and UI)"),
                new GUIContent("Single Channel"),
            };

            public static readonly GUIContent[] SpriteModeLabels = { new GUIContent("Single"), new GUIContent("Multiple") };

            public static readonly GUIContent[] MaxSizeLabels = Array.ConvertAll(s_MaxSizeValues, v => new GUIContent(v.ToString()));

            // Index 0 is the default settings, the rest follow JxlImporter.OverridePlatformNames.
            static readonly string[] s_PlatformNames = { "Default", "PC, Mac & Linux Standalone", "Android", "iOS", "WebGL" };
            static readonly string[] s_PlatformShortNames = { "Default", "PC", "Android", "iOS", "WebGL" };
            static readonly string[] s_PlatformIcons = { null, "BuildSettings.Standalone.Small", "BuildSettings.Android.Small", "BuildSettings.iPhone.Small", "BuildSettings.WebGL.Small" };
            static GUIContent[] s_PlatformTabs;

            /// <summary>"Override for ..." toggle label per platform tab (index 0 unused).</summary>
            public static readonly GUIContent[] OverrideToggles = Array.ConvertAll(s_PlatformNames, n => new GUIContent("Override for " + n));

            /// <summary>Help text per platform tab when a multi-selection lacks that override entry (index 0 unused).</summary>
            public static readonly string[] MissingOverride = Array.ConvertAll(s_PlatformNames,
                n => $"Some selected assets have no '{n}' entry; select them one at a time to add it.");

            /// <summary>Build-target icons like the built-in inspector, falling back to short text when an icon is missing.</summary>
            public static GUIContent[] PlatformTabs
            {
                get
                {
                    if (s_PlatformTabs != null)
                        return s_PlatformTabs;
                    s_PlatformTabs = new GUIContent[s_PlatformNames.Length];
                    for (int i = 0; i < s_PlatformNames.Length; i++)
                    {
                        Texture icon = s_PlatformIcons[i] != null ? EditorGUIUtility.FindTexture(s_PlatformIcons[i]) : null;
                        s_PlatformTabs[i] = icon != null
                            ? new GUIContent(icon, s_PlatformNames[i])
                            : new GUIContent(s_PlatformShortNames[i], s_PlatformNames[i]);
                    }
                    return s_PlatformTabs;
                }
            }
        }

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

        // JXL-specific, labelled with the field tooltips
        SerializedProperty m_ConvertToSRGB;
        SerializedProperty m_UnpremultiplyAlpha;
        SerializedProperty m_HdrReferenceWhiteNits;
        SerializedProperty m_HdrPrimaries;
        SerializedProperty m_HdrClampNegative;
        GUIContent m_ConvertToSRGBLabel;
        GUIContent m_UnpremultiplyAlphaLabel;
        GUIContent m_HdrReferenceWhiteNitsLabel;
        GUIContent m_HdrPrimariesLabel;
        GUIContent m_HdrClampNegativeLabel;

        // Platform settings
        SerializedProperty m_DefaultPlatform;
        SerializedProperty m_PlatformOverrides;

        // Source image facts as (label, value) rows, read once in OnEnable; null when several assets are selected.
        GUIContent[] m_InfoLabels;
        GUIContent[] m_InfoValues;

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
            m_ConvertToSRGBLabel = new GUIContent("Convert Wide Gamut to sRGB", m_ConvertToSRGB.tooltip);
            m_UnpremultiplyAlphaLabel = new GUIContent("Unpremultiply Alpha", m_UnpremultiplyAlpha.tooltip);
            m_HdrReferenceWhiteNitsLabel = new GUIContent("Reference White (nits)", m_HdrReferenceWhiteNits.tooltip);
            m_HdrPrimariesLabel = new GUIContent("Primaries", m_HdrPrimaries.tooltip);
            m_HdrClampNegativeLabel = new GUIContent("Clamp Negative", m_HdrClampNegative.tooltip);

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
                EditorGUILayout.LabelField(Styles.TextureShape, Styles.TextureShapeValue);

            EditorGUILayout.Space();
            DrawTypeSection();

            EditorGUILayout.Space();
            DrawAdvanced();

            EditorGUILayout.Space();
            DrawWrapMode();
            EnumField<FilterMode>(m_FilterMode, Styles.FilterMode);
            IntSlider(m_Aniso, Styles.AnisoLevel, 0, 16);

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
            int value = EditorGUILayout.IntPopup(Styles.TextureType, m_TextureType.intValue, Styles.TypeLabels, s_TypeValues);
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
                    DrawSpriteMode();
                    using (new EditorGUI.IndentLevelScope())
                    {
                        FloatField(m_SpritePixelsToUnits, Styles.PixelsPerUnit, 0.001f);
                        EnumField<SpriteMeshType>(m_SpriteMeshType, Styles.MeshType);
                        IntSlider(m_SpriteExtrude, Styles.ExtrudeEdges, 0, 32);
                        // Multiple mode keeps a pivot per sprite (edited in the Sprite Editor), like TextureImporter.
                        if (m_SpriteMode.hasMultipleDifferentValues || m_SpriteMode.intValue != (int)SpriteImportMode.Multiple)
                        {
                            EnumField<SpriteAlignment>(m_Alignment, Styles.Pivot);
                            if (!m_Alignment.hasMultipleDifferentValues && m_Alignment.intValue == (int)SpriteAlignment.Custom)
                            {
                                using (new EditorGUI.IndentLevelScope())
                                    EditorGUILayout.PropertyField(m_SpritePivot, Styles.CustomPivot);
                            }
                        }
                        Toggle(m_SpriteGenerateFallbackPhysicsShape, Styles.GeneratePhysicsShape);
                    }
                    DrawSpriteEditorButton();
                    break;

                case TextureImporterType.NormalMap:
                    Toggle(m_ConvertToNormalMap, Styles.CreateFromGrayscale);
                    if (m_ConvertToNormalMap.hasMultipleDifferentValues || m_ConvertToNormalMap.intValue != 0)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            FloatSlider(m_HeightScale, Styles.Bumpiness, 0f, 0.3f);
                            EnumField<TextureImporterNormalFilter>(m_NormalMapFilter, Styles.Filtering);
                        }
                    }
                    Toggle(m_FlipGreenChannel, Styles.FlipGreenChannel);
                    break;

                case TextureImporterType.SingleChannel:
                    EnumField<TextureImporterSingleChannelComponent>(m_SingleChannelComponent, Styles.Channel);
                    break;
            }
        }

        void DrawSpriteMode()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = m_SpriteMode.hasMultipleDifferentValues;
            // Polygon is not supported and imports as Single, so show it as Single.
            int current = m_SpriteMode.intValue == (int)SpriteImportMode.Multiple ? (int)SpriteImportMode.Multiple : (int)SpriteImportMode.Single;
            int value = EditorGUILayout.IntPopup(Styles.SpriteMode, current, Styles.SpriteModeLabels, s_SpriteModeValues);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                m_SpriteMode.intValue = value;
        }

        void DrawSpriteEditorButton()
        {
#if JXL_2D_SPRITE
            using (new EditorGUI.DisabledScope(targets.Length != 1))
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                bool open = GUILayout.Button(Styles.OpenSpriteEditor, GUILayout.Width(150));
                GUILayout.EndHorizontal();
                if (open)
                {
                    string path = ((AssetImporter)target).assetPath;
                    if (HasModified())
                    {
                        int choice = EditorUtility.DisplayDialogComplex("Unapplied import settings",
                            $"Unapplied import settings for '{path}'.\nThe Sprite Editor works on the applied settings.", "Apply", "Cancel", "Revert");
                        if (choice == 1)
                            GUIUtility.ExitGUI();
                        if (choice == 0)
                            SaveChanges();
                        else
                            DiscardChanges();
                    }
                    EditorApplication.delayCall += () => OpenSpriteEditor(path);
                    GUIUtility.ExitGUI();
                }
            }
#else
            EditorGUILayout.HelpBox("Install the 2D Sprite package (com.unity.2d.sprite) to slice this texture in the Sprite Editor.", MessageType.Info);
#endif
        }

#if JXL_2D_SPRITE
        /// <summary>
        /// The Sprite Editor window class is internal to com.unity.2d.sprite; its menu item opens it on the current selection,
        /// and it finds <see cref="JxlImporter"/> as the data provider through the asset's importer.
        /// </summary>
        static void OpenSpriteEditor(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorApplication.ExecuteMenuItem("Window/2D/Sprite Editor");
        }
#endif

        // ------------------------------------------------------------------ advanced

        void DrawAdvanced()
        {
            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, Styles.Advanced, true);
            if (!m_ShowAdvanced)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                bool colorType = m_TextureType.hasMultipleDifferentValues ||
                                 (TextureImporterType)m_TextureType.intValue == TextureImporterType.Default ||
                                 (TextureImporterType)m_TextureType.intValue == TextureImporterType.Sprite;
                if (colorType)
                    Toggle(m_sRGBTexture, Styles.SrgbTexture);
                EnumField<TextureImporterAlphaSource>(m_AlphaSource, Styles.AlphaSource);
                Toggle(m_AlphaIsTransparency, Styles.AlphaIsTransparency);
                EnumField<TextureImporterNPOTScale>(m_NPOTScale, Styles.NonPowerOf2);
                Toggle(m_IsReadable, Styles.ReadWrite);

                Toggle(m_StreamingMipmaps, Styles.StreamingMipmaps);
                if (m_StreamingMipmaps.hasMultipleDifferentValues || m_StreamingMipmaps.intValue != 0)
                {
                    using (new EditorGUI.IndentLevelScope())
                        IntField(m_StreamingMipmapsPriority, Styles.Priority);
                }

                Toggle(m_EnableMipMap, Styles.GenerateMipmaps);
                if (m_EnableMipMap.hasMultipleDifferentValues || m_EnableMipMap.intValue != 0)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        Toggle(m_BorderMipMap, Styles.BorderMipMaps);
                        EnumField<TextureImporterMipFilter>(m_MipMapMode, Styles.MipmapFiltering);
                        Toggle(m_MipMapsPreserveCoverage, Styles.PreserveCoverage);
                        if (m_MipMapsPreserveCoverage.hasMultipleDifferentValues || m_MipMapsPreserveCoverage.intValue != 0)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                FloatSlider(m_AlphaTestReferenceValue, Styles.AlphaCutoffValue, 0f, 1f);
                        }
                        Toggle(m_FadeOut, Styles.FadeoutMipMaps);
                        if (m_FadeOut.hasMultipleDifferentValues || m_FadeOut.intValue != 0)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                IntSlider(m_MipMapFadeDistanceStart, Styles.FadeStart, 0, 10);
                                IntSlider(m_MipMapFadeDistanceEnd, Styles.FadeEnd, 0, 10);
                            }
                        }
                    }
                }

                EditorGUILayout.PropertyField(m_ConvertToSRGB, m_ConvertToSRGBLabel);
                EditorGUILayout.PropertyField(m_UnpremultiplyAlpha, m_UnpremultiplyAlphaLabel);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(Styles.HdrHeader, EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(m_HdrReferenceWhiteNits, m_HdrReferenceWhiteNitsLabel);
                EditorGUILayout.PropertyField(m_HdrPrimaries, m_HdrPrimariesLabel);
                EditorGUILayout.PropertyField(m_HdrClampNegative, m_HdrClampNegativeLabel);
            }
        }

        void DrawWrapMode()
        {
            bool mixed = m_WrapU.hasMultipleDifferentValues || m_WrapV.hasMultipleDifferentValues ||
                         m_WrapU.intValue != m_WrapV.intValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = mixed;
            var value = (TextureWrapMode)EditorGUILayout.EnumPopup(Styles.WrapMode, (TextureWrapMode)m_WrapU.intValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                m_WrapU.intValue = m_WrapV.intValue = m_WrapW.intValue = (int)value;
        }

        // ------------------------------------------------------------------ platforms

        void DrawPlatformSettings()
        {
            m_PlatformTab = GUILayout.Toolbar(m_PlatformTab, Styles.PlatformTabs);

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
                        EditorGUILayout.HelpBox(Styles.MissingOverride[m_PlatformTab], MessageType.Info);
                        return;
                    }
                    if (!EditorGUILayout.Toggle(Styles.OverrideToggles[m_PlatformTab], false))
                        return;
                    entry = AddOverride(platformName);
                }

                SerializedProperty overridden = entry.FindPropertyRelative("m_Overridden");
                Toggle(overridden, Styles.OverrideToggles[m_PlatformTab]);
                bool enabled = overridden.hasMultipleDifferentValues || overridden.intValue != 0;
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
                int size = EditorGUILayout.IntPopup(Styles.MaxSize, maxSize.intValue, Styles.MaxSizeLabels, s_MaxSizeValues);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                    maxSize.intValue = size;

                EnumField<TextureResizeAlgorithm>(platform.FindPropertyRelative("m_ResizeAlgorithm"), Styles.ResizeAlgorithm);
                EnumField<TextureImporterFormat>(platform.FindPropertyRelative("m_TextureFormat"), Styles.Format);
                EnumField<TextureImporterCompression>(platform.FindPropertyRelative("m_TextureCompression"), Styles.Compression);

                SerializedProperty crunch = platform.FindPropertyRelative("m_CrunchedCompression");
                Toggle(crunch, Styles.UseCrunchCompression);
                if (crunch.hasMultipleDifferentValues || crunch.intValue != 0)
                    IntSlider(platform.FindPropertyRelative("m_CompressionQuality"), Styles.CompressorQuality, 0, 100);
            }
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
            var first = targets[0] as JxlImporter;
            if (first == null || first.platformOverrides == null)
                return false;
            foreach (var t in targets)
            {
                var other = t as JxlImporter;
                if (other == null || other.platformOverrides == null || other.platformOverrides.Count != first.platformOverrides.Count)
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
            element.FindPropertyRelative("m_CrunchedCompression").intValue = defaults.crunchedCompression ? 1 : 0;
            element.FindPropertyRelative("m_CompressionQuality").intValue = defaults.compressionQuality;
            return element;
        }

        // ------------------------------------------------------------------ image info

        /// <summary>Reads the file header through libjxl (no pixel decode) and formats the rows shown under Image info.</summary>
        void ReadImageInfo()
        {
            m_InfoLabels = m_InfoValues = null;
            if (targets.Length != 1 || !(target is JxlImporter importer))
                return;

            if (!JxlImporter.TryReadInfo(importer.assetPath, out JxlImageInfo info, out string error))
            {
                m_InfoLabels = new[] { new GUIContent(error) };
                m_InfoValues = new[] { GUIContent.none };
                return;
            }

            string note = (info.HasAnimation ? "Animated (first frame only). " : "") +
                          (info.NumExtraChannels > 0 ? $"{info.NumExtraChannels} extra channel(s) ignored." : "");
            string[] rows =
            {
                "Size", $"{info.Width} x {info.Height}",
                "Bits Per Sample", info.ExponentBitsPerSample > 0 ? $"{info.BitsPerSample} (float)" : info.BitsPerSample.ToString(),
                "Transfer", info.Transfer.ToString(),
                "Primaries", info.Primaries + (info.IsWideGamut ? " (wide gamut)" : ""),
                "Alpha", info.HasAlpha ? (info.AlphaPremultiplied ? "Yes (premultiplied)" : "Yes") : "No",
                "HDR", info.IsHdr ? "Yes (imported linear)" : "No",
                "Note", note,
            };
            int count = rows.Length / 2 - (note.Length > 0 ? 0 : 1);
            m_InfoLabels = new GUIContent[count];
            m_InfoValues = new GUIContent[count];
            for (int i = 0; i < count; i++)
            {
                m_InfoLabels[i] = new GUIContent(rows[2 * i]);
                m_InfoValues[i] = new GUIContent(rows[2 * i + 1]);
            }
        }

        void DrawImageInfo()
        {
            m_ShowInfo = EditorGUILayout.Foldout(m_ShowInfo, Styles.ImageInfo, true);
            if (!m_ShowInfo)
                return;

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
            {
                if (m_InfoLabels == null)
                {
                    EditorGUILayout.LabelField(Styles.MultiEditInfo);
                    return;
                }

                for (int i = 0; i < m_InfoLabels.Length; i++)
                    EditorGUILayout.LabelField(m_InfoLabels[i], m_InfoValues[i]);

                if (assetTarget is Texture2D tex)
                {
                    EditorGUILayout.LabelField(Styles.TextureFormat, EditorGUIUtility.TrTempContent(tex.format.ToString()));
                    EditorGUILayout.LabelField(Styles.TextureSize, EditorGUIUtility.TrTempContent($"{tex.width} x {tex.height}"));
                    EditorGUILayout.LabelField(Styles.MipCount, EditorGUIUtility.TrTempContent(tex.mipmapCount.ToString()));
                }

                EditorGUILayout.LabelField(Styles.Decoder, Styles.DecoderValue);
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

        static void Toggle(SerializedProperty p, GUIContent label)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            bool value = EditorGUILayout.Toggle(label, ReadBool(p));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                WriteBool(p, value);
        }

        static void EnumField<T>(SerializedProperty p, GUIContent label) where T : Enum
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            var current = (T)Enum.ToObject(typeof(T), p.intValue);
            Enum value = EditorGUILayout.EnumPopup(label, current);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.intValue = Convert.ToInt32(value);
        }

        static void IntSlider(SerializedProperty p, GUIContent label, int min, int max)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            int value = EditorGUILayout.IntSlider(label, p.intValue, min, max);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.intValue = value;
        }

        static void IntField(SerializedProperty p, GUIContent label)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            int value = EditorGUILayout.IntField(label, p.intValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.intValue = value;
        }

        static void FloatField(SerializedProperty p, GUIContent label, float min)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            float value = EditorGUILayout.FloatField(label, p.floatValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.floatValue = Mathf.Max(min, value);
        }

        static void FloatSlider(SerializedProperty p, GUIContent label, float min, float max)
        {
            if (p == null)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            float value = EditorGUILayout.Slider(label, p.floatValue, min, max);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                p.floatValue = value;
        }
    }
}
