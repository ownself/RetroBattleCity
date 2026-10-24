using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCity.Editor
{
    public static class PlayerUpgradeAuthoring
    {
        public const string DefinitionPath = Phase0Baseline.Root + "/Data/PlayerUpgrades.asset";
        public const string BasePath = Phase0Baseline.Root + "/Data/PlayerPrototype.asset";
        public static string LevelPath(int level) => Phase0Baseline.Root + "/Data/PlayerLevel" + level + ".asset";

        /// <summary>Create missing data only. Existing tuning and the M0 base player are never overwritten.</summary>
        public static PlayerUpgradeDefinition Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var player = AssetDatabase.LoadAssetAtPath<TankDefinition>(BasePath);
            if (player == null) throw new InvalidOperationException("Missing base player definition.");
            player.Validate();
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            var definition = AssetDatabase.LoadAssetAtPath<PlayerUpgradeDefinition>(DefinitionPath);
            if (definition == null)
            {
                var levels = new TankDefinition[PlayerUpgradeDefinition.MaxLevel + 1];
                levels[0] = player;
                for (int i = 1; i < levels.Length; i++)
                {
                    levels[i] = AssetDatabase.LoadAssetAtPath<TankDefinition>(LevelPath(i));
                    if (levels[i] != null) continue;
                    var tank = ScriptableObject.CreateInstance<TankDefinition>();
                    tank.definitionId = "player.level" + i;
                    tank.spritePrefix = "tank.yellow.player.level" + i;
                    tank.moveSpeedPixelsPerSecond = player.moveSpeedPixelsPerSecond;
                    tank.hitPoints = player.hitPoints; tank.score = player.score;
                    // Project defaults only: fast bullets after one upgrade; two live shots after two.
                    tank.bulletSpeedPixelsPerSecond = 300;
                    tank.fireCooldownTicks = 15;
                    tank.maxActiveBullets = i >= 2 ? 2 : 1;
                    tank.canDestroySteel = i == 3;
                    tank.requiresCalibration = true;
                    tank.ValidateSprites(catalog);
                    AssetDatabase.CreateAsset(tank, LevelPath(i));
                    AssetDatabase.SaveAssetIfDirty(tank);
                    levels[i] = tank;
                }
                definition = ScriptableObject.CreateInstance<PlayerUpgradeDefinition>();
                definition.levels = levels;
                definition.ValidateFor(player); definition.ValidateSprites(catalog);
                AssetDatabase.CreateAsset(definition, DefinitionPath);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
            if (EditorUtility.IsDirty(definition) || definition.levels.Any(t => t != null && EditorUtility.IsDirty(t)))
                throw new InvalidOperationException("Save player upgrade asset edits before binding.");
            definition.ValidateFor(player); definition.ValidateSprites(catalog);
            return definition;
        }

        [MenuItem("Battle City/Apply Player Upgrades to Stage 01 (BC-302)")]
        public static void ApplyFromMenu() => Debug.Log(ApplyStage01());

        public static string ApplyStage01()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before binding player upgrades.");
            Prepare();
            var scene = EditorSceneManager.OpenScene(Stage01Authoring.ScenePath, OpenSceneMode.Single);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            // A scene transition can unload assets held only by local managed references.
            var upgrades = AssetDatabase.LoadAssetAtPath<PlayerUpgradeDefinition>(DefinitionPath);
            upgrades.ValidateFor(controller.playerDefinition);
            if (controller.playerUpgradeDefinition != upgrades)
            {
                controller.playerUpgradeDefinition = upgrades;
                controller.ValidateBindings();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save player upgrade binding.");
            }
            return "BC-302 Stage01 player upgrade binding applied; existing base player, map, wave, camera and HUD retained.";
        }
    }
}
