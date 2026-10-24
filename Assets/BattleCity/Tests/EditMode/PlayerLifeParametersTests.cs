using System;
using System.Linq;
using NUnit.Framework;

namespace BattleCity.Tests
{
    public sealed class PlayerLifeParametersTests
    {
        [Test]
        public void Prototype_IsImmutableThreeTotalLivesAnd60Delay120Protection_RequiresCalibration()
        {
            var parameters = PlayerLifeParameters.Prototype;
            Assert.That(parameters.InitialLives, Is.EqualTo(3));
            Assert.That(parameters.RespawnDelayTicks, Is.EqualTo(60));
            Assert.That(parameters.ProtectionTicks, Is.EqualTo(120));
            Assert.That(parameters.RequiresCalibration, Is.True);
            Assert.That(typeof(PlayerLifeParameters).IsSealed, Is.True);
            Assert.That(typeof(PlayerLifeParameters).GetProperties().All(p => p.SetMethod == null), Is.True);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
        public void InitialLives_MustBePositive(int value)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerLifeParameters(value, 60, 120));
            Assert.That(error.ParamName, Is.EqualTo("initialLives"));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
        public void RespawnDelayTicks_RejectsZeroAndNegative(int value)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerLifeParameters(3, value, 120));
            Assert.That(error.ParamName, Is.EqualTo("respawnDelayTicks"));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
        public void ProtectionTicks_RejectsZeroAndNegative(int value)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerLifeParameters(3, 60, value));
            Assert.That(error.ParamName, Is.EqualTo("protectionTicks"));
        }

        [Test]
        public void State_CopiesImmutableParameterSnapshot_IndependentOfCallerReferenceReplacement()
        {
            var parameters = new PlayerLifeParameters(4, 7, 9);
            var life = new PlayerLifeState(parameters);
            Assert.That(life.Parameters, Is.Not.SameAs(parameters));
            parameters = new PlayerLifeParameters(1, 1, 1);
            Assert.That(life.RemainingLives, Is.EqualTo(4));
            Assert.That(life.Parameters.InitialLives, Is.EqualTo(4));
            Assert.That(life.Parameters.RespawnDelayTicks, Is.EqualTo(7));
            Assert.That(life.Parameters.ProtectionTicks, Is.EqualTo(9));
            Assert.That(parameters.InitialLives, Is.EqualTo(1));
        }

        [Test]
        public void MaximumPositiveValues_AreSupportedWithoutTimerArithmeticOverflow()
        {
            var life = new PlayerLifeState(new PlayerLifeParameters(int.MaxValue, int.MaxValue, int.MaxValue));
            life.Step(false, () => true);
            Assert.That(life.RemainingLives, Is.EqualTo(int.MaxValue - 1));
            Assert.That(life.RespawnRemainingTicks, Is.EqualTo(int.MaxValue));
            life.Step(false, () => true);
            Assert.That(life.RespawnRemainingTicks, Is.EqualTo(int.MaxValue - 1));
        }

        [Test]
        public void NullParameters_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerLifeState(null));
        }
    }
}
