using System.Collections.Generic;
using HS.Framework.Tests.Support;
using HS.Tactics.Character.Movement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>회전 속도가 유닛 정의의 값이고, 조립이 그것을 실제로 도는 구성요소에 옮기는 것을 고정한다.</summary>
    /// <remarks>
    /// 각속도가 빠른 유닛이 등 뒤의 적에 빨리 대처하려면 그 값이 유닛마다 달라야 하고, 그러려면 유닛 데이터에
    /// 있어야 한다. 이동 속도와 같은 길(정의 → 조립 → 구성요소)을 지나게 해 출처가 둘이 되지 않게 한다.
    /// </remarks>
    public sealed class UnitTurnSpeedTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheDefinitionCarriesATurnSpeedWithATemporaryDefault()
        {
            var definition = Track(UnitDefinition.CreateRuntime("소총병", 100));

            Assert.That(definition.TurnSpeed, Is.EqualTo(UnitDefinition.DefaultTurnSpeed));
            Assert.That(UnitDefinition.DefaultTurnSpeed, Is.EqualTo(120f), "기본값은 임시값이며, 바꾸면 이 검사와 함께 사용자에게 알린다.");
        }

        [Test]
        public void TheTurnSpeedNeverDropsBelowOneDegreePerSecond()
        {
            var definition = Track(UnitDefinition.CreateRuntime("소총병", 100, turnSpeed: 0f));

            Assert.That(definition.TurnSpeed, Is.EqualTo(1f), "0이면 영영 돌지 못해 조준이 끝나지 않는다.");
        }

        [Test]
        public void AssemblyCopiesTheTurnSpeedToTheFacingComponentThatActuallyTurns()
        {
            var unitObject = Track(new GameObject("Unit"));
            var facing = unitObject.AddComponent<CharacterFacing>();
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100, turnSpeed: 300f)));

            unit.InitializeUnit();

            Assert.That(
                facing.TurnSpeed,
                Is.EqualTo(300f),
                "실제로 도는 자리가 정의의 각속도를 모르면 유닛마다 다른 대응 속도가 나오지 않는다.");
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
