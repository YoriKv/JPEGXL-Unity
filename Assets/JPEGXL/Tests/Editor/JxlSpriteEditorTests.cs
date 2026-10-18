#if JXL_2D_SPRITE
using System;
using System.Linq;
using JpegXL.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static JpegXL.Tests.JxlImporterTests;

namespace JpegXL.Tests
{
    /// <summary>
    /// Phase 5: Sprite Editor support. The data provider is fetched the way the Sprite Editor window does it
    /// (<see cref="SpriteDataProviderFactories.GetSpriteEditorDataProviderFromObject"/> on the main asset).
    /// </summary>
    public class JxlSpriteEditorTests
    {
        static ISpriteEditorDataProvider GetProvider(string path)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(AssetDatabase.LoadMainAssetAtPath(path));
            Assert.IsNotNull(provider, "No ISpriteEditorDataProvider for the asset: the Sprite Editor would reject it.");
            Assert.IsInstanceOf<JxlImporter>(provider);
            provider.InitSpriteEditorDataProvider();
            return provider;
        }

        static void MakeSprite(JxlImporter importer, SpriteImportMode mode)
        {
            importer.textureSettings.textureType = TextureImporterType.Sprite;
            importer.textureSettings.spriteMode = (int)mode;
            importer.textureSettings.mipmapEnabled = false;
            importer.textureSettings.npotScale = TextureImporterNPOTScale.None;
        }

        static readonly GUID LeftId = new GUID("0123456789abcdef0123456789abcdef");
        static readonly GUID RightId = new GUID("fedcba9876543210fedcba9876543210");

        /// <summary>Two sprites set through the provider, as the Sprite Editor does: SetSpriteRects, Apply, then the window saves and reimports.</summary>
        static void SliceSunInTwo()
        {
            Reimport(Sun, importer => MakeSprite(importer, SpriteImportMode.Multiple));
            ISpriteEditorDataProvider provider = GetProvider(Sun);
            Assert.AreEqual(SpriteImportMode.Multiple, provider.spriteImportMode);
            Assert.AreEqual(0, provider.GetSpriteRects().Length, "fresh Multiple mode has no sprites");

            provider.SetSpriteRects(new[]
            {
                new SpriteRect { name = "sun_left", rect = new Rect(0, 0, 256, 512), alignment = SpriteAlignment.BottomLeft, pivot = Vector2.zero, spriteID = LeftId },
                new SpriteRect { name = "sun_right", rect = new Rect(256, 128, 256, 384), alignment = SpriteAlignment.Custom, pivot = new Vector2(0.25f, 0.75f), border = new Vector4(1, 2, 3, 4), spriteID = RightId },
            });
            provider.Apply();
            var importer = (AssetImporter)provider.targetObject;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        [Test]
        public void Multiple_TwoRectsThroughProvider_YieldTwoSprites()
        {
            try
            {
                SliceSunInTwo();

                var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(Sun).OfType<Sprite>().OrderBy(s => s.name).ToArray();
                Assert.AreEqual(2, sprites.Length, "sprite sub-assets: " + string.Join(", ", sprites.Select(s => s.name)));
                Assert.IsInstanceOf<Texture2D>(AssetDatabase.LoadMainAssetAtPath(Sun));

                Sprite left = sprites[0], right = sprites[1];
                Assert.AreEqual("sun_left", left.name);
                Assert.AreEqual("sun_right", right.name);
                Assert.AreEqual(new Rect(0, 0, 256, 512), left.rect);
                Assert.AreEqual(new Rect(256, 128, 256, 384), right.rect);
                Assert.AreEqual(Vector2.zero, left.pivot, "left pivot (pixels)");
                Assert.AreEqual(new Vector2(64, 288), right.pivot, "right pivot in pixels = (0.25, 0.75) * size");
                Assert.AreEqual(new Vector4(1, 2, 3, 4), right.border);
                Assert.AreSame(AssetDatabase.LoadMainAssetAtPath(Sun), left.texture);

                // Serialized data round trip.
                JxlImporter importer = GetImporter(Sun);
                Assert.AreEqual(2, importer.spriteSheet.Count);
                Assert.AreEqual(LeftId.ToString(), importer.spriteSheet[0].spriteID);

                // A fresh provider reads the same rects back, IDs included.
                SpriteRect[] rects = GetProvider(Sun).GetSpriteRects();
                Assert.AreEqual(2, rects.Length);
                CollectionAssert.AreEquivalent(new[] { LeftId, RightId }, rects.Select(r => r.spriteID).ToArray());
                Assert.AreEqual(new Rect(256, 128, 256, 384), rects.First(r => r.spriteID == RightId).rect);
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        [Test]
        public void Multiple_RenameKeepsSubAssetIdentity()
        {
            try
            {
                SliceSunInTwo();
                Sprite before = AssetDatabase.LoadAllAssetRepresentationsAtPath(Sun).OfType<Sprite>().First(s => s.name == "sun_right");
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(before, out string _, out long fileIdBefore));

                ISpriteEditorDataProvider provider = GetProvider(Sun);
                SpriteRect[] rects = provider.GetSpriteRects();
                rects.First(r => r.spriteID == RightId).name = "sun_right_renamed";
                provider.SetSpriteRects(rects);
                provider.Apply();
                var importer = (AssetImporter)provider.targetObject;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                Sprite after = AssetDatabase.LoadAllAssetRepresentationsAtPath(Sun).OfType<Sprite>().FirstOrDefault(s => s.name == "sun_right_renamed");
                Assert.IsNotNull(after, "renamed sprite");
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(after, out string _, out long fileIdAfter));
                Assert.AreEqual(fileIdBefore, fileIdAfter, "The sub-asset identifier is the sprite ID, so references survive a rename.");
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        [Test]
        public void Single_WithStoredSheet_StillYieldsOneSprite_WithProviderPivotAndBorder()
        {
            try
            {
                SliceSunInTwo();
                Reimport(Sun, importer => importer.textureSettings.spriteMode = (int)SpriteImportMode.Single);

                ISpriteEditorDataProvider provider = GetProvider(Sun);
                Assert.AreEqual(SpriteImportMode.Single, provider.spriteImportMode);
                SpriteRect[] rects = provider.GetSpriteRects();
                Assert.AreEqual(1, rects.Length);
                Assert.AreEqual(new Rect(0, 0, 512, 512), rects[0].rect);

                rects[0].alignment = SpriteAlignment.Custom;
                rects[0].pivot = new Vector2(0.1f, 0.2f);
                rects[0].border = new Vector4(5, 6, 7, 8);
                provider.SetSpriteRects(rects);
                provider.Apply();
                var importerObj = (AssetImporter)provider.targetObject;
                EditorUtility.SetDirty(importerObj);
                importerObj.SaveAndReimport();

                var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(Sun).OfType<Sprite>().ToArray();
                Assert.AreEqual(1, sprites.Length, "Single mode ignores the stored sheet");
                Assert.AreEqual(new Rect(0, 0, 512, 512), sprites[0].rect);
                Assert.AreEqual(new Vector2(51.2f, 102.4f).x, sprites[0].pivot.x, 0.01f);
                Assert.AreEqual(new Vector2(51.2f, 102.4f).y, sprites[0].pivot.y, 0.01f);
                Assert.AreEqual(new Vector4(5, 6, 7, 8), sprites[0].border);
                Assert.AreEqual(2, GetImporter(Sun).spriteSheet.Count, "the Multiple-mode sheet is kept, like TextureImporter");
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        [Test]
        public void DefaultType_ProviderReportsNone()
        {
            ISpriteEditorDataProvider provider = GetProvider(Sun);
            Assert.AreEqual(SpriteImportMode.None, provider.spriteImportMode);
            Assert.IsTrue(provider.HasDataProvider(typeof(ITextureDataProvider)));
            Assert.IsFalse(provider.HasDataProvider(typeof(ISpriteOutlineDataProvider)), "outline editing is out of scope");
            Assert.IsFalse(provider.HasDataProvider(typeof(ISecondaryTextureDataProvider)), "secondary textures are out of scope");
            Assert.IsNotNull(provider.GetDataProvider<ISpriteOutlineDataProvider>(), "the frame module needs a non-null outline provider");
            Assert.AreEqual(0, provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>().GetOutlines(LeftId).Count);
        }

        [Test]
        public void TextureProvider_ReadableTexture_IsDecodedAtSourceSize()
        {
            try
            {
                // Max size 128: the imported texture is smaller than the source, the readable copy is not.
                Reimport(Sun, importer =>
                {
                    MakeSprite(importer, SpriteImportMode.Multiple);
                    importer.defaultPlatformSettings.maxTextureSize = 128;
                });
                var textureProvider = GetProvider(Sun).GetDataProvider<ITextureDataProvider>();
                Assert.IsNotNull(textureProvider);

                textureProvider.GetTextureActualWidthAndHeight(out int width, out int height);
                Assert.AreEqual(512, width);
                Assert.AreEqual(512, height);
                Assert.AreSame(AssetDatabase.LoadMainAssetAtPath(Sun), textureProvider.texture);
                Assert.AreEqual(128, textureProvider.texture.width);

                Texture2D readable = textureProvider.GetReadableTexture2D();
                Assert.IsNotNull(readable);
                Assert.IsTrue(readable.isReadable);
                Assert.AreEqual(TextureFormat.RGBA32, readable.format);
                Assert.AreEqual(512, readable.width);
                Assert.AreEqual(512, readable.height);

                Color32[] px = readable.GetPixels32();
                foreach (int[] p in SunReferencePixels)
                {
                    Color32 c = px[(511 - p[1]) * 512 + p[0]];
                    Assert.LessOrEqual(Math.Abs(c.r - p[2]), 3, $"R at png ({p[0]},{p[1]}) got {c}");
                    Assert.LessOrEqual(Math.Abs(c.g - p[3]), 3, $"G at png ({p[0]},{p[1]}) got {c}");
                    Assert.LessOrEqual(Math.Abs(c.b - p[4]), 3, $"B at png ({p[0]},{p[1]}) got {c}");
                }
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }

        [Test]
        public void Multiple_MaxSizeDownscale_ScalesSpriteRects()
        {
            try
            {
                SliceSunInTwo();
                Reimport(Sun, importer => importer.defaultPlatformSettings.maxTextureSize = 256);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sun);
                Assert.AreEqual(256, tex.width);
                var right = AssetDatabase.LoadAllAssetRepresentationsAtPath(Sun).OfType<Sprite>().First(s => s.name == "sun_right");
                // Source-space rect (256, 128, 256, 384) at half scale.
                Assert.AreEqual(new Rect(128, 64, 128, 192), right.rect);
            }
            finally
            {
                RestoreDefaults(Sun);
            }
        }
    }
}
#endif
