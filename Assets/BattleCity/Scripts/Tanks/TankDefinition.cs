using System;
using UnityEngine;

namespace BattleCity
{
    [CreateAssetMenu(menuName = "Battle City/Tank Definition")]
    public sealed class TankDefinition : ScriptableObject
    {
        public string definitionId = "player.level0";
        public string spritePrefix = "tank.yellow.player.level0";
        [Tooltip("Prototype defaults, not verified original-game values. All speeds are logical pixels/second.")]
        public bool requiresCalibration = true;
        public float moveSpeedPixelsPerSecond = 60f;
        public float bulletSpeedPixelsPerSecond = 120f;
        public int fireCooldownTicks = 15;
        public int maxActiveBullets = 1;
        public bool canDestroySteel;
        public int hitPoints = 1;
        public int score = 0;
        [Tooltip("Optional HP damage stages, starting at full health. Empty keeps spritePrefix for all HP; otherwise one prefix per hit point, first equal to spritePrefix.")]
        public string[] damageSpritePrefixes = Array.Empty<string>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(spritePrefix) ||
                float.IsNaN(moveSpeedPixelsPerSecond) || float.IsInfinity(moveSpeedPixelsPerSecond) || moveSpeedPixelsPerSecond <= 0 ||
                float.IsNaN(bulletSpeedPixelsPerSecond) || float.IsInfinity(bulletSpeedPixelsPerSecond) || bulletSpeedPixelsPerSecond <= 0 ||
                fireCooldownTicks < 1 || maxActiveBullets < 1 || hitPoints < 1 || score < 0)
                throw new InvalidOperationException("Invalid tank definition: " + name);
            if (damageSpritePrefixes == null || (damageSpritePrefixes.Length != 0 &&
                (damageSpritePrefixes.Length != hitPoints || damageSpritePrefixes[0] != spritePrefix)))
                throw new InvalidOperationException("Invalid tank damage sprite stages: " + name);
            foreach (var prefix in damageSpritePrefixes)
                if (string.IsNullOrWhiteSpace(prefix)) throw new InvalidOperationException("Empty tank damage sprite prefix: " + name);
        }

        public void ValidateSprites(SpriteCatalog sprites)
        {
            Validate();
            if (sprites == null) throw new ArgumentNullException(nameof(sprites));
            int stages = Math.Max(1, damageSpritePrefixes.Length);
            for (int stage = 0; stage < stages; stage++)
                for (int direction = 0; direction < 4; direction++)
                    for (int frame = 0; frame < 2; frame++)
                        sprites.Get((damageSpritePrefixes.Length == 0 ? spritePrefix : damageSpritePrefixes[stage]) + "." +
                            ((CardinalDirection)direction).SpriteKey() + "." + frame);
        }
    }
}
