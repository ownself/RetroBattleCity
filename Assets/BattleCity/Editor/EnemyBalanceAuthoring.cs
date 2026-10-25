using System;
using UnityEditor;
using UnityEngine;

namespace BattleCity.Editor
{
    /// <summary>Creates missing BC-300 defaults without overwriting tuned assets or M2 scene bindings.</summary>
    public static class EnemyBalanceAuthoring
    {
        public const string BalancePath = Phase0Baseline.Root + "/Data/EnemyBalance.asset";
        private const string DataRoot = Phase0Baseline.Root + "/Data/";

        [MenuItem("Battle City/Create Missing Enemy Balance Assets (BC-300)")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating enemy balance assets.");
            var roster = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(BalancePath);
            if (roster != null)
            {
                roster.ValidateSprites(AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.Root + "/Data/SpriteCatalog.asset"));
                return "BC-300 existing enemy balance retained: " + BalancePath;
            }
            if (AssetDatabase.LoadMainAssetAtPath(BalancePath) != null)
                throw new InvalidOperationException("Enemy balance path contains an unexpected asset type.");

            var entries = new[]
            {
                Create(EnemyKind.Basic, "Basic", "basic", 60, 120, 1, 100),
                Create(EnemyKind.Fast, "Fast", "fast", 90, 120, 1, 200),
                Create(EnemyKind.FastBullet, "FastBullet", "power", 60, 240, 1, 300),
                Create(EnemyKind.Armor, "Armor", "armor", 45, 120, 4, 400)
            };
            roster = ScriptableObject.CreateInstance<EnemyBalanceDefinition>();
            try
            {
                roster.entries = entries;
                roster.ValidateSprites(AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.Root + "/Data/SpriteCatalog.asset"));
                AssetDatabase.CreateAsset(roster, BalancePath);
                AssetDatabase.SaveAssetIfDirty(roster);
            }
            catch
            {
                if (!EditorUtility.IsPersistent(roster)) UnityEngine.Object.DestroyImmediate(roster);
                throw;
            }
            return "BC-300 four uncalibrated enemy definitions created; Stage01 unchanged: " + BalancePath;
        }

        private static EnemyBalanceDefinition.Entry Create(EnemyKind kind, string assetName, string spriteVariant,
            float speed, float bulletSpeed, int hp, int score)
        {
            string path = DataRoot + "Enemy" + assetName + ".asset";
            var tank = AssetDatabase.LoadAssetAtPath<TankDefinition>(path);
            if (tank == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("Enemy definition path contains an unexpected asset type: " + path);
                tank = ScriptableObject.CreateInstance<TankDefinition>();
                tank.definitionId = "enemy." + spriteVariant;
                tank.spritePrefix = "tank.white.enemy." + spriteVariant;
                tank.requiresCalibration = true;
                tank.moveSpeedPixelsPerSecond = speed;
                tank.bulletSpeedPixelsPerSecond = bulletSpeed;
                tank.fireCooldownTicks = 15;
                tank.maxActiveBullets = 1;
                tank.hitPoints = hp;
                tank.score = score;
                tank.Validate();
                AssetDatabase.CreateAsset(tank, path);
                AssetDatabase.SaveAssetIfDirty(tank);
            }
            tank.Validate();
            return new EnemyBalanceDefinition.Entry { kind = kind, tank = tank };
        }
    }
}
