using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>등록된 유닛의 자리와 반지름을 원으로 얻는 <c>TryGetCircle</c>을 검증한다.</summary>
    /// <remarks>
    /// <c>CollectOverlapping</c>은 누가 겹치는지만 알려 준다. 유닛간 걸음 자르기처럼 그 유닛의 정확한
    /// 원(중심·반지름)이 다시 필요한 소비자를 위한 조회다.
    /// </remarks>
    public sealed class UnitSpatialRegistryTryGetCircleTests
    {
        private readonly List<Object> _created = new();
        private UnitSpatialRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var registryObject = new GameObject("Registry");
            _created.Add(registryObject);
            _registry = registryObject.AddComponent<UnitSpatialRegistry>();
        }

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
        public void ReturnsTheRegisteredPositionAndRadius()
        {
            var unitObject = new GameObject("Unit");
            _created.Add(unitObject);
            unitObject.transform.position = new Vector3(3f, 0f, 4f);
            var team = unitObject.AddComponent<TeamMember>();
            _registry.Register(team, unitObject.transform, 0.75f);

            var found = _registry.TryGetCircle(team, out var circle);

            Assert.That(found, Is.True);
            Assert.That(circle.Center.X, Is.EqualTo(3f));
            Assert.That(circle.Center.Z, Is.EqualTo(4f));
            Assert.That(circle.Radius, Is.EqualTo(0.75f));
        }

        [Test]
        public void ReadsTheCurrentPositionEachCallRatherThanACachedOne()
        {
            var unitObject = new GameObject("Unit");
            _created.Add(unitObject);
            var team = unitObject.AddComponent<TeamMember>();
            _registry.Register(team, unitObject.transform, 0.5f);

            unitObject.transform.position = new Vector3(10f, 0f, -2f);
            _registry.TryGetCircle(team, out var circle);

            Assert.That(circle.Center.X, Is.EqualTo(10f), "위치를 따로 갱신받지 않으므로 조회 시점의 실제 transform 값이어야 한다.");
            Assert.That(circle.Center.Z, Is.EqualTo(-2f));
        }

        [Test]
        public void AnUnregisteredTeamReturnsFalse()
        {
            var strangerObject = new GameObject("Stranger");
            _created.Add(strangerObject);
            var stranger = strangerObject.AddComponent<TeamMember>();

            var found = _registry.TryGetCircle(stranger, out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void AfterUnregisterTheQueryReturnsFalseAgain()
        {
            var unitObject = new GameObject("Unit");
            _created.Add(unitObject);
            var team = unitObject.AddComponent<TeamMember>();
            _registry.Register(team, unitObject.transform, 0.5f);
            _registry.Unregister(team);

            var found = _registry.TryGetCircle(team, out _);

            Assert.That(found, Is.False);
        }
    }
}
