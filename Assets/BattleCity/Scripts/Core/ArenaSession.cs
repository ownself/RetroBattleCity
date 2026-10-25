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
        private readonly List<TankState> hitCarriers = new List<TankState>();
        private readonly Dictionary<long, TankDefinition> waveEnemyDefinitions = new Dictionary<long, TankDefinition>();
        private readonly Vector2 playerSpawn;
        private readonly int stageNumber, pickupScore;
        private long nextEntityId;
        private bool focused = true;
        private readonly SessionFlowState flow;
        private bool advancing, notifying, retired, transitionPending, focusPausePending;
        private PlayerButtons previousButtons;
        public bool EnableSessionLifecycle => flow != null;
        public SessionPhase Phase => flow?.Phase ?? (Clock.Paused ? SessionPhase.Paused : SessionPhase.Playing);
        public SessionFailureReason FailureReason => flow?.FailureReason ?? SessionFailureReason.None;
        public event Action StateChanged;
        public event Action SessionEnded;
        public LogicClock Clock { get; } = new LogicClock();
        public MapState Map { get; }
        public IReadOnlyList<TankState> Tanks { get; }
        public TankState PlayerTank { get; }
        public TankMotor Player => PlayerTank.Motor;
        public BulletWorld Bullets { get; } = new BulletWorld();
        public CombatEffects Effects { get; } = new CombatEffects();
        public ScoreState Scoring { get; } = new ScoreState();
        public long Score => Scoring.Total;
        public int DefeatedEnemyCount => Scoring.DefeatedCount;
        public StageResult Result { get; private set; }
        public WeaponState PlayerWeapon => PlayerTank.Weapon;
        public TankCommand LastCommand { get; private set; } = new TankCommand(CardinalDirection.None);
        public EnemyWaveState EnemyWave { get; }
        public BaseState Base { get; }
        public PlayerLifeState PlayerLife { get; }
        public PlayerUpgradeState PlayerUpgrades { get; }
        public PickupState Pickups { get; }
        public BaseFortificationState BaseFortification { get; }
        public bool PlayerIsProtected => PlayerTank.IsAlive &&
            ((PlayerLife?.IsProtected ?? false) || (Pickups?.HelmetRemainingTicks ?? 0) > 0);
        public bool PlayerLifeFailureRequested => PlayerLife != null && PlayerLife.IsExhausted;
        public event Action PlayerLivesExhausted;
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

        public ArenaSession(LevelDefinition level, TankDefinition tank, EnemyWaveDefinition wave = null, bool enableBase = false,
            PlayerLifeParameters playerLifeParameters = null, bool enableSessionLifecycle = false,
            PlayerUpgradeDefinition playerUpgrades = null, PickupDefinition pickups = null, uint? pickupSeed = null)
        {
            if (enableSessionLifecycle && wave == null) throw new ArgumentNullException(nameof(wave));
            if (enableSessionLifecycle) { flow = new SessionFlowState(); Clock.SetPaused(true); }
            if (tank == null) throw new ArgumentNullException(nameof(tank));
            tank.Validate();
            PlayerUpgrades = playerUpgrades == null ? null : new PlayerUpgradeState(playerUpgrades, tank);
            Map = new MapState(level);
            stageNumber = level.stageNumber;
            playerSpawn = level.playerSpawnPixels;
            PlayerLife = playerLifeParameters == null ? null : new PlayerLifeState(playerLifeParameters);
            // Validate/snapshot the complete wave before registering any combat entity.
            EnemyWave = wave == null ? null : new EnemyWaveState(level, wave);
            if (pickups != null && wave == null) throw new ArgumentException("Pickup carriers require a natural wave.", nameof(pickups));
            Pickups = pickups == null ? null : new PickupState(pickups, level, pickupSeed);
            pickupScore = pickups?.collectionScore ?? 0;
            if (enableBase)
            {
                if (!Map.CanOccupy(level.baseCenterPixels, BaseState.HalfSizePixels))
                    throw new ArgumentException("Base footprint is obstructed.", nameof(level));
                Base = new BaseState(level.baseCenterPixels);
                if (pickups != null)
                    BaseFortification = new BaseFortificationState(Map, level.baseCenterPixels, pickups.shovelDurationTicks);
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
            state.CarriesPickup = Pickups != null && Pickups.IsCarrierIndex(request.SequenceIndex);
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
            // Notifications may call public APIs. Never recursively run a combat tick.
            if (advancing || notifying || retired) return 0;
            bool start = (buttons & PlayerButtons.Start) != 0 && (previousButtons & PlayerButtons.Start) == 0;
            previousButtons = buttons;
            bool toggle = input.Sample(buttons, focused && !Clock.Paused && PlayerTank.IsAlive);
            if (focused && ((flow != null && start && StartBattle()) ||
                (toggle && ChangePaused(!Clock.Paused))))
            {
                transitionPending = false;
                return 0;
            }
            if (transitionPending) { transitionPending = false; ClearCommands(); return 0; }
            // The state model, not a writable Clock flag, owns lifecycle permission.
            if (flow != null && flow.Phase != SessionPhase.Playing) { Clock.SetPaused(true); return 0; }
            advancing = true;
            try { return Clock.Advance(elapsedSeconds, Step); }
            finally { advancing = false; }
        }

        /// <summary>BC-304 pickup seam: one request advances at most one level. No input/debug shortcut.</summary>
        public bool TryUpgradePlayer()
        {
            if (retired || notifying || Clock.Paused || Phase != SessionPhase.Playing || !PlayerTank.IsAlive || PlayerUpgrades == null)
                return false;
            return PlayerUpgrades.TryUpgrade(PlayerWeapon);
        }

        public bool StartBattle()
        {
            if (flow == null || !focused || retired || notifying || advancing || !flow.TryStart()) return false;
            Clock.SetPaused(false);
            ClearCommands();
            transitionPending = true;
            NotifyStateChanged();
            return true;
        }

        public void SetPaused(bool paused) => ChangePaused(paused);

        private bool ChangePaused(bool paused)
        {
            if (retired || notifying || (flow != null && (!focused && !paused))) return false;
            if (flow != null && !flow.SetPaused(paused)) return false;
            Clock.SetPaused(paused);
            ClearCommands();
            if (flow != null) { transitionPending = true; NotifyStateChanged(); }
            return true;
        }

        private void ClearCommands()
        {
            input.SuppressHeld();
            LastCommand = new TankCommand(CardinalDirection.None);
            for (int i = 0; i < tanks.Count; i++) tanks[i].Command = LastCommand;
        }

        internal void SuppressInput(PlayerButtons buttons)
        {
            previousButtons = buttons;
            input.Sample(buttons, false);
            ClearCommands();
        }

        // Stop an old controller session immediately, even when restart was requested by a notification.
        internal void Stop()
        {
            retired = true;
            Pickups?.ClearActive();
            Pickups?.ClearPlayerEffects();
            Pickups?.ClearGlobalEffects();
            Clock.SetPaused(true);
            BaseFortification?.Cancel();
            for (int i = 0; i < tanks.Count; i++) tanks[i].Motor.Stop();
            ClearCommands();
        }

        internal void Release()
        {
            Stop();
            Bullets.Clear();
            Effects.Clear();
        }

        private void NotifyStateChanged()
        {
            notifying = true;
            try { StateChanged?.Invoke(); }
            finally { FinishNotification(); }
        }

        private void FinishNotification()
        {
            notifying = false;
            if (!focusPausePending) return;
            focusPausePending = false;
            ChangePaused(true);
        }

        public void SetFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (hasFocus) return; // Focus return is deliberately not resume.
            if (notifying)
            {
                // Stop time immediately, but defer the state transition until callbacks have returned.
                focusPausePending = true;
                Clock.SetPaused(true);
                ClearCommands();
            }
            else SetPaused(true);
        }

        private bool TryRespawnPlayer()
        {
            CollectBlockers(PlayerTank);
            if (EnemyWave?.Reservation is EnemySpawnRequest reservation)
                blockers.Add(new Rect((Vector2)reservation.PositionPixels - Vector2.one * TankMotor.HalfSizePixels,
                    Vector2.one * (TankMotor.HalfSizePixels * 2)));
            if (!Map.CanOccupy(playerSpawn, TankMotor.HalfSizePixels, blockers)) return false;
            PlayerTank.Respawn(playerSpawn);
            return true;
        }

        private void CollectFortificationOccupants()
        {
            CollectBlockers(null);
            if (EnemyWave?.Reservation is EnemySpawnRequest reservation)
                blockers.Add(new Rect((Vector2)reservation.PositionPixels - Vector2.one * TankMotor.HalfSizePixels,
                    Vector2.one * (TankMotor.HalfSizePixels * 2)));
        }

        private bool IsFrozenEnemy(TankState tank) => tank.Team == CombatTeam.Enemy &&
            (Pickups?.ClockRemainingTicks ?? 0) > 0;

        // Caller supplies the single alive -> dead transition, regardless of damage source.
        private void SettleTankDeath(TankState state)
        {
            Effects.PlayExplosion(state.Motor.Position);
            if (state == PlayerTank) PlayerUpgrades?.ResetOnDeath(PlayerWeapon);
            if (state.Team != CombatTeam.Enemy) return;
            Scoring.RecordEnemy(state);
        }

        private bool SettleOutcome() => flow != null && flow.Settle(BaseFailureRequested, PlayerLifeFailureRequested,
            EnemyWave.SequenceExhausted, AliveEnemyCount, EnemyWave.PendingCount, EnemyWave.ReservedCount);

        private void Step()
        {
            LastCommand = PlayerTank.IsAlive ? input.ConsumeTick() : new TankCommand(CardinalDirection.None);
            PlayerTank.Command = LastCommand;
            // Capture ALL local options before any decision or movement. Later IDs cannot observe this tick's moves.
            decisionMasks.Clear();
            for (int i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                int mask = 0;
                if (tank.IsAlive && !IsFrozenEnemy(tank) && enemyBrains.ContainsKey(tank.EntityId))
                {
                    CollectBlockers(tank);
                    for (int direction = 0; direction < 4; direction++)
                        if ((tank.Motor.PreviewStep((CardinalDirection)direction, tank.MoveSpeedPixelsPerSecond, Map, blockers)
                            - tank.Motor.Position).sqrMagnitude > MapState.Epsilon * MapState.Epsilon) mask |= 1 << direction;
                }
                decisionMasks.Add(mask);
            }
            for (int i = 0; i < tanks.Count; i++)
                if (!IsFrozenEnemy(tanks[i]) && enemyBrains.TryGetValue(tanks[i].EntityId, out var brain))
                    tanks[i].Command = brain.Decide(tanks[i].IsAlive, decisionMasks[i]);
            for (int i = 0; i < tanks.Count; i++)
            {
                if (IsFrozenEnemy(tanks[i])) continue;
                CollectBlockers(tanks[i]); // Later IDs see earlier movement; no mutual crossing.
                tanks[i].Move(Map, blockers);
            }
            targets.Clear();
            for (int i = 0; i < tanks.Count; i++)
            {
                var state = tanks[i];
                if (!IsFrozenEnemy(state)) state.Fire();
                if (state.IsAlive) targets.Add(new BulletTarget(state.EntityId, BulletHitKind.Tank, state.Team, state.Bounds));
            }
            if (Base != null && Base.IsAlive)
                targets.Add(new BulletTarget(BaseState.EntityId, BulletHitKind.Base, CombatTeam.Player, Base.Bounds));
            Bullets.Step(Map, targets);
            Map.ApplyBulletHits(Bullets.Hits); // All sweeps see one pre-damage snapshot for this tick.
            Effects.Step(Bullets.Hits); // Consume every tick, never from a potentially slower render frame.
            bool baseDestroyed = false;
            hitCarriers.Clear();
            for (int hit = 0; hit < Bullets.Hits.Count; hit++)
            {
                if (Base != null && Base.ApplyHit(Bullets.Hits[hit]))
                {
                    baseDestroyed = true;
                    Effects.PlayExplosion(Base.Position);
                }
                for (int tank = 0; tank < tanks.Count; tank++)
                {
                    if (tanks[tank] == PlayerTank && PlayerIsProtected) continue;
                    var state = tanks[tank];
                    int previousHp = state.HitPoints;
                    bool died = state.ApplyHit(Bullets.Hits[hit]);
                    if (state.HitPoints < previousHp && state.CarriesPickup)
                    {
                        state.CarriesPickup = false;
                        hitCarriers.Add(state);
                    }
                    if (!died) continue;
                    SettleTankDeath(state);
                }
            }
            Pickups?.StepPlayerEffects(PlayerTank.IsAlive);
            Pickups?.StepGlobalEffects();
            // Birth commits AFTER damage. New tanks cannot enter the earlier decision/move/fire loops this tick.
            var lifeResult = PlayerLife == null ? default : PlayerLife.Step(PlayerTank.IsAlive, TryRespawnPlayer);
            if (!PlayerTank.IsAlive || lifeResult.Respawned)
            {
                input.SuppressHeld();
                LastCommand = new TankCommand(CardinalDirection.None);
                PlayerTank.Command = LastCommand;
            }
            EnemyWave?.Step(AliveEnemyCount, IsEnemySpawnOccupied, TryCreateWaveEnemy);
            bool ended = SettleOutcome();
            if (!ended && BaseFortification != null)
            {
                CollectFortificationOccupants();
                BaseFortification.Step(blockers); // Complete future tick; births/reservations already settled, receipts not yet applied.
            }
            if (!ended && Pickups != null)
            {
                // No terminal-tick drop/collection. Every carrier was consumed only by accepted damage.
                // Drop after ALL damage and births: a dead player never supplies a bogus reachability origin.
                for (int drop = 0; drop < hitCarriers.Count; drop++)
                {
                    CollectBlockers(null);
                    if (EnemyWave?.Reservation is EnemySpawnRequest reservation)
                        blockers.Add(new Rect((Vector2)reservation.PositionPixels - Vector2.one * 8, Vector2.one * 16));
                    Pickups.TryDrop(Map, Player.Position, PlayerTank.IsAlive, blockers, Base?.Bounds);
                }
                if (Pickups.TryCollect(PlayerTank.Bounds, PlayerTank.IsAlive, Clock.Tick))
                {
                    // Count every accepted receipt, even when a capped/optional effect cannot change state.
                    Scoring.RecordPickup(Pickups.LastCollected.Value.Item.Kind, pickupScore);
                    // Consume only a newly committed receipt, never replay LastCollected on later ticks/renders.
                    switch (Pickups.LastCollected.Value.Item.Kind)
                    {
                        case PickupKind.Star: TryUpgradePlayer(); break;
                        case PickupKind.Helmet: Pickups.RefreshHelmet(); break;
                        case PickupKind.Tank: PlayerLife?.TryAddLife(Pickups.MaximumLives); break;
                        case PickupKind.Clock: Pickups.RefreshClock(); break;
                        case PickupKind.Grenade:
                            for (int tank = 0; tank < tanks.Count; tank++)
                                if (tanks[tank].Team == CombatTeam.Enemy && tanks[tank].TryKill())
                                    SettleTankDeath(tanks[tank]);
                            EnemyWave?.RefreshAliveCount(AliveEnemyCount);
                            ended = SettleOutcome(); // Deaths only: never advance the wave a second time.
                            break;
                        case PickupKind.Shovel:
                            if (BaseFortification != null)
                            {
                                CollectFortificationOccupants();
                                BaseFortification.Activate(blockers);
                            }
                            break;
                    }
                }
            }
            if (ended)
            {
                Clock.SetPaused(true); ClearCommands(); Pickups?.ClearActive();
                Pickups?.ClearPlayerEffects(); Pickups?.ClearGlobalEffects();
                BaseFortification?.Cancel();
                for (int i = 0; i < tanks.Count; i++) tanks[i].Motor.Stop();
                Result = new StageResult(stageNumber, Phase, FailureReason, Clock.Tick,
                    PlayerLife?.RemainingLives ?? (PlayerTank.IsAlive ? 1 : 0), Scoring);
            }
            // All original death notifications survive simultaneous failures. The outcome is already frozen.
            notifying = true;
            try
            {
                if (lifeResult.LivesExhaustedThisTick) PlayerLivesExhausted?.Invoke();
                if (baseDestroyed) BaseDestroyed?.Invoke();
                if (ended) { StateChanged?.Invoke(); SessionEnded?.Invoke(); }
            }
            finally { FinishNotification(); }
        }
    }
}
