using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Owns combat time and stable tank order; presentation only reads settled state.</summary>
    public sealed class ArenaSession
    {
        private readonly PlayerInputBuffer input = new PlayerInputBuffer();
        public const long PlayerEntityId = 1;
        private readonly List<TankState> tanks = new List<TankState>();
        private readonly List<BulletTarget> targets = new List<BulletTarget>();
        private readonly List<Rect> blockers = new List<Rect>();
        private readonly Dictionary<long, EnemyBrain> enemyBrains = new Dictionary<long, EnemyBrain>();
        private readonly List<int> decisionMasks = new List<int>();
        private readonly Dictionary<long, TankDefinition> waveEnemyDefinitions = new Dictionary<long, TankDefinition>();
        private long nextEntityId;
        private bool focused = true;
        public LogicClock Clock { get; } = new LogicClock();
        public MapState Map { get; }
        public IReadOnlyList<TankState> Tanks { get; }
        public TankState PlayerTank { get; }
        public TankMotor Player => PlayerTank.Motor;
        public BulletWorld Bullets { get; } = new BulletWorld();
        public CombatEffects Effects { get; } = new CombatEffects();
        public WeaponState PlayerWeapon => PlayerTank.Weapon;
        public TankCommand LastCommand { get; private set; } = new TankCommand(CardinalDirection.None);
        public EnemyWaveState EnemyWave { get; }
        public BaseState Base { get; }
        /// <summary>Latched failure request for BC-206, not a terminal state or a combat freeze.</summary>
        public bool BaseFailureRequested => Base != null && !Base.IsAlive;
        /// <summary>Once, after this tick's damage and births settle. Late consumers can read the latched request.</summary>
        public event Action BaseDestroyed;
        public int AliveEnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < tanks.Count; i++) if (tanks[i].Team == CombatTeam.Enemy && tanks[i].IsAlive) count++;
                return count;
            }
        }

        public ArenaSession(LevelDefinition level, TankDefinition tank, EnemyWaveDefinition wave = null, bool enableBase = false)
        {
            if (tank == null) throw new ArgumentNullException(nameof(tank));
            tank.Validate();
            Map = new MapState(level);
            // Validate/snapshot the complete wave before registering any combat entity.
            EnemyWave = wave == null ? null : new EnemyWaveState(level, wave);
            if (enableBase)
            {
                if (!Map.CanOccupy(level.baseCenterPixels, BaseState.HalfSizePixels))
                    throw new ArgumentException("Base footprint is obstructed.", nameof(level));
                Base = new BaseState(level.baseCenterPixels);
            }
            Tanks = tanks.AsReadOnly();
            PlayerTank = AddTank(tank, CombatTeam.Player, level.playerSpawnPixels);
        }

        /// <summary>Explicit registration, not a wave/spawn policy. IDs are never reused in this session.</summary>
        public TankState AddTank(TankDefinition definition, CombatTeam team, Vector2 spawn)
        {
            if (EnemyWave != null && team == CombatTeam.Enemy &&
                AliveEnemyCount + EnemyWave.ReservedCount >= EnemyWave.MaxActiveEnemies)
                throw new InvalidOperationException("Enemy registration would exceed wave capacity including its reservation.");
            CollectBlockers(null);
            if (EnemyWave?.Reservation is EnemySpawnRequest reservation)
                blockers.Add(new Rect((Vector2)reservation.PositionPixels - Vector2.one * TankMotor.HalfSizePixels,
                    Vector2.one * (TankMotor.HalfSizePixels * 2)));
            if (nextEntityId >= BaseState.EntityId - 1) throw new InvalidOperationException("Tank IDs exhausted.");
            var state = new TankState(checked(nextEntityId + 1), team, spawn, definition, Map, Bullets);
            if (!Map.CanOccupy(spawn, TankMotor.HalfSizePixels, blockers))
                throw new ArgumentException("Tank spawn overlaps a living tank.", nameof(spawn));
            nextEntityId = state.EntityId;
            tanks.Add(state); // Monotonic IDs also establish the frozen movement/fire order.
            return state;
        }

        /// <summary>Only successful natural spawns have a definition for the wave's read-only display host.</summary>
        public TankDefinition GetWaveEnemyDefinition(long entityId)
            => waveEnemyDefinitions.TryGetValue(entityId, out var definition) ? definition : null;

        private bool IsEnemySpawnOccupied(Vector2Int position)
        {
            CollectBlockers(null);
            return !Map.CanOccupy(position, TankMotor.HalfSizePixels, blockers);
        }

        private bool TryCreateWaveEnemy(EnemySpawnRequest request)
        {
            if (AliveEnemyCount >= EnemyWave.MaxActiveEnemies || IsEnemySpawnOccupied(request.PositionPixels)) return false;
            // All constructors/validation run before either registry is changed. No public registration callbacks.
            var brain = new EnemyBrain(request.BrainSeed, EnemyBrainParameters.Prototype, CardinalDirection.Down);
            if (nextEntityId >= BaseState.EntityId - 1) throw new InvalidOperationException("Tank IDs exhausted.");
            var state = new TankState(checked(nextEntityId + 1), CombatTeam.Enemy, request.PositionPixels,
                request.Definition, Map, Bullets);
            enemyBrains.Add(state.EntityId, brain);
            waveEnemyDefinitions.Add(state.EntityId, request.Definition);
            tanks.Add(state);
            nextEntityId = state.EntityId;
            return true;
        }

        /// <summary>Opt-in control for an existing shared enemy. No spawning, waves, or implicit AI ownership.</summary>
        public EnemyBrain RegisterEnemyAI(long entityId, uint seed, EnemyBrainParameters parameters = null,
            CardinalDirection initialDirection = CardinalDirection.Down)
        {
            for (int i = 0; i < tanks.Count; i++) if (tanks[i].EntityId == entityId)
            {
                if (tanks[i].Team != CombatTeam.Enemy || !tanks[i].IsAlive || enemyBrains.ContainsKey(entityId))
                    throw new ArgumentException("AI requires a living, unregistered enemy tank.", nameof(entityId));
                var brain = new EnemyBrain(seed, parameters ?? EnemyBrainParameters.Prototype, initialDirection);
                enemyBrains.Add(entityId, brain);
                return brain;
            }
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        /// <summary>Non-player control seam. A held command persists, a fire edge is consumed once.</summary>
        public void SetTankCommand(long entityId, TankCommand command)
        {
            if (entityId == PlayerEntityId) throw new ArgumentException("Player commands come from the input buffer.", nameof(entityId));
            if (enemyBrains.ContainsKey(entityId)) throw new ArgumentException("Registered AI owns this tank's commands.", nameof(entityId));
            if (command.Direction != CardinalDirection.None) command.Direction.Vector();
            for (int i = 0; i < tanks.Count; i++) if (tanks[i].EntityId == entityId)
            {
                tanks[i].Command = Clock.Paused || !tanks[i].IsAlive ? new TankCommand(CardinalDirection.None) : command;
                return;
            }
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        private void CollectBlockers(TankState except)
        {
            blockers.Clear();
            if (Base != null) blockers.Add(Base.Bounds); // Intact and ruined bases both block tanks/spawns.
            for (int i = 0; i < tanks.Count; i++)
                if (tanks[i] != except && tanks[i].IsAlive) blockers.Add(tanks[i].Bounds);
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
            for (int i = 0; i < tanks.Count; i++) tanks[i].Command = LastCommand;
        }

        public void SetFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) SetPaused(true); // Focus return is deliberately not resume.
        }

        private void Step()
        {
            LastCommand = input.ConsumeTick();
            PlayerTank.Command = LastCommand;
            // Capture ALL local options before any decision or movement. Later IDs cannot observe this tick's moves.
            decisionMasks.Clear();
            for (int i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                int mask = 0;
                if (tank.IsAlive && enemyBrains.ContainsKey(tank.EntityId))
                {
                    CollectBlockers(tank);
                    for (int direction = 0; direction < 4; direction++)
                        if ((tank.Motor.PreviewStep((CardinalDirection)direction, tank.MoveSpeedPixelsPerSecond, Map, blockers)
                            - tank.Motor.Position).sqrMagnitude > MapState.Epsilon * MapState.Epsilon) mask |= 1 << direction;
                }
                decisionMasks.Add(mask);
            }
            for (int i = 0; i < tanks.Count; i++)
                if (enemyBrains.TryGetValue(tanks[i].EntityId, out var brain))
                    tanks[i].Command = brain.Decide(tanks[i].IsAlive, decisionMasks[i]);
            for (int i = 0; i < tanks.Count; i++)
            {
                CollectBlockers(tanks[i]); // Later IDs see earlier movement; no mutual crossing.
                tanks[i].Move(Map, blockers);
            }
            targets.Clear();
            for (int i = 0; i < tanks.Count; i++)
            {
                var state = tanks[i];
                state.Fire();
                if (state.IsAlive) targets.Add(new BulletTarget(state.EntityId, BulletHitKind.Tank, state.Team, state.Bounds));
            }
            if (Base != null && Base.IsAlive)
                targets.Add(new BulletTarget(BaseState.EntityId, BulletHitKind.Base, CombatTeam.Player, Base.Bounds));
            Bullets.Step(Map, targets);
            Map.ApplyBulletHits(Bullets.Hits); // All sweeps see one pre-damage snapshot for this tick.
            Effects.Step(Bullets.Hits); // Consume every tick, never from a potentially slower render frame.
            bool baseDestroyed = false;
            for (int hit = 0; hit < Bullets.Hits.Count; hit++)
            {
                if (Base != null && Base.ApplyHit(Bullets.Hits[hit]))
                {
                    baseDestroyed = true;
                    Effects.PlayExplosion(Base.Position);
                }
                for (int tank = 0; tank < tanks.Count; tank++)
                    if (tanks[tank].ApplyHit(Bullets.Hits[hit])) Effects.PlayExplosion(tanks[tank].Motor.Position);
            }
            // Birth commits AFTER damage. New tanks cannot enter the earlier decision/move/fire loops this tick.
            EnemyWave?.Step(AliveEnemyCount, IsEnemySpawnOccupied, TryCreateWaveEnemy);
            if (baseDestroyed) BaseDestroyed?.Invoke();
        }
    }
}
