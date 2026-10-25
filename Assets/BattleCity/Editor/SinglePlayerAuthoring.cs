using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCity.Editor
{
    public static class SinglePlayerAuthoring
    {
        public const string ScenePath = "Assets/BattleCity/Scenes/SinglePlayer.unity";
        public const string TitlePath = "Assets/BattleCity/Art/Title.png";
        private const string SourcePath = "Assets/Sprites/NES - Battle City (JPN) - Miscellaneous - Miscellaneous.png";

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            PrepareTitle();
            var source = EditorSceneManager.OpenScene(Stage01Authoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("Cannot copy Stage01.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.name = "Single player - title and stage flow";
            var root = new GameObject("Single player screens", typeof(RectTransform), typeof(Canvas), typeof(SinglePlayerFlow));
            var rect = root.GetComponent<RectTransform>();
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(256, 240);
            rect.position = PixelGrid.ToWorld(-PixelGrid.MapScreenOffset);
            rect.localScale = Vector3.one / PixelGrid.PixelsPerUnit;
            var flow = root.GetComponent<SinglePlayerFlow>();
            flow.screenCanvas = root.GetComponent<Canvas>();
            flow.screenCanvas.renderMode = RenderMode.WorldSpace;
            flow.screenCanvas.sortingLayerName = "HUD";
            flow.screenCanvas.sortingOrder = 10;
            var background = Box(rect, "Opaque screen", 0, 0, 256, 240).gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Color.black; background.raycastTarget = false;
            flow.titleArt = Box(rect, "Original title - derived crop", 32, 148, 192, 72).gameObject.AddComponent<UnityEngine.UI.Image>();
            flow.titleArt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitlePath);
            flow.titleArt.raycastTarget = false;
            flow.heading = Caption(rect, "Heading", 32, 204, 192, 10, 2);
            flow.lines = new PixelHudText[8];
            for (int i = 0; i < flow.lines.Length; i++) flow.lines[i] = Caption(rect, "Row " + i, 24, 172 - i * 20, 224, 5, 1);
            controller.singlePlayerFlow = flow;
            flow.Render(null, controller.level.stageNumber);
            controller.ValidateBindings();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save single player scene.");
            return ScenePath;
        }

        private static void PrepareTitle()
        {
            // Source top-left rect (24, 40, 192, 72), opaque black retained intentionally.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var crop = new Texture2D(192, 72, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(SourcePath))) throw new InvalidOperationException("Cannot read title reference.");
                crop.SetPixels(source.GetPixels(24, source.height - 112, 192, 72));
                crop.Apply();
                File.WriteAllBytes(TitlePath, crop.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(crop); }
            AssetDatabase.ImportAsset(TitlePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TitlePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelGrid.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static RectTransform Box(Transform parent, string name, int x, int y, int w, int h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static PixelHudText Caption(Transform parent, string name, int x, int y, int w, int h, int scale)
        {
            var rect = Box(parent, name, x, y, w / scale, h / scale);
            rect.localScale = Vector3.one * scale;
            var text = rect.gameObject.AddComponent<PixelHudText>();
            text.color = Color.white; text.raycastTarget = false;
            return text;
        }
    }
}
