using HS.Framework.Gameplay.Teams;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>유닛 정의가 잘못된 수치를 안전한 값으로 보정하는지 검증한다.</summary>
    public sealed class UnitDefinitionTests
    {
        private UnitDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
                _definition = null;
            }
        }

        [Test]
        public void CreateRuntimeKeepsGivenValues()
        {
            _definition = UnitDefinition.CreateRuntime(
                "소총병",
                120,
                new TeamId(1),
                4.5f,
                12f,
                15,
                0.8f);

            Assert.That(_definition.DisplayName, Is.EqualTo("소총병"));
            Assert.That(_definition.MaxHealth, Is.EqualTo(120));
            Assert.That(_definition.DefaultTeam, Is.EqualTo(new TeamId(1)));
            Assert.That(_definition.MoveSpeed, Is.EqualTo(4.5f));
            Assert.That(_definition.AttackRange, Is.EqualTo(12f));
            Assert.That(_definition.AttackDamage, Is.EqualTo(15));
            Assert.That(_definition.AttackInterval, Is.EqualTo(0.8f));
            Assert.That(_definition.HasUnitPrefab, Is.False);
        }

        [Test]
        public void InvalidNumbersAreClampedToUsableValues()
        {
            _definition = UnitDefinition.CreateRuntime(
                "깨진 유닛",
                0,
                default,
                0f,
                0f,
                0,
                0f);

            Assert.That(_definition.MaxHealth, Is.EqualTo(1));
            Assert.That(_definition.MoveSpeed, Is.EqualTo(0.1f));
            Assert.That(_definition.AttackRange, Is.EqualTo(0.1f));
            Assert.That(_definition.AttackDamage, Is.EqualTo(1));
            Assert.That(_definition.AttackInterval, Is.EqualTo(0.05f));
        }

        [Test]
        public void EmptyDisplayNameFallsBackToTheAssetName()
        {
            _definition = UnitDefinition.CreateRuntime("   ", 100);
            _definition.name = "Rifleman";

            Assert.That(_definition.DisplayName, Is.EqualTo("Rifleman"));
        }

        [Test]
        public void DefaultTeamIsUnassignedUntilItIsGiven()
        {
            _definition = UnitDefinition.CreateRuntime("무소속", 100);

            Assert.That(_definition.DefaultTeam.IsAssigned, Is.False);
        }
    }
}
