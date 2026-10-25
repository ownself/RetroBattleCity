#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class StageBatchPlayModeTests
    {
        private Scene scene;
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator AuthoredCampaignCompletesAllThirtyFiveStagesOnOneController()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/SinglePlayer.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            c.enabled = false; c.SendMessage("OnApplicationFocus", true);
            var flow = c.singlePlayerFlow;
            var hostId = c.GetEntityId(); var bullets = c.BulletView; var effects = c.EffectView;
            int scenes = SceneManager.sceneCount; int frame = Time.frameCount;
            var shot = Object.Instantiate(c.playerDefinition);
            shot.bulletSpeedPixelsPerSecond = 6000; shot.fireCooldownTicks = 1; shot.maxActiveBullets = 100;
            try
            {
                flow.Route(c, PlayerButtons.Start, true); flow.Route(c, PlayerButtons.None, true);
                for (int stage = 1; stage <= 35; stage++)
                {
                    Assert.That(c.CurrentStageNumber, Is.EqualTo(stage));
                    Assert.That(flow.State.Screen, Is.EqualTo(SinglePlayerScreen.StageIntro));
                    var s = c.Session;
                    Assert.That(s.Score, Is.Zero); Assert.That(s.Result, Is.Null);
                    flow.Route(c, PlayerButtons.Start, true); flow.Route(c, PlayerButtons.None, true);
                    var gun = new WeaponState(shot, s.Bullets, CombatTeam.Player, 90000);
                    // Natural unmodified 20-enemy wave. Kill immediately at a controlled muzzle position;
                    // production sweep/HP/score/settlement still owns the outcome, not a forced phase.
                    for (int tick = 0; tick < 6000 && s.Phase == SessionPhase.Playing; tick++)
                    {
                        var enemy = s.Tanks.FirstOrDefault(t => t.Team == CombatTeam.Enemy && t.IsAlive);
                        if (enemy != null)
                            // Place the controlled muzzle inside the target footprint: some references have a wall
                            // directly below the spawn, so an external test muzzle can hit that wall instead.
                            gun.Step(new TankCommand(CardinalDirection.Up, true), enemy.Motor.Position - Vector2.up * 10, CardinalDirection.Up);
                        s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                    }
                    Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won), $"stage {stage} bounded completion");
                    Assert.That(s.Result.DefeatedCount, Is.EqualTo(20));
                    Assert.That(s.Result.StageNumber, Is.EqualTo(stage));
                    flow.Route(c, PlayerButtons.None, true); flow.Render(s, stage);
                    Assert.That(flow.State.Screen, Is.EqualTo(SinglePlayerScreen.Results));
                    flow.Route(c, PlayerButtons.Start, true); flow.Route(c, PlayerButtons.None, true);
                    Assert.That(c.GetEntityId(), Is.EqualTo(hostId));
                    Assert.That(c.BulletView, Is.SameAs(bullets)); Assert.That(c.EffectView, Is.SameAs(effects));
                    Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
                    yield return null;
                }
                Assert.That(flow.State.Screen, Is.EqualTo(SinglePlayerScreen.Complete));
                Assert.That(c.HasNextStage, Is.False); Assert.That(c.AdvanceStage(), Is.False);
                flow.Route(c, PlayerButtons.Start, true); flow.Route(c, PlayerButtons.None, true);
                Assert.That(c.CurrentStageNumber, Is.EqualTo(1));
                Assert.That(c.Session.Clock.Tick, Is.Zero); Assert.That(c.Session.Score, Is.Zero);
                Debug.Log($"BC402 35-stage same-host controlled 700 kills; frame {frame}->{Time.frameCount}; final complete and replay to 01");
            }
            finally { Object.Destroy(shot); }
        }

        [UnityTest] public IEnumerator AllThirtyFiveStagesRenderAndSpawnNaturalEnemiesWithoutOldTerrain()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/SinglePlayer.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            c.enabled = false;
            Assert.That(c.CampaignStageCount, Is.EqualTo(35));
            int frame = Time.frameCount;
            foreach (var level in c.campaign.stages)
            {
                var session = new ArenaSession(level, c.playerDefinition, c.enemyWaveDefinition, true,
                    PlayerLifeParameters.Prototype, true, c.playerUpgradeDefinition, c.pickupDefinition);
                Assert.That(session.StartBattle(), Is.True);
                session.AdvanceFrame(0, PlayerButtons.None);
                for (int i = 0; i < 62; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                Assert.That(session.Clock.Tick, Is.GreaterThanOrEqualTo(61));
                Assert.That(session.AliveEnemyCount, Is.EqualTo(1), $"Stage {level.stageNumber} natural first spawn");
                Assert.That(session.Base.IsAlive, Is.True);
                Assert.That(session.PlayerTank.IsAlive, Is.True);
                c.mapView.Render(session.Map); c.hud.Render(session, level.stageNumber);
                Assert.That(c.hud.DisplayedStage, Is.EqualTo(level.stageNumber));
                for (int y = 0; y < 26; y++) for (int x = 0; x < 26; x++)
                {
                    var kind = session.Map.TerrainAt(x, y); var cell = new Vector3Int(x, y, 0);
                    Assert.That(c.mapView.obstacles.HasTile(cell), Is.EqualTo(kind == TerrainKind.Brick || kind == TerrainKind.Steel));
                    Assert.That(c.mapView.Foliage.HasTile(cell), Is.EqualTo(kind == TerrainKind.Grass));
                    Assert.That(c.mapView.Surfaces.HasTile(cell), Is.EqualTo(kind == TerrainKind.Water || kind == TerrainKind.Ice));
                }
                session.Bullets.Clear(); session.Effects.Clear(); session.SetPaused(true);
                yield return null;
            }
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Debug.Log($"BC402 all stages 01..35 actual frame {frame}->{Time.frameCount}, each 62 fixed ticks, natural spawn and 676 displayed cells verified");
        }
    }
}
#endif
