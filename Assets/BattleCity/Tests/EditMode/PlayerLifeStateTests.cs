using System;
using NUnit.Framework;

namespace BattleCity.Tests
{
    public sealed class PlayerLifeStateTests
    {
        private static bool Spawn() => true;
        private static bool Blocked() => false;
        private static PlayerLifeState Prototype() => new PlayerLifeState(PlayerLifeParameters.Prototype);
        private static string Snapshot(PlayerLifeState life) =>
            $"{life.RemainingLives}|{life.IsAlive}|{life.RespawnRemainingTicks}|{life.ProtectionRemainingTicks}|" +
            $"{life.IsExhausted}|{life.IsWaitingForRespawn}|{life.IsRespawnReady}|{life.IsProtected}";

        [Test]
        public void InitialLife_IncludesCurrentAndHasNoProtectionOrPendingRespawn()
        {
            var life = Prototype();
            Assert.That(life.RemainingLives, Is.EqualTo(3));
            Assert.That(life.IsAlive, Is.True);
            Assert.That(life.IsProtected, Is.False);
            Assert.That(life.ProtectionRemainingTicks, Is.Zero);
            Assert.That(life.RespawnRemainingTicks, Is.Zero);
            Assert.That(life.IsWaitingForRespawn, Is.False);
            Assert.That(life.IsRespawnReady, Is.False);
            Assert.That(life.IsExhausted, Is.False);
            string initial = Snapshot(life);
            for (int tick = 1; tick <= 200; tick++)
            {
                var result = life.Step(true, () => { Assert.Fail("Alive player cannot respawn"); return true; });
                Assert.That(result.DeathRecorded || result.Respawned || result.LivesExhaustedThisTick, Is.False);
            }
            Assert.That(Snapshot(life), Is.EqualTo(initial));
        }

        [Test]
        public void AbsoluteTicks_DeathDDoesNotDecrement_DPlus59Waits_DPlus60FirstRespawn()
        {
            const int deathTick = 37;
            var life = Prototype();
            int callbacks = 0;
            for (int tick = 1; tick < deathTick; tick++) life.Step(true, Spawn);
            var death = life.Step(false, Spawn); // D = 37
            Assert.That(death.DeathRecorded, Is.True);
            Assert.That(death.Respawned || death.LivesExhaustedThisTick, Is.False);
            Assert.That(life.RemainingLives, Is.EqualTo(2));
            Assert.That(life.RespawnRemainingTicks, Is.EqualTo(60));
            Assert.That(life.IsWaitingForRespawn, Is.True);
            for (int tick = deathTick + 1; tick <= deathTick + 59; tick++)
            {
                var result = life.Step(false, () => { callbacks++; return true; });
                Assert.That(result.DeathRecorded || result.Respawned || result.LivesExhaustedThisTick, Is.False);
                Assert.That(life.RespawnRemainingTicks, Is.EqualTo(deathTick + 60 - tick));
                Assert.That(life.IsAlive || life.IsRespawnReady, Is.False);
                Assert.That(life.RemainingLives, Is.EqualTo(2));
            }
            Assert.That(callbacks, Is.Zero);
            var respawn = life.Step(false, () =>
            {
                callbacks++;
                Assert.That(life.IsRespawnReady, Is.True);
                Assert.That(life.IsAlive || life.IsProtected, Is.False); // Not committed inside callback.
                Assert.That(life.RemainingLives, Is.EqualTo(2));
                return true;
            }); // D+60 = R = 97
            Assert.That(respawn.Respawned, Is.True);
            Assert.That(respawn.DeathRecorded || respawn.LivesExhaustedThisTick, Is.False);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(life.IsAlive && life.IsProtected, Is.True);
            Assert.That(life.RemainingLives, Is.EqualTo(2));
            Assert.That(life.RespawnRemainingTicks, Is.Zero);
            Assert.That(life.ProtectionRemainingTicks, Is.EqualTo(120));
        }

        [Test]
        public void AbsoluteTicks_RPlus1ThroughRPlus120FullDamagePhasesProtected_RPlus121FirstVulnerable()
        {
            const int deathTick = 37;
            const int respawnTick = deathTick + 60;
            var life = Prototype();
            for (int tick = 1; tick < deathTick; tick++) life.Step(true, Spawn);
            life.Step(false, Spawn);
            for (int tick = deathTick + 1; tick <= respawnTick; tick++) life.Step(false, Spawn);
            Assert.That(life.ProtectionRemainingTicks, Is.EqualTo(120)); // R has no repeated damage phase.
            for (int tick = respawnTick + 1; tick <= respawnTick + 120; tick++)
            {
                Assert.That(life.IsProtected, Is.True, "Damage phase at absolute tick " + tick);
                Assert.That(life.ProtectionRemainingTicks, Is.EqualTo(respawnTick + 121 - tick));
                life.Step(true, Spawn); // After damage, not before it.
                Assert.That(life.ProtectionRemainingTicks, Is.EqualTo(respawnTick + 120 - tick));
            }
            Assert.That(life.IsProtected, Is.False, "R+121 = absolute tick 218 is first vulnerable damage phase");
            Assert.That(life.Step(false, Spawn).DeathRecorded, Is.True);
            Assert.That(life.RemainingLives, Is.EqualTo(1));
            Assert.That(life.ProtectionRemainingTicks, Is.Zero);
            Assert.That(life.RespawnRemainingTicks, Is.EqualTo(60));
        }

        [Test]
        public void ReadyButBlocked_RetriesEachTickAtZeroWithoutSpendingLifeOrRestartingDelay()
        {
            var life = Prototype(); life.Step(false, Spawn);
            for (int i = 1; i < 60; i++) life.Step(false, Spawn);
            int callbacks = 0;
            for (int i = 0; i < 25; i++)
            {
                var result = life.Step(false, () => { callbacks++; return false; });
                Assert.That(life.IsRespawnReady, Is.True);
                Assert.That(life.RespawnRemainingTicks, Is.Zero);
                Assert.That(life.ProtectionRemainingTicks, Is.Zero);
                Assert.That(life.RemainingLives, Is.EqualTo(2));
                Assert.That(result.DeathRecorded || result.Respawned || result.LivesExhaustedThisTick, Is.False);
            }
            Assert.That(callbacks, Is.EqualTo(25));
            Assert.That(life.Step(false, Spawn).Respawned, Is.True);
            Assert.That(life.IsWaitingForRespawn || life.IsRespawnReady, Is.False);
            Assert.That(life.RemainingLives, Is.EqualTo(2));
        }

        [Test]
        public void ThreeLives_DeathSpendsOnePerGeneration_ExhaustionEdgeOnceAndLatchForever()
        {
            var life = new PlayerLifeState(new PlayerLifeParameters(3, 1, 1));
            int deaths = 0, respawns = 0, exhaustionEdges = 0;
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                var result = life.Step(false, Spawn);
                if (result.DeathRecorded) deaths++;
                if (result.LivesExhaustedThisTick) exhaustionEdges++;
                Assert.That(life.RemainingLives, Is.EqualTo(remaining));
                Assert.That(life.IsExhausted, Is.EqualTo(remaining == 0));
                Assert.That(life.IsAlive, Is.False);
                if (remaining == 0) break;
                if (life.Step(false, Spawn).Respawned) respawns++;
                life.Step(true, Spawn); // Expire the single complete protected damage phase.
            }
            Assert.That(deaths, Is.EqualTo(3));
            Assert.That(respawns, Is.EqualTo(2));
            Assert.That(exhaustionEdges, Is.EqualTo(1));
            string exhausted = Snapshot(life);
            for (int tick = 0; tick < 500; tick++)
            {
                var result = life.Step(false, () => { Assert.Fail("Exhausted cannot respawn"); return true; });
                Assert.That(result.DeathRecorded || result.Respawned || result.LivesExhaustedThisTick, Is.False);
                Assert.That(Snapshot(life), Is.EqualTo(exhausted));
            }
            Assert.That(life.IsWaitingForRespawn || life.IsRespawnReady || life.IsProtected, Is.False);
        }

        [Test]
        public void RepeatedDeadSnapshotsAndReadOnlyObservations_DoNotSpendAdditionalLives()
        {
            var life = Prototype();
            life.Step(false, Spawn);
            for (int tick = 1; tick < 60; tick++)
            {
                Assert.That(life.Step(false, Blocked).DeathRecorded, Is.False);
                string snapshot = Snapshot(life);
                for (int observation = 0; observation < 50; observation++)
                    Assert.That(Snapshot(life), Is.EqualTo(snapshot));
                Assert.That(life.RemainingLives, Is.EqualTo(2));
            }
            for (int tick = 0; tick < 10; tick++) Assert.That(life.Step(false, Blocked).DeathRecorded, Is.False);
            Assert.That(life.RemainingLives, Is.EqualTo(2));
        }

        [Test]
        public void NoStepDuringPauseOrFocusLoss_LeavesWaitAndProtectionUnchanged_NoCatchUpClock()
        {
            var life = new PlayerLifeState(new PlayerLifeParameters(3, 1, 1));
            life.Step(false, Spawn);
            string waiting = Snapshot(life);
            for (int renderFrame = 0; renderFrame < 1000; renderFrame++)
                Assert.That(Snapshot(life), Is.EqualTo(waiting));
            Assert.That(life.Step(false, Spawn).Respawned, Is.True);
            string protectedState = Snapshot(life);
            for (int renderFrame = 0; renderFrame < 1000; renderFrame++)
                Assert.That(Snapshot(life), Is.EqualTo(protectedState));
            Assert.That(life.IsProtected, Is.True);
            life.Step(true, Spawn);
            Assert.That(life.IsProtected, Is.False);
        }

        [Test]
        public void OneTickDelayAndProtection_DeathDoesNotSpawn_RespawnDoesNotExpireProtection()
        {
            var life = new PlayerLifeState(new PlayerLifeParameters(2, 1, 1));
            life.Step(false, () => { Assert.Fail("No respawn in death tick"); return true; });
            Assert.That(life.RespawnRemainingTicks, Is.EqualTo(1));
            Assert.That(life.Step(false, Spawn).Respawned, Is.True);
            Assert.That(life.ProtectionRemainingTicks, Is.EqualTo(1));
            Assert.That(life.IsProtected, Is.True);
            life.Step(true, Spawn);
            Assert.That(life.ProtectionRemainingTicks, Is.Zero);
            Assert.That(life.Step(false, Spawn).LivesExhaustedThisTick, Is.True);
            Assert.That(life.RespawnRemainingTicks, Is.Zero);
        }

        [Test]
        public void NullCallbackAndUnauthorizedRevival_AreRejectedWithoutChangingState()
        {
            var life = Prototype();
            string initial = Snapshot(life);
            Assert.Throws<ArgumentNullException>(() => life.Step(false, null));
            Assert.That(Snapshot(life), Is.EqualTo(initial));
            life.Step(false, Spawn);
            string dead = Snapshot(life);
            Assert.Throws<InvalidOperationException>(() => life.Step(true, Spawn));
            Assert.That(Snapshot(life), Is.EqualTo(dead));
        }

        [Test]
        public void ThrowingOrReentrantCallback_DoesNotCommitAndCanRetryOnNextTick()
        {
            var life = new PlayerLifeState(new PlayerLifeParameters(3, 1, 1));
            life.Step(false, Spawn);
            Assert.Throws<InvalidOperationException>(() => life.Step(false, () => throw new InvalidOperationException("host failure")));
            Assert.That(life.IsRespawnReady, Is.True);
            Assert.That(life.RemainingLives, Is.EqualTo(2));
            Assert.That(life.IsAlive || life.IsProtected, Is.False);
            string ready = Snapshot(life);
            Assert.Throws<InvalidOperationException>(() => life.Step(false, () => life.Step(false, Spawn).Respawned));
            Assert.That(Snapshot(life), Is.EqualTo(ready));
            Assert.That(life.Step(false, Spawn).Respawned, Is.True);
            Assert.That(life.RemainingLives, Is.EqualTo(2));
        }

        [Test]
        public void SeparateStates_DoNotShareMutableLifeOrTimerState()
        {
            var first = Prototype(); var second = Prototype();
            string initial = Snapshot(second);
            first.Step(false, Spawn);
            for (int i = 0; i < 60; i++) first.Step(false, Spawn);
            Assert.That(first.IsProtected, Is.True);
            Assert.That(Snapshot(second), Is.EqualTo(initial));
        }
    }
}
