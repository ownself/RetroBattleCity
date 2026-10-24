using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCity.Editor
{
    /// <summary>BC-301 opt-in upgrade: changes only the explicit wave mapping/sequence and armor feedback data.</summary>
    public static class EnemyTypesAuthoring
    {
        public static string[] PrototypeSequence() => Enumerable.Range(0, 20)
            .Select(i => new[] { "enemy.basic", "enemy.fast", "enemy.power", "enemy.armor" }[i % 4]).ToArray();

        public static EnemyBalanceDefinition PrepareRoster()
        {
            EnemyBalanceAuthoring.Build();
            var roster = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(EnemyBalanceAuthoring.BalancePath);
            var armor = roster.Get(EnemyKind.Armor);
            // Existing authored stages are tuning, not disposable defaults. Only initialize an empty mapping.
            if (armor.damageSpritePrefixes.Length == 0)
            {
                if (armor.hitPoints != 4 || armor.spritePrefix != "tank.white.enemy.armor")
                    throw new InvalidOperationException("Custom armor requires an explicit damage sprite mapping; defaults were not overwritten.");
                armor.damageSpritePrefixes = new[] { "tank.white.enemy.armor", "tank.green.enemy.armor",
                    "tank.yellow.enemy.armor", "tank.red.enemy.armor" };
                armor.ValidateSprites(AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath));
                EditorUtility.SetDirty(armor);
                AssetDatabase.SaveAssetIfDirty(armor);
            }
            roster.ValidateSprites(AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath));
            return roster;
        }

        [MenuItem("Battle City/Apply Mixed Enemy Types to Stage 01 (BC-301)")]
        public static void ApplyFromMenu() => Debug.Log(ApplyStage01());

        public static string ApplyStage01()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Stage01Authoring.LevelPath);
            var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveDefinition>(Stage01Authoring.WavePath);
            if (level == null || wave == null) throw new InvalidOperationException("Author Stage01 before upgrading its enemy types.");
            if (EditorUtility.IsDirty(level) || EditorUtility.IsDirty(wave))
                throw new InvalidOperationException("Save Stage01 level/wave edits before applying the prototype sequence.");
            var roster = PrepareRoster();
            var definitions = Enum.GetValues(typeof(EnemyKind)).Cast<EnemyKind>().Select(roster.Get).ToArray();
            // Validate the replacement mapping/sequence together before modifying either existing asset.
            var levelCopy = UnityEngine.Object.Instantiate(level);
            var waveCopy = UnityEngine.Object.Instantiate(wave);
            try
            {
                levelCopy.enemySequence = PrototypeSequence();
                waveCopy.tankDefinitions = definitions;
                _ = new EnemyWaveState(levelCopy, waveCopy);
                Stage01Authoring.ValidatePlacement(levelCopy);
                if (!level.enemySequence.SequenceEqual(levelCopy.enemySequence))
                {
                    level.enemySequence = levelCopy.enemySequence;
                    EditorUtility.SetDirty(level);
                    AssetDatabase.SaveAssetIfDirty(level);
                }
                if (!wave.tankDefinitions.SequenceEqual(definitions))
                {
                    wave.tankDefinitions = definitions;
                    EditorUtility.SetDirty(wave);
                    AssetDatabase.SaveAssetIfDirty(wave);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(levelCopy);
                UnityEngine.Object.DestroyImmediate(waveCopy);
            }
            return "BC-301 Stage01 mixed prototype sequence applied (5 each); terrain, scene, timing and cap retained.";
        }
    }
}
