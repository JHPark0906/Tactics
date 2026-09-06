using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Flow;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 유닛 정의가 없어 체력 어트리뷰트에 아직 묶이지 못한 유닛이 꺼질 때도 회수 경로가 도는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <see cref="BattleUnitRegistrant.OnDisable"/>은 사망이 확정됐을 때만 등록 해제를 건너뛴다.
    /// <c>HealthAttributeComponent</c>는 체력 어트리뷰트에 묶이기 전에는 <c>IsDead</c>를 물어도 늘
    /// "살아 있음"으로만 답하고, 묻는 것 자체가 <c>EnsureBound</c>의 오류 로그를 낸다 — 그래서 묶이지
    /// 않았으면 그 답을 사망 확정으로 쓰지 않고 곧바로 등록을 해제해야 한다. 유닛 정의가 없는 채로
    /// 조립되는 것은 <c>TacticalUnit.ApplyDefinition</c>이 이미 경고와 함께 감당하는 정상 경로이므로,
    /// 그런 유닛이 등록 해제 도중에 죽었는지 잘못 묻지 않는 것도 정상 경로의 일부다.
    /// </remarks>
    public sealed class BattleUnitRegistrantOnDisableTests
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
        public void ADefinitionLessUnitStillUnregistersOnDisable()
        {
            var unitObject = Track(new GameObject("Unit"));
            var unit = unitObject.AddComponent<TacticalUnit>();

            LogAssert.Expect(LogType.Warning, new Regex("유닛 정의가 없어"));
            unit.InitializeUnit();

            var registrant = unit.GetComponent<BattleUnitRegistrant>();
            var registry = new FakeRegistry();
            registrant.InjectRegistry(registry);
            Assert.That(registrant.IsRegistered, Is.True, "무대 확인: 등록이 먼저 성공해야 해제를 볼 수 있다.");

            // 에디터는 플레이 모드가 아닐 때 OnDisable을 호출하지 않으므로, SetActive(false)만으로는
            // 이 컴포넌트의 OnDisable이 돌지 않는다 — MonoBehaviourLifecycle로 직접 구동한다.
            MonoBehaviourLifecycle.InvokeOnDisable(registrant);

            Assert.That(
                registrant.IsRegistered, Is.False,
                "체력이 어트리뷰트에 안 묶였으면 사망을 확정할 수 없다 — 회수 경로를 막지 않아야 한다.");
            Assert.That(
                registry.UnregisteredObjects, Has.Member(unitObject),
                "등록 해제가 실제로 레지스트리까지 전달돼야 한다.");
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>등록·해제 호출만 그대로 받아 적는 검사용 집계이다.</summary>
        private sealed class FakeRegistry : IBattleUnitRegistry
        {
            public List<GameObject> UnregisteredObjects { get; } = new();

            public bool RegisterUnit(TacticalUnit unit) => true;

            public bool RegisterUnit(GameObject unitObject, TeamId teamId) => true;

            public bool UnregisterUnit(GameObject unitObject)
            {
                UnregisteredObjects.Add(unitObject);
                return true;
            }
        }
    }
}
