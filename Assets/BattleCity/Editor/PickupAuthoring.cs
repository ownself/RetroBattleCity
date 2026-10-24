using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCity.Editor
{
    public static class PickupAuthoring
    {
        public const string DefinitionPath = Phase0Baseline.Root + "/Data/Pickups.asset";

        public static PickupDefinition Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PickupDefinition>();
                definition.ValidateSprites(AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath));
                AssetDatabase.CreateAsset(definition, DefinitionPath);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
            if (EditorUtility.IsDirty(definition)) throw new InvalidOperationException("Save pickup rule edits before binding.");
            definition.Validate();
            definition.ValidateSprites(AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath));
            return definition;
        }

        [MenuItem("Battle City/Apply Pickup Framework to Stage 01 (BC-303)")]
        public static void ApplyFromMenu() => Debug.Log(ApplyStage01());

        public static string ApplyStage01()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before pickup binding.");
            Prepare();
            var scene = EditorSceneManager.OpenScene(Stage01Authoring.ScenePath, OpenSceneMode.Single);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>(DefinitionPath);
            definition.ValidateFor(controller.level);
            var previous = controller.pickupDefinition;
            controller.pickupDefinition = definition;
            try { controller.ValidateBindings(); }
            catch { controller.pickupDefinition = previous; throw; }
            if (previous != definition)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save pickup binding.");
            }
            return "BC-303 Stage01 pickup framework bound; existing tuning, map, wave, camera/HUD and player retained.";
        }
    }
}
