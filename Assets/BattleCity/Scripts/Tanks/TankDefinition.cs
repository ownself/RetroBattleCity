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
        public float bulletSpeedPixelsPerSecond = 180f;
        public int fireCooldownTicks = 15;
        public int maxActiveBullets = 1;
        public int hitPoints = 1;
        public int score = 0;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(spritePrefix) ||
                float.IsNaN(moveSpeedPixelsPerSecond) || float.IsInfinity(moveSpeedPixelsPerSecond) || moveSpeedPixelsPerSecond <= 0 ||
                float.IsNaN(bulletSpeedPixelsPerSecond) || float.IsInfinity(bulletSpeedPixelsPerSecond) || bulletSpeedPixelsPerSecond <= 0 ||
                fireCooldownTicks < 1 || maxActiveBullets < 1 || hitPoints < 1 || score < 0)
                throw new InvalidOperationException("Invalid tank definition: " + name);
        }
    }
}
