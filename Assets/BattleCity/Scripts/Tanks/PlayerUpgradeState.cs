namespace BattleCity
{
    /// <summary>Session-owned progression. No mutable asset references survive construction.</summary>
    public sealed class PlayerUpgradeState
    {
        private readonly WeaponParameters[] weapons = new WeaponParameters[PlayerUpgradeDefinition.MaxLevel + 1];
        private readonly string[] spritePrefixes = new string[PlayerUpgradeDefinition.MaxLevel + 1];
        public int Level { get; private set; }
        public int MaxLevel => PlayerUpgradeDefinition.MaxLevel;
        public string SpritePrefix => spritePrefixes[Level];
        internal PlayerUpgradeState(PlayerUpgradeDefinition definition, TankDefinition player)
        {
            definition.ValidateFor(player);
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i] = new WeaponParameters(definition.levels[i]);
                spritePrefixes[i] = definition.levels[i].spritePrefix;
            }
        }
        internal bool TryUpgrade(WeaponState weapon)
        {
            if (Level == MaxLevel) return false;
            Level++;
            weapon.ApplyParameters(weapons[Level]);
            return true;
        }
        internal void ResetOnDeath(WeaponState weapon)
        {
            Level = 0;
            weapon.ApplyParameters(weapons[0]);
        }
    }
}
