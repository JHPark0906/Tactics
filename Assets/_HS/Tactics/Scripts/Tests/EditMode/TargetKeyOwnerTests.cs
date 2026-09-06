using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Behaviour;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 표적 문맥 키는 탐지 서비스만 쓰고 지우는지 검증한다.
    /// 서비스 키를 기본값과 다르게 지정해 탐지기가 별도 기본 키에 쓰는 경우를 구별한다.
    /// 어떤 키 이름을 사용할지는 트리 정의가 정하며, 이 검사는 선택된 키의 소유권을 확인한다.
    /// </summary>
    public sealed class TargetKeyOwnerTests
    {
        /// <summary>기본값과 다른 키이며, 갈라짐을 드러내려고 일부러 다르게 둔다.</summary>
        private const string CustomKey = "Unit.Target.Custom";

        private readonly List<Object> _created = new();
        private UnitSpatialRegistry _registry;

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
        public void OnlyTheConfiguredKeyIsFilled()
        {
            var unit = CreateUnit();
            unit.InitializeUnit();
            CreateEnemy(new Vector3(0f, 0f, 5f));

            RunService(unit, CustomKey);

            Assert.That(
                unit.BehaviourContext.TryGetValue(CustomKey, out Transform _),
                Is.True,
                "서비스가 지정받은 키에 담아야 한다.");
            Assert.That(
                unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.Target, out Transform _),
                Is.False,
                "탐지기가 자기 키에 따로 쓰면 키 둘이 채워진다.");
        }

        [Test]
        public void OnlyTheConfiguredKeyIsClearedWhenTheEnemyIsGone()
        {
            var unit = CreateUnit();
            unit.InitializeUnit();
            var enemy = CreateEnemy(new Vector3(0f, 0f, 5f));
            RunService(unit, CustomKey);

            Object.DestroyImmediate(enemy);
            RunService(unit, CustomKey);

            Assert.That(
                unit.BehaviourContext.TryGetValue(CustomKey, out Transform _),
                Is.False,
                "적이 사라지면 서비스가 자기 키를 지워야 한다.");
            Assert.That(
                unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.Target, out Transform _),
                Is.False,
                "지우는 쪽이 한쪽만 지우면 사라진 적이 다른 키에 남는다.");
        }

        [Test]
        public void TheDetectorAnswersWithoutTouchingAnyKey()
        {
            // 탐지기를 직접 불러도 문맥은 그대로여야 한다. 담는 일은 서비스 몫이다.
            var unit = CreateUnit();
            var detector = unit.GetComponent<EnemyDetector>();
            unit.InitializeUnit();
            var enemy = CreateEnemy(new Vector3(0f, 0f, 5f));

            var answered = detector.RefreshTarget();

            Assert.That(answered, Is.SameAs(enemy.transform), "탐지기는 찾은 것을 답해야 한다.");
            Assert.That(detector.CurrentTarget, Is.SameAs(enemy.transform));
            Assert.That(
                unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.Target, out Transform _),
                Is.False,
                "탐지기는 어느 키에도 쓰지 않는다.");
        }

        [Test]
        public void ClearingTheDetectorLeavesTheKeyToItsOwner()
        {
            var unit = CreateUnit();
            var detector = unit.GetComponent<EnemyDetector>();
            unit.InitializeUnit();
            CreateEnemy(new Vector3(0f, 0f, 5f));
            RunService(unit, CustomKey);

            detector.ClearTarget();

            Assert.That(detector.CurrentTarget, Is.Null, "탐지기의 답은 비워진다.");
            Assert.That(
                unit.BehaviourContext.TryGetValue(CustomKey, out Transform _),
                Is.True,
                "탐지기가 남의 키를 지우면 서비스가 정한 임자가 둘이 된다. 지우는 것은 다음 실행이다.");
        }

        /// <summary>지정한 키로 탐지 서비스를 한 번 돌린다.</summary>
        /// <param name="unit">서비스를 돌릴 유닛이다.</param>
        /// <param name="targetKey">서비스가 쓸 문맥 키이다.</param>
        private static void RunService(TacticalUnit unit, string targetKey)
        {
            var detector = unit.GetComponent<EnemyDetector>();
            var tree = new BehaviourTreeInstance();
            var service = tree.SetRoot(new DetectEnemyService(detector, 0f, targetKey, () => 0f));
            tree.AddChild(service, new ActionBehaviour(_ => BehaviourStatus.Success));

            var status = tree.Tick(unit.BehaviourContext);

            Assert.That(status, Is.EqualTo(BehaviourStatus.Success), "서비스가 자식을 돌리지 못했다.");
        }

        /// <summary>탐지기를 갖춘 아군 유닛을 만든다. 조립은 부르는 쪽이 한다.</summary>
        /// <returns>만든 유닛이다.</returns>
        private TacticalUnit CreateUnit()
        {
            var unitObject = new GameObject("Unit");
            _created.Add(unitObject);
            var unit = unitObject.AddComponent<TacticalUnit>();
            var detector = unitObject.AddComponent<EnemyDetector>();
            var registryObject = new GameObject("Registry");
            _created.Add(registryObject);
            _registry = registryObject.AddComponent<UnitSpatialRegistry>();
            detector.InjectSpatialRegistry(_registry);
            unitObject.GetComponent<TeamMember>().SetTeam(new TeamId(1));
            var definition = UnitDefinition.CreateRuntime("소총병", 100, new TeamId(1), attackRange: 10f);
            _created.Add(definition);
            unit.SetDefinition(definition);
            return unit;
        }

        /// <summary>탐지기의 레지스트리에 등록해 찾을 수 있는 적 유닛을 세운다.</summary>
        /// <param name="position">적이 설 자리이다.</param>
        /// <returns>만든 적 오브젝트이다.</returns>
        private GameObject CreateEnemy(Vector3 position)
        {
            var enemyObject = new GameObject("Enemy");
            _created.Add(enemyObject);
            enemyObject.transform.position = position;
            var team = enemyObject.AddComponent<TeamMember>();
            team.SetTeam(new TeamId(2));
            _registry.Register(team, enemyObject.transform, 0.5f);
            return enemyObject;
        }
    }
}
