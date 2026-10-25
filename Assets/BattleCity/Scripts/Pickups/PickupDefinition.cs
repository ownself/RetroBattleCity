using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    public enum PickupKind { Clock, Grenade, Helmet, Shovel, Star, Tank }

    /// <summary>Project prototype rules, not verified original-game behavior.</summary>
    [CreateAssetMenu(menuName = "Battle City/Pickup rules")]
    public sealed class PickupDefinition : ScriptableObject
    {
        public uint seed = 303;
        public int collectionScore = 500; // Each accepted pickup, including capped rewards. Project default.
        public int helmetDurationTicks = 600;
        public int clockDurationTicks = 600;
        public int shovelDurationTicks = BaseFortificationState.DefaultDurationTicks;
        // Reward ceiling only: never truncates a session's higher initial life count.
        public int maximumLives = 9;
        // Zero-based successful natural wave indices: fourth, eleventh, eighteenth enemy.
        public int[] carrierSequenceIndices = { 3, 10, 17 };
        public bool requiresCalibration = true;
        public string originalRulesSource = "";

        public void Validate()
        {
            if (seed == 0) throw new InvalidOperationException("Pickup seed must be nonzero.");
            if (collectionScore < 0) throw new InvalidOperationException("Pickup collection score must be nonnegative.");
            if (helmetDurationTicks < 1 || clockDurationTicks < 1 || shovelDurationTicks < 1 || maximumLives < 1)
                throw new InvalidOperationException("Pickup duration and life ceiling must be positive.");
            if (carrierSequenceIndices == null) throw new InvalidOperationException("Missing pickup carrier indices.");
            var seen = new HashSet<int>();
            foreach (int index in carrierSequenceIndices)
                if (index < 0 || !seen.Add(index)) throw new InvalidOperationException("Invalid/duplicate pickup carrier index.");
            if (!requiresCalibration && string.IsNullOrWhiteSpace(originalRulesSource))
                throw new InvalidOperationException("Verified pickup rules require a source.");
        }

        public void ValidateFor(LevelDefinition level)
        {
            Validate();
            if (level == null) throw new ArgumentNullException(nameof(level));
            level.Validate();
            foreach (int index in carrierSequenceIndices)
                if (index >= level.enemySequence.Length) throw new InvalidOperationException("Pickup carrier index exceeds the wave.");
        }

        public void ValidateSprites(SpriteCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (PickupKind kind in Enum.GetValues(typeof(PickupKind))) catalog.Get(SpriteId(kind));
        }

        public static string SpriteId(PickupKind kind)
        {
            if (!Enum.IsDefined(typeof(PickupKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return "pickup." + kind.ToString().ToLowerInvariant();
        }

        public static string CarrierPrefix(TankDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string[] parts = definition.spritePrefix.Split('.');
            if (parts.Length != 4 || parts[0] != "tank" || parts[2] != "enemy" ||
                (parts[3] != "basic" && parts[3] != "fast" && parts[3] != "power" && parts[3] != "armor"))
                throw new InvalidOperationException("Pickup carrier requires an explicit supported enemy Sprite family.");
            return "tank.red.enemy." + parts[3];
        }

        public static void ValidateCarrierSprites(TankDefinition definition, SpriteCatalog catalog)
        {
            string prefix = CarrierPrefix(definition);
            for (int direction = 0; direction < 4; direction++) for (int frame = 0; frame < 2; frame++)
                catalog.Get(prefix + "." + ((CardinalDirection)direction).SpriteKey() + "." + frame);
        }
    }
}
