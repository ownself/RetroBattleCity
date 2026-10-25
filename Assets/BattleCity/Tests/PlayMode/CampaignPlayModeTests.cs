#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class CampaignPlayModeTests
    {
        private GameObject host;
        private Keyboard keyboard;
        private LevelDefinition first, second;
        private CampaignDefinition campaign;
        private EnemyWaveDefinition wave;
        private TankDefinition enemy, shot;
        private ArenaController c;
        private void Keys(params Key[] keys)
        {
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
        }
        private IEnumerator Press(params Key[] keys) { Keys(); yield return null; Keys(keys); yield return null; }
        private static IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "bounded real Update");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (host != null) Object.Destroy(host);
            yield return null;
            foreach (var item in new Object[] { first, second, campaign, wave, enemy, shot }) if (item != null) Object.Destroy(item);
            LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Load()
        {
            Keys();
            // Load the authored hierarchy inactive to supply two transient interactive fixtures before Awake.
            var preview = EditorSceneManager.OpenPreviewScene("Assets/BattleCity/Scenes/SinglePlayer.unity");
            try
            {
                var roots = preview.GetRootGameObjects();
                var wrapper = new GameObject("BC401 transient host"); wrapper.SetActive(false);
                SceneManager.MoveGameObjectToScene(wrapper, preview);
                foreach (var root in roots) root.transform.SetParent(wrapper.transform, true);
                host = Object.Instantiate(wrapper);
                SceneManager.MoveGameObjectToScene(host, SceneManager.GetActiveScene());
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            c = host.GetComponentsInChildren<ArenaController>(true).Single();
            first = ScriptableObject.CreateInstance<LevelDefinition>(); first.stageNumber = 11;
            first.playerSpawnPixels = new Vector2Int(24, 24);
            first.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            first.enemySequence = new[] { "enemy.basic.prototype" };
            second = Object.Instantiate(first); second.stageNumber = 23;
            second.terrain[12 * 26 + 12] = TerrainKind.Water;
            first.terrain[12 * 26 + 12] = TerrainKind.Grass;
            enemy = Object.Instantiate(c.enemyWaveDefinition.tankDefinitions[0]);
            enemy.definitionId = "enemy.basic.prototype"; enemy.hitPoints = 1;
            enemy.moveSpeedPixelsPerSecond = .001f;
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            wave.tankDefinitions = new[] { enemy }; wave.preparationTicks = 1;
            campaign = ScriptableObject.CreateInstance<CampaignDefinition>(); campaign.stages = new[] { first, second };
            c.campaign = campaign; c.level = first; c.enemyWaveDefinition = wave; c.pickupDefinition = null;
            shot = Object.Instantiate(c.playerDefinition); shot.bulletSpeedPixelsPerSecond = 6000;
            host.SetActive(true); yield return null;
            c.SendMessage("OnApplicationFocus", true);
        }
        private IEnumerator Win()
        {
            var s = c.Session;
            yield return Until(() => s.AliveEnemyCount == 1);
            var target = s.Tanks.Last();
            var gun = new WeaponState(shot, s.Bullets, CombatTeam.Player, 9999);
            gun.Step(new TankCommand(CardinalDirection.Down, true), target.Motor.Position + Vector2.up * 18, CardinalDirection.Down);
            yield return Until(() => c.singlePlayerFlow.State.Screen == SinglePlayerScreen.Results);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won));
        }
        [UnityTest] public IEnumerator TwoStagesSameHostSnapshotIsolationRestartAndFinalCompletion()
        {
            yield return Load();
            int scenes = SceneManager.sceneCount; var id = c.GetEntityId();
            var view = c.BulletView; var effect = c.EffectView; var map = c.mapView;
            // Authoring changes after Awake must not change future loads or count/order.
            second.stageNumber = 99; second.terrain[12 * 26 + 12] = TerrainKind.Brick;
            campaign.stages = new[] { first };
            Assert.That(c.AdvanceStage(), Is.False);
            yield return Press(Key.Enter); yield return Press(Key.Enter);
            yield return Win();
            var old = c.Session; var result = old.Result;
            Assert.That(c.HasNextStage, Is.True);
            Assert.That(map.Foliage.HasTile(new Vector3Int(12, 12, 0)), Is.True);
            Assert.That(map.Surfaces.HasTile(new Vector3Int(12, 12, 0)), Is.False);
            yield return Press(Key.Enter, Key.Space, Key.D);
            yield return Until(() => c.CurrentStageNumber == 23);
            Assert.That(c.singlePlayerFlow.State.Screen, Is.EqualTo(SinglePlayerScreen.StageIntro));
            Assert.That(c.Session.Clock.Tick, Is.Zero);
            Assert.That(c.Session.Map.TerrainAt(12, 12), Is.EqualTo(TerrainKind.Water));
            Assert.That(map.Surfaces.HasTile(new Vector3Int(12, 12, 0)), Is.True);
            Assert.That(map.Foliage.HasTile(new Vector3Int(12, 12, 0)), Is.False, "old stage grass must not cover the new map");
            Assert.That(c.Session.Score, Is.Zero); Assert.That(c.Session.Result, Is.Null);
            Assert.That(c.Session.PlayerLife.RemainingLives, Is.EqualTo(3));
            Assert.That(c.Session.PlayerUpgrades.Level, Is.Zero);
            Assert.That(old.Clock.Paused, Is.True); Assert.That(old.Bullets.Active, Is.Empty);
            Assert.That(result.DefeatedCount, Is.EqualTo(1));
            Assert.That(c.BulletView, Is.SameAs(view)); Assert.That(c.EffectView, Is.SameAs(effect));
            Assert.That(c.mapView, Is.SameAs(map)); Assert.That(c.GetEntityId(), Is.EqualTo(id));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(c.Session.Clock.Tick, Is.Zero, "held confirm must not skip stage intro");
            yield return Press(Key.Enter); yield return Until(() => c.Session.Clock.Tick > 0);
            Assert.That(c.hud.DisplayedStage, Is.EqualTo(23));
            Assert.That(c.Session.PlayerWeapon.ActiveBullets, Is.Zero);
            yield return Press(Key.R);
            Assert.That(c.CurrentStageNumber, Is.EqualTo(23));
            Assert.That(c.singlePlayerFlow.State.Screen, Is.EqualTo(SinglePlayerScreen.StageIntro));
            yield return Press(Key.Enter); yield return Win();
            Assert.That(c.HasNextStage, Is.False);
            yield return Press(Key.Enter);
            Assert.That(c.singlePlayerFlow.State.Screen, Is.EqualTo(SinglePlayerScreen.Complete));
            Assert.That(c.singlePlayerFlow.heading.Text, Is.EqualTo("CAMPAIGN COMPLETE"));
            yield return Press(Key.Enter);
            Assert.That(c.CurrentStageNumber, Is.EqualTo(11)); Assert.That(c.CampaignIndex, Is.Zero);
            Assert.That(c.Session.Clock.Tick, Is.Zero);
            Assert.That(map.Foliage.HasTile(new Vector3Int(12, 12, 0)), Is.True);
            Assert.That(map.Surfaces.HasTile(new Vector3Int(12, 12, 0)), Is.False);
            // Losing a later stage must offer a fresh campaign, never accidentally advance.
            yield return Press(Key.Enter); yield return Win(); yield return Press(Key.Enter);
            yield return Press(Key.Enter); yield return Until(() => c.Session.Clock.Tick > 0);
            var later = c.Session;
            var baseShot = new WeaponState(shot, later.Bullets, CombatTeam.Enemy, 8888);
            baseShot.Step(new TankCommand(CardinalDirection.Down, true), later.Base.Position + Vector2.up * 10, CardinalDirection.Down);
            yield return Until(() => c.singlePlayerFlow.State.Screen == SinglePlayerScreen.Results);
            Assert.That(later.Phase, Is.EqualTo(SessionPhase.Lost));
            Assert.That(c.AdvanceStage(), Is.False);
            yield return Press(Key.Enter);
            Assert.That(c.singlePlayerFlow.State.Screen, Is.EqualTo(SinglePlayerScreen.GameOver));
            yield return Press(Key.DownArrow); yield return Press(Key.Enter);
            Assert.That(c.singlePlayerFlow.State.Screen, Is.EqualTo(SinglePlayerScreen.Title));
            Assert.That(c.CurrentStageNumber, Is.EqualTo(11));
            Assert.That(c.Session.Score, Is.Zero);
            Debug.Log($"BC401 same host {id}, stages 11->23->11, real Update frame {Time.frameCount}, no scene duplication");
        }
    }
}
#endif
