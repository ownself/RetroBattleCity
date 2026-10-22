using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>M1 session. Owns combat time/input; presentation only reads the resulting state.</summary>
    public sealed class ArenaSession
    {
        private readonly PlayerInputBuffer input = new PlayerInputBuffer();
        public const long PlayerEntityId = 1;
        private readonly BulletTarget[] targets = new BulletTarget[1];
        private readonly float moveSpeed;
        private bool focused = true;
        public LogicClock Clock { get; } = new LogicClock();
        public MapState Map { get; }
        public TankMotor Player { get; }
        public BulletWorld Bullets { get; } = new BulletWorld();
        public WeaponState PlayerWeapon { get; }
        public TankCommand LastCommand { get; private set; } = new TankCommand(CardinalDirection.None);

        public ArenaSession(LevelDefinition level, TankDefinition tank)
        {
            if (tank == null) throw new ArgumentNullException(nameof(tank));
            tank.Validate();
            Map = new MapState(level);
            Player = new TankMotor(level.playerSpawnPixels, Map);
            moveSpeed = tank.moveSpeedPixelsPerSecond;
            PlayerWeapon = new WeaponState(tank, Bullets, CombatTeam.Player, PlayerEntityId);
        }

        public int AdvanceFrame(double elapsedSeconds, PlayerButtons buttons)
        {
            bool toggle = input.Sample(buttons, focused && !Clock.Paused);
            if (toggle && focused)
            {
                SetPaused(!Clock.Paused);
                return 0; // The transition frame never contributes elapsed combat time.
            }
            return Clock.Advance(elapsedSeconds, Step);
        }

        public void SetPaused(bool paused)
        {
            Clock.SetPaused(paused);
            input.SuppressHeld();
            LastCommand = new TankCommand(CardinalDirection.None);
        }

        public void SetFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) SetPaused(true); // Focus return is deliberately not resume.
        }

        private void Step()
        {
            LastCommand = input.ConsumeTick();
            Player.Step(LastCommand.Direction, moveSpeed, Map);
            PlayerWeapon.Step(LastCommand, Player.Position, Player.Facing);
            targets[0] = new BulletTarget(PlayerEntityId, BulletHitKind.Tank, CombatTeam.Player,
                new Rect(Player.Position - Vector2.one * 8, Vector2.one * 16));
            Bullets.Step(Map, targets);
            Map.ApplyBulletHits(Bullets.Hits); // All sweeps see one pre-damage snapshot for this tick.
        }
    }
}
