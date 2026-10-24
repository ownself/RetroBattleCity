using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    public sealed class ArenaController : MonoBehaviour
    {
        public LevelDefinition level;
        public TankDefinition playerDefinition;
        public EnemyWaveDefinition enemyWaveDefinition;
        public SpriteCatalog sprites;
        public ArenaMapView mapView;
        public SpriteRenderer playerRenderer;
        // Explicit binding enables the base; unbound combat harnesses remain base-free.
        public SpriteRenderer baseRenderer;
        // Stage01 opts in explicitly; legacy combat harnesses keep death-only behavior.
        public bool enablePlayerLifecycle;
        private readonly Sprite[] shieldFrames = new Sprite[2];
        private SpriteRenderer playerShield;
        private Sprite baseAlive, baseDead;
        private readonly Sprite[,] playerFrames = new Sprite[4, 2];
        private readonly Sprite[] spawnFrames = new Sprite[4];
        private readonly Dictionary<long, EnemyTankView> enemyViews = new Dictionary<long, EnemyTankView>();
        private readonly List<long> retiredViews = new List<long>();
        private SpriteRenderer spawnHint;
        public ArenaSession Session { get; private set; }
        public BulletView BulletView { get; private set; }
        public CombatEffectView EffectView { get; private set; }

        private void Awake()
        {
            ValidateBindings();
            for (int direction = 0; direction < 4; direction++) for (int frame = 0; frame < 2; frame++)
                playerFrames[direction, frame] = sprites.Get(playerDefinition.spritePrefix + "." + ((CardinalDirection)direction).SpriteKey() + "." + frame);
            if (enemyWaveDefinition != null)
                for (int i = 0; i < spawnFrames.Length; i++) spawnFrames[i] = sprites.Get("effect.spawn." + i);
            if (baseRenderer != null)
            {
                baseAlive = sprites.Get("base.alive"); baseDead = sprites.Get("base.dead");
            }
            if (enablePlayerLifecycle)
            {
                for (int i = 0; i < shieldFrames.Length; i++) shieldFrames[i] = sprites.Get("effect.shield." + i);
                playerShield = new GameObject("Player shield - display only", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                playerShield.transform.SetParent(transform, false);
                playerShield.sharedMaterial = playerRenderer.sharedMaterial;
                playerShield.sortingLayerName = "Effects";
            }
            Session = new ArenaSession(level, playerDefinition, enemyWaveDefinition, baseRenderer != null,
                enablePlayerLifecycle ? PlayerLifeParameters.Prototype : null);
            BulletView = new GameObject("Bullets - display only").AddComponent<BulletView>();
            BulletView.transform.SetParent(transform, false);
            BulletView.Initialize(Session.Bullets, sprites, playerRenderer.sharedMaterial);
            EffectView = new GameObject("Combat effects - display only").AddComponent<CombatEffectView>();
            EffectView.transform.SetParent(transform, false);
            EffectView.Initialize(Session.Effects, sprites, playerRenderer.sharedMaterial);
            mapView.Render(Session.Map);
            Session.SetFocus(Application.isFocused);
            RenderBase();
            RenderPlayer();
            RenderEnemies();
        }

        private void Update()
        {
            Session.AdvanceFrame(Time.unscaledDeltaTime, KeyboardInputSource.Read());
            mapView.Render(Session.Map); // No tile writes unless logical cell revisions changed.
            RenderBase();
            RenderPlayer();
            RenderEnemies();
            BulletView.Render();
            EffectView.Render();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (Session != null) Session.SetFocus(hasFocus);
        }

        private void OnDisable()
        {
            if (Session != null)
            {
                Session.SetPaused(true);
                Session.Bullets.Clear(); // Release owner leases, not just their display.
                if (BulletView != null) BulletView.Render();
            }
            HidePlayerShield();
            ClearEnemyDisplays();
            ClearEffects();
        }

        private void OnDestroy()
        {
            ClearEnemyDisplays();
            ClearEffects();
            // Destroying just the host component must not strand children referencing its old session.
            if (BulletView != null) Destroy(BulletView.gameObject);
            if (EffectView != null) Destroy(EffectView.gameObject);
            if (playerShield != null) { HidePlayerShield(); Destroy(playerShield.gameObject); }
            Session = null;
        }

        private void HidePlayerShield()
        {
            if (playerShield == null) return;
            playerShield.enabled = false;
            playerShield.sprite = null;
        }

        /// <summary>Lifecycle seam for reset/reload; does not implement a full session restart.
        /// Re-enabling remains paused and never replays the previous tick's hits.</summary>
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
                view.Render();
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
            playerRenderer.sprite = playerFrames[(int)Session.Player.Facing, Session.Player.TrackFrame];
            if (playerShield != null)
            {
                if (Session.PlayerTank.IsAlive && Session.PlayerLife.IsProtected)
                {
                    int age = Session.PlayerLife.Parameters.ProtectionTicks - Session.PlayerLife.ProtectionRemainingTicks;
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
            if (baseRenderer != null)
            {
                if (baseRenderer == playerRenderer) throw new InvalidOperationException("Base and player require distinct renderers.");
                sprites.Get("base.alive"); sprites.Get("base.dead");
            }
            if (enablePlayerLifecycle)
            {
                sprites.Get("effect.shield.0"); sprites.Get("effect.shield.1");
            }
            if (enemyWaveDefinition != null)
            {
                // Resolve every sequence ID now, not halfway through a future spawn callback.
                _ = new EnemyWaveState(level, enemyWaveDefinition);
                if (playerRenderer.sharedMaterial == null) throw new InvalidOperationException("Missing enemy display material.");
                foreach (var definition in enemyWaveDefinition.tankDefinitions)
                    for (int direction = 0; direction < 4; direction++) for (int frame = 0; frame < 2; frame++)
                        sprites.Get(definition.spritePrefix + "." + ((CardinalDirection)direction).SpriteKey() + "." + frame);
                for (int frame = 0; frame < 4; frame++) sprites.Get("effect.spawn." + frame);
            }
        }
    }
}
