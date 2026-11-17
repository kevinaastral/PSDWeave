#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    internal static class RightClickExtension
    {
        [MenuItem("Assets/Psd2UIForm/Auto Sprite Border", priority = 1004)]
        [MenuItem("Assets/GF Tools/2D/Auto Sprite Border", priority = 1004)]
        private static void AutoSpriteSliceBorder()
        {
            foreach (var guid in Selection.assetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (texImporter == null || texImporter.textureType != TextureImporterType.Sprite)
                {
                    continue;
                }

                var rawReadable = texImporter.isReadable;
                if (!rawReadable)
                {
                    texImporter.isReadable = true;
                    texImporter.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null && sprite.texture != null)
                {
                    texImporter.spriteBorder = UGUIParser.CalculateTexture9SliceBorder(sprite.texture);
                }

                texImporter.isReadable = rawReadable;
                texImporter.SaveAndReimport();
            }
        }
    }
}
#endif
