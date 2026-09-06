using System.Collections.Generic;
using HS.Tactics.Battlefield;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 스테이지 데이터의 기본값과 배치 항목 보존을 검증한다.
    /// 빈 목록은 null이 아니고, 길이와 레벨은 하한 아래로 내려가지 않으며, 항목 순서와 값은 유지된다.
    /// </summary>
    public sealed class StageDataTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void AFreshInstanceReadsWithoutThrowing()
        {
            var stage = Track(ScriptableObject.CreateInstance<StageData>());

            Assert.That(stage.UnitPlacements, Is.Not.Null, "빈 리스트는 null이 아니어야 한다. 읽는 쪽이 매번 null을 살피게 하지 않는다.");
            Assert.That(stage.UnitPlacements, Is.Empty);
            Assert.That(stage.BattlefieldLength, Is.EqualTo(1f), "길이의 기본값은 하한과 같다.");
        }

        [Test]
        public void PlacementsAreReadBackInOrderWithTheirValues()
        {
            var grunt = Track(UnitDefinition.CreateRuntime("Grunt", 100));
            var rifleman = Track(UnitDefinition.CreateRuntime("Rifleman", 120));
            var stage = Track(StageData.CreateRuntime(
                36f,
                new UnitPlacement(grunt, 1, new Vector3(24f, 0f, 3f)),
                new UnitPlacement(rifleman, 3, new Vector3(6f, 0f, -3f))));

            Assert.That(stage.BattlefieldLength, Is.EqualTo(36f));
            Assert.That(stage.UnitPlacements, Has.Count.EqualTo(2));
            Assert.That(stage.UnitPlacements[0].Definition, Is.SameAs(grunt));
            Assert.That(stage.UnitPlacements[0].Level, Is.EqualTo(1));
            Assert.That(stage.UnitPlacements[0].Position, Is.EqualTo(new Vector3(24f, 0f, 3f)));
            Assert.That(stage.UnitPlacements[1].Definition, Is.SameAs(rifleman));
            Assert.That(stage.UnitPlacements[1].Level, Is.EqualTo(3));
            Assert.That(stage.UnitPlacements[1].Position, Is.EqualTo(new Vector3(6f, 0f, -3f)));
        }

        [Test]
        public void LengthAndLevelDoNotGoBelowTheirFloors()
        {
            var grunt = Track(UnitDefinition.CreateRuntime("Grunt", 100));
            var stage = Track(StageData.CreateRuntime(0f, new UnitPlacement(grunt, 0, Vector3.zero)));

            Assert.That(stage.BattlefieldLength, Is.EqualTo(1f), "길이 0의 전장은 없다.");
            Assert.That(stage.UnitPlacements[0].Level, Is.EqualTo(1), "레벨 0의 유닛은 없다.");
        }

        [Test]
        public void NullPlacementsAreNotStored()
        {
            var stage = Track(StageData.CreateRuntime(10f, null, null));

            Assert.That(stage.UnitPlacements, Is.Empty, "null 항목이 들어가면 읽는 쪽이 스폰할 것 없는 자리를 만난다.");
        }

        [Test]
        public void ADefaultConstructedPlacementStartsAtLevelOne()
        {
            var placement = new UnitPlacement();

            Assert.That(placement.Definition, Is.Null);
            Assert.That(placement.Level, Is.EqualTo(1), "코드에서 기본 생성한 항목은 레벨 1이다. 직렬화가 0을 채워도 접근자가 1로 올리는 것은 하한 검사가 지킨다.");
            Assert.That(placement.Position, Is.EqualTo(Vector3.zero));
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
