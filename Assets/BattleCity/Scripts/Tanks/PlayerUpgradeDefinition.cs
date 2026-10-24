using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    [CreateAssetMenu(menuName = "Battle City/Player Upgrade Definition")]
    public sealed class PlayerUpgradeDefinition : ScriptableObject
    {
        public const int MaxLevel = 3;
        [Tooltip("Ordered levels 0..3. Shared TankDefinitions remain the only combat-number model. Project defaults are not verified original rules.")]
        public TankDefinition[] levels = new TankDefinition[MaxLevel + 1];

        public void Validate()
        {
            if (levels == null || levels.Length != MaxLevel + 1)
                throw new InvalidOperationException("Player upgrades require exactly four levels (0..3).");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tank in levels)
            {
                if (tank == null) throw new InvalidOperationException("Missing player upgrade level.");
                tank.Validate();
                if (!ids.Add(tank.definitionId)) throw new InvalidOperationException("Duplicate player level id.");
                if (tank.moveSpeedPixelsPerSecond != levels[0].moveSpeedPixelsPerSecond ||
                    tank.hitPoints != levels[0].hitPoints || tank.score != levels[0].score || tank.damageSpritePrefixes.Length != 0)
                    throw new InvalidOperationException("Player upgrades change weapon and sprite only, not movement, HP, score or damage palettes.");
            }
        }

        public void ValidateFor(TankDefinition player)
        {
            Validate();
            if (player == null) throw new ArgumentNullException(nameof(player));
            player.Validate();
            var zero = levels[0];
            if (player.definitionId != zero.definitionId || player.spritePrefix != zero.spritePrefix ||
                player.moveSpeedPixelsPerSecond != zero.moveSpeedPixelsPerSecond || player.hitPoints != zero.hitPoints ||
                player.score != zero.score || player.bulletSpeedPixelsPerSecond != zero.bulletSpeedPixelsPerSecond ||
                player.fireCooldownTicks != zero.fireCooldownTicks || player.maxActiveBullets != zero.maxActiveBullets ||
                player.canDestroySteel != zero.canDestroySteel || player.damageSpritePrefixes.Length != 0)
                throw new InvalidOperationException("Player definition must match upgrade level zero.");
        }

        public void ValidateSprites(SpriteCatalog sprites)
        {
            Validate();
            foreach (var tank in levels) tank.ValidateSprites(sprites);
        }
    }
}
