using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Uncalibrated prototype timing/mapping only; the level owns sequence and active limit.</summary>
    [CreateAssetMenu(menuName = "Battle City/Enemy Wave Definition")]
    public sealed class EnemyWaveDefinition : ScriptableObject
    {
        public TankDefinition[] tankDefinitions = Array.Empty<TankDefinition>();
        public uint seed = 1;
        public int preparationTicks = 60;
        // Successful materialization -> next reservation, followed by preparationTicks.
        public int spawnIntervalTicks = 120;
        public bool requiresCalibration = true;

        public void Validate()
        {
            if (seed == 0) throw new InvalidOperationException("Enemy wave seed must be nonzero.");
            if (preparationTicks < 1 || spawnIntervalTicks < 1)
                throw new InvalidOperationException("Enemy wave preparation and interval must be positive ticks.");
            if (tankDefinitions == null) throw new InvalidOperationException("Enemy wave definition mapping is null.");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in tankDefinitions)
            {
                if (definition == null) throw new InvalidOperationException("Enemy wave contains a null tank definition.");
                definition.Validate();
                if (!ids.Add(definition.definitionId))
                    throw new InvalidOperationException("Duplicate enemy wave definition ID: " + definition.definitionId);
            }
        }
    }
}
