using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    public enum EnemyKind { Basic = 0, Fast = 1, FastBullet = 2, Armor = 3 }

    /// <summary>Typed balance roster. Numeric combat data remains solely in TankDefinition.</summary>
    [CreateAssetMenu(menuName = "Battle City/Enemy Balance Definition")]
    public sealed class EnemyBalanceDefinition : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyKind kind;
            public TankDefinition tank;
            [Tooltip("Required when tank.requiresCalibration is false. Cite evidence for the entire numeric parameter set; partial verification must remain marked for calibration.")]
            public string originalValuesSource;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public void Validate()
        {
            if (entries == null || entries.Length != Enum.GetValues(typeof(EnemyKind)).Length)
                throw new InvalidOperationException("Enemy balance requires exactly one entry for each enemy kind.");
            var kinds = new HashSet<EnemyKind>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!Enum.IsDefined(typeof(EnemyKind), entry.kind) || !kinds.Add(entry.kind))
                    throw new InvalidOperationException("Unknown or duplicate enemy kind: " + entry.kind);
                if (entry.tank == null)
                    throw new InvalidOperationException("Missing tank definition for enemy kind: " + entry.kind);
                entry.tank.Validate();
                if (!ids.Add(entry.tank.definitionId))
                    throw new InvalidOperationException("Duplicate enemy balance definition ID: " + entry.tank.definitionId);
                if (!entry.tank.requiresCalibration && string.IsNullOrWhiteSpace(entry.originalValuesSource))
                    throw new InvalidOperationException("Verified original values require a source: " + entry.tank.definitionId);
            }
        }

        public TankDefinition Get(EnemyKind kind)
        {
            Validate();
            foreach (var entry in entries)
                if (entry.kind == kind) return entry.tank;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown enemy kind.");
        }

        public void ValidateSprites(SpriteCatalog sprites)
        {
            Validate();
            if (sprites == null) throw new ArgumentNullException(nameof(sprites));
            sprites.Validate();
            foreach (var entry in entries) entry.tank.ValidateSprites(sprites);
        }
    }
}
