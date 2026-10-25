using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    public sealed class ArenaController : MonoBehaviour
    {
        public LevelDefinition level;
        public TankDefinition playerDefinition;
        public PlayerUpgradeDefinition playerUpgradeDefinition;
        public PickupDefinition pickupDefinition;
        public EnemyWaveDefinition enemyWaveDefinition;
        public SpriteCatalog sprites;
        public ArenaMapView mapView;
        public SpriteRenderer playerRenderer;
        // Explicit binding enables the base; unbound combat harnesses remain base-free.
        public SpriteRenderer baseRenderer;
        // Stage01 opts in explicitly; legacy combat harnesses keep death-only behavior.
        public bool enablePlayerLifecycle;
        public bool enableSessionLifecycle;
        private LevelDefinition initialLevel;
        private TankDefinition initialPlayer;
        private PlayerUpgradeDefinition initialUpgrades;
        private PickupDefinition initialPickups;
        private EnemyWaveDefinition initialWave, sessionWave;
        private readonly List<UnityEngine.Object> initialObjects = new List<UnityEngine.Object>();
        private bool initialBaseEnabled, initialPlayerLifecycle, initialSessionLifecycle;
        private bool focused, updating, restarting, restartPending;
        private PlayerButtons previousButtons;
        private readonly Sprite[] shieldFrames = new Sprite[2];
        private SpriteRenderer playerShield;
        private Sprite baseAlive, baseDead;
        private Sprite[,,] playerFrames;
        private readonly Sprite[] spawnFrames = new Sprite[4];
        private readonly Dictionary<long, EnemyTankView> enemyViews = new Dictionary<long, EnemyTankView>();
        private readonly List<long> retiredViews = new List<long>();
        private SpriteRenderer spawnHint;
        public ArenaSession Session { get; private set; }
        public BulletView BulletView { get; private set; }
        public CombatEffectView EffectView { get; private set; }
        public PickupView PickupView { get; private set; }
        public ArenaHud hud;
        public SinglePlayerFlow singlePlayerFlow;

        private void Awake()
        {
            ValidateBindings();
            CaptureInitialConfiguration();
            focused = Application.isFocused;
            int stages = initialUpgrades == null ? 1 : initialUpgrades.levels.Length;
            playerFrames = new Sprite[stages, 4, 2];
            for (int stage = 0; stage < stages; stage++)
                for (int direction = 0; direction < 4; direction++) for (int frame = 0; frame < 2; frame++)
                    playerFrames[stage, direction, frame] = sprites.Get((initialUpgrades == null ? initialPlayer.spritePrefix :
                        initialUpgrades.levels[stage].spritePrefix) + "." + ((CardinalDirection)direction).SpriteKey() + "." + frame);
            if (enemyWaveDefinition != null)
                for (int i = 0; i < spawnFrames.Length; i++) spawnFrames[i] = sprites.Get("effect.spawn." + i);
            if (baseRenderer != null)
            {
                baseAlive = sprites.Get("base.alive"); baseDead = sprites.Get("base.dead");
            }
            if (enablePlayerLifecycle || initialPickups != null)
            {
                for (int i = 0; i < shieldFrames.Length; i++) shieldFrames[i] = sprites.Get("effect.shield." + i);
                playerShield = new GameObject("Player shield - display only", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                playerShield.transform.SetParent(transform, false);
                playerShield.sharedMaterial = playerRenderer.sharedMaterial;
                playerShield.sortingLayerName = "Effects";
            }
            Session = CreateSession();
            BulletView = new GameObject("Bullets - display only").AddComponent<BulletView>();
            BulletView.transform.SetParent(transform, false);
            BulletView.Initialize(Session.Bullets, sprites, playerRenderer.sharedMaterial);
            EffectView = new GameObject("Combat effects - display only").AddComponent<CombatEffectView>();
            EffectView.transform.SetParent(transform, false);
            EffectView.Initialize(Session.Effects, sprites, playerRenderer.sharedMaterial);
            if (Session.Pickups != null)
            {
                PickupView = new GameObject("Pickups - display only").AddComponent<PickupView>();
                PickupView.transform.SetParent(transform, false);
                PickupView.Initialize(Session.Pickups, sprites, playerRenderer.sharedMaterial);
            }
            mapView.ConfigureSpecialTerrain(sprites);
            mapView.Render(Session.Map);
            Session.SetFocus(focused);
            RenderBase();
            RenderPlayer();
            RenderEnemies();
            RenderHud();
        }

        private void OnEnable()
        {
            if (Session != null && PickupView != null)
                PickupView.Initialize(Session.Pickups, sprites, playerRenderer.sharedMaterial);
        }

        private T Snapshot<T>(T source) where T : UnityEngine.Object
        {
            var copy = Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            initialObjects.Add(copy);
            return copy;
        }

        private void CaptureInitialConfiguration()
        {
            initialLevel = Snapshot(level);
            initialPlayer = Snapshot(playerDefinition);
            if (pickupDefinition != null) initialPickups = Snapshot(pickupDefinition);
            if (playerUpgradeDefinition != null)
            {
                initialUpgrades = Snapshot(playerUpgradeDefinition);
                initialUpgrades.levels = new TankDefinition[playerUpgradeDefinition.levels.Length];
                for (int i = 0; i < initialUpgrades.levels.Length; i++)
                    initialUpgrades.levels[i] = Snapshot(playerUpgradeDefinition.levels[i]);
            }
            if (enemyWaveDefinition != null)
            {
                initialWave = Snapshot(enemyWaveDefinition);
                initialWave.tankDefinitions = new TankDefinition[enemyWaveDefinition.tankDefinitions.Length];
                for (int i = 0; i < initialWave.tankDefinitions.Length; i++)
                    initialWave.tankDefinitions[i] = Snapshot(enemyWaveDefinition.tankDefinitions[i]);
                // Wave requests expose definitions for display; never expose the private restart originals.
                sessionWave = Snapshot(initialWave);
                sessionWave.tankDefinitions = new TankDefinition[initialWave.tankDefinitions.Length];
                for (int i = 0; i < sessionWave.tankDefinitions.Length; i++)
                    sessionWave.tankDefinitions[i] = Snapshot(initialWave.tankDefinitions[i]);
            }
            initialBaseEnabled = baseRenderer != null;
            initialPlayerLifecycle = enablePlayerLifecycle;
            initialSessionLifecycle = enableSessionLifecycle;
        }

        private ArenaSession CreateSession()
        {
            if (sessionWave != null)
                for (int i = 0; i < sessionWave.tankDefinitions.Length; i++)
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(initialWave.tankDefinitions[i]), sessionWave.tankDefinitions[i]);
            return new ArenaSession(initialLevel, initialPlayer, sessionWave,
                initialBaseEnabled, initialPlayerLifecycle ? PlayerLifeParameters.Prototype : null, initialSessionLifecycle, initialUpgrades, initialPickups);
        }

        public bool StartBattle() => Session != null && Session.StartBattle();

        public void RestartSession()
        {
            if (Session == null || restarting) return;
            Session.Stop();
            if (updating) { restartPending = true; return; }
            RestartNow();
        }

        private void RestartNow()
        {
            restarting = true;
            try
            {
                Session.Release();
                HidePlayerShield();
                ClearEnemyDisplays();
                Session = CreateSession();
                Session.SetFocus(focused);
                // Preserve physical management edges in the controller; do not replay held combat keys.
                Session.SuppressInput(previousButtons);
                BulletView.Initialize(Session.Bullets, sprites, playerRenderer.sharedMaterial);
                EffectView.Initialize(Session.Effects, sprites, playerRenderer.sharedMaterial);
                if (PickupView != null) PickupView.Initialize(Session.Pickups, sprites, playerRenderer.sharedMaterial);
                RenderSession();
            }
            finally { restarting = false; restartPending = false; }
        }

        private void Update()
        {
            var buttons = KeyboardInputSource.Read();
            bool restart = (buttons & PlayerButtons.Restart) != 0 && (previousButtons & PlayerButtons.Restart) == 0;
            previousButtons = buttons;
            if (singlePlayerFlow != null)
            {
                if (!singlePlayerFlow.Route(this, buttons, focused)) { RenderSession(); return; }
                // Flow owns restart; the legacy combat harness retains its original input path.
                buttons &= ~(PlayerButtons.Restart | PlayerButtons.Start);
            }
            else if (initialSessionLifecycle && focused && restart) { RestartSession(); return; }
            updating = true;
            try { Session.AdvanceFrame(Time.unscaledDeltaTime, buttons); }
            finally { updating = false; }
            if (restartPending) RestartNow();
            else RenderSession();
        }

        private void RenderSession()
        {
            mapView.Render(Session.Map);
            RenderBase();
            RenderPlayer();
            RenderEnemies();
            BulletView.Render();
            EffectView.Render();
            if (PickupView != null) PickupView.Render();
            RenderHud();
        }

        private void RenderHud()
        {
            if (hud != null)
            {
                hud.Render(Session, initialLevel.stageNumber);
                if (singlePlayerFlow != null && singlePlayerFlow.State.Screen != SinglePlayerScreen.Battle) hud.Clear();
            }
            if (singlePlayerFlow != null) singlePlayerFlow.Render(Session, initialLevel.stageNumber);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (singlePlayerFlow != null) singlePlayerFlow.State.Suppress(KeyboardInputSource.Read());
            if (Session != null) Session.SetFocus(hasFocus);
        }

        private void OnDisable()
        {
            if (singlePlayerFlow != null) singlePlayerFlow.Clear();
            if (hud != null) hud.Clear();
            if (Session != null)
            {
                Session.SetPaused(true);
                Session.Bullets.Clear(); // Release owner leases, not just their display.
                if (BulletView != null) BulletView.Render();
            }
            HidePlayerShield();
            if (PickupView != null) PickupView.Clear();
            ClearEnemyDisplays();
            ClearEffects();
        }

        private void OnDestroy()
        {
            if (singlePlayerFlow != null) singlePlayerFlow.Clear();
            if (hud != null) hud.Clear();
            ClearEnemyDisplays();
            ClearEffects();
            // Destroying just the host component must not strand children referencing its old session.
            if (BulletView != null) Destroy(BulletView.gameObject);
            if (EffectView != null) Destroy(EffectView.gameObject);
            if (PickupView != null) { PickupView.Clear(); Destroy(PickupView.gameObject); }
            if (playerShield != null) { HidePlayerShield(); Destroy(playerShield.gameObject); }
            Session?.Release();
            Session = null;
            foreach (var owned in initialObjects)
                if (owned != null)
                {
                    if (Application.isPlaying) Destroy(owned);
                    else DestroyImmediate(owned);
                }
            initialObjects.Clear();
        }

        private void HidePlayerShield()
        {
            if (playerShield == null) return;
            playerShield.enabled = false;
            playerShield.sprite = null;
        }

        /// <summary>Clears logical and visible effects without resuming combat.</summary>
        public void ClearEffects()
        {
            if (Session != null) Session.Effects.Clear();
            if (EffectView != null) EffectView.Clear();
        }

        private void RenderEnemies()
        {
            retiredViews.Clear();
            foreach (var pair in enemyViews)
            {
                TankState tank = null;
                for (int i = 0; i < Session.Tanks.Count; i++)
                    if (Session.Tanks[i].EntityId == pair.Key) { tank = Session.Tanks[i]; break; }
                if (tank == null || !tank.IsAlive) retiredViews.Add(pair.Key);
            }
            foreach (long id in retiredViews)
            {
                var view = enemyViews[id];
                view.Clear();
                Destroy(view.gameObject);
                enemyViews.Remove(id);
            }
            foreach (var tank in Session.Tanks)
            {
                var definition = Session.GetWaveEnemyDefinition(tank.EntityId);
                if (!tank.IsAlive || definition == null) continue;
                if (!enemyViews.TryGetValue(tank.EntityId, out var view))
                {
                    view = new GameObject("Enemy " + tank.EntityId + " - display only").AddComponent<EnemyTankView>();
                    view.transform.SetParent(transform, false);
                    view.Initialize(tank, definition, sprites, playerRenderer.sharedMaterial);
                    enemyViews.Add(tank.EntityId, view);
                }
                view.Render(Session.Clock.Tick);
            }
            var wave = Session.EnemyWave;
            if (wave?.Reservation is EnemySpawnRequest reservation)
            {
                if (spawnHint == null)
                {
                    spawnHint = new GameObject("Enemy preparation - display only", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    spawnHint.transform.SetParent(transform, false);
                    spawnHint.sharedMaterial = playerRenderer.sharedMaterial;
                    spawnHint.sortingLayerName = "Effects";
                }
                int age = wave.PreparationTicks - wave.PreparationRemainingTicks;
                int frame = (int)Math.Min(3L, (long)age * spawnFrames.Length / wave.PreparationTicks);
                spawnHint.sprite = spawnFrames[frame];
                spawnHint.transform.position = PixelGrid.ToWorld(reservation.PositionPixels);
                spawnHint.enabled = true;
            }
            else if (spawnHint != null) { spawnHint.enabled = false; spawnHint.sprite = null; }
        }

        private void ClearEnemyDisplays()
        {
            foreach (var view in enemyViews.Values)
                if (view != null) { view.Clear(); Destroy(view.gameObject); }
            enemyViews.Clear();
            retiredViews.Clear();
            if (spawnHint != null)
            {
                spawnHint.enabled = false; spawnHint.sprite = null;
                Destroy(spawnHint.gameObject); spawnHint = null;
            }
        }

        private void RenderBase()
        {
            if (baseRenderer == null) return;
            baseRenderer.transform.position = PixelGrid.ToWorld(Session.Base.Position);
            baseRenderer.sprite = Session.Base.IsAlive ? baseAlive : baseDead;
        }

        private void RenderPlayer()
        {
            playerRenderer.enabled = Session.PlayerTank.IsAlive;
            var position = Session.Player.Position;
            // Display rounding is never written back to the motor.
            position = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
            playerRenderer.transform.position = PixelGrid.ToWorld(position);
            playerRenderer.sprite = playerFrames[Session.PlayerUpgrades?.Level ?? 0, (int)Session.Player.Facing, Session.Player.TrackFrame];
            if (playerShield != null)
            {
                if (Session.PlayerIsProtected)
                {
                    int age = Session.Pickups != null && Session.Pickups.HelmetRemainingTicks > 0
                        ? Session.Pickups.HelmetDurationTicks - Session.Pickups.HelmetRemainingTicks
                        : Session.PlayerLife.Parameters.ProtectionTicks - Session.PlayerLife.ProtectionRemainingTicks;
                    playerShield.sprite = shieldFrames[age / TankMotor.TrackFrameTicks % shieldFrames.Length];
                    playerShield.transform.position = playerRenderer.transform.position;
                    playerShield.enabled = true;
                }
                else HidePlayerShield();
            }
        }

        public void ValidateBindings()
        {
            if (level == null || playerDefinition == null || sprites == null || mapView == null || playerRenderer == null)
                throw new InvalidOperationException("Missing arena reference.");
            level.Validate(); playerDefinition.Validate(); sprites.Validate(); mapView.ValidateBindings();
            playerDefinition.ValidateSprites(sprites);
            if (playerUpgradeDefinition != null)
            {
                playerUpgradeDefinition.ValidateFor(playerDefinition);
                playerUpgradeDefinition.ValidateSprites(sprites);
            }
            if (pickupDefinition != null)
            {
                if (enemyWaveDefinition == null || playerRenderer.sharedMaterial == null)
                    throw new InvalidOperationException("Pickup display/carriers require a wave and material.");
                pickupDefinition.ValidateFor(level);
                pickupDefinition.ValidateSprites(sprites);
                // Resolve configured carrier families/frames before entering play, not on a later spawn.
                _ = new EnemyWaveState(level, enemyWaveDefinition);
                foreach (int index in pickupDefinition.carrierSequenceIndices)
                {
                    var id = level.enemySequence[index];
                    foreach (var definition in enemyWaveDefinition.tankDefinitions)
                        if (definition.definitionId == id) PickupDefinition.ValidateCarrierSprites(definition, sprites);
                }
            }
            if (hud != null) hud.ValidateBindings();
            if (singlePlayerFlow != null)
            {
                if (!enableSessionLifecycle) throw new InvalidOperationException("Single-player flow requires session lifecycle.");
                singlePlayerFlow.ValidateBindings();
            }
            if (enableSessionLifecycle && enemyWaveDefinition == null)
                throw new InvalidOperationException("Session lifecycle requires an enemy wave definition.");
            if (baseRenderer != null)
            {
                if (baseRenderer == playerRenderer) throw new InvalidOperationException("Base and player require distinct renderers.");
                sprites.Get("base.alive"); sprites.Get("base.dead");
            }
            if (enablePlayerLifecycle || pickupDefinition != null)
            {
                sprites.Get("effect.shield.0"); sprites.Get("effect.shield.1");
            }
            if (enemyWaveDefinition != null)
            {
                // Resolve every sequence ID now, not halfway through a future spawn callback.
                _ = new EnemyWaveState(level, enemyWaveDefinition);
                if (playerRenderer.sharedMaterial == null) throw new InvalidOperationException("Missing enemy display material.");
                foreach (var definition in enemyWaveDefinition.tankDefinitions) definition.ValidateSprites(sprites);
                for (int frame = 0; frame < 4; frame++) sprites.Get("effect.spawn." + frame);
            }
        }
    }
}
