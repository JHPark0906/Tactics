using System.Collections.Generic;
using System.Reflection;
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
    /// 표적을 컨텍스트에 쓰는 시계가 행동 트리의 탐지 서비스 하나뿐임을 못 박는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 탐지기가 <c>Update</c>에서도 찾으면 같은 키를 두 시계가 쓰고, 그중 프레임 시간 쪽이
    /// <b>표적이 얼마나 신선한지를 기기 성능에 묶는다.</b> 표적은 접근·엄폐·사격이 모두 읽는 값이므로
    /// 그것이 흔들리면 결과가 흔들린다.
    /// </para>
    /// <para>
    /// 간격도 한쪽만 갖는다. 탐지기와 서비스가 각자 간격을 가지면 어느 쪽이 실제로 쓰이는지
    /// 인스펙터만 보고는 알 수 없다.
    /// </para>
    /// <para>
    /// <b>확인하지 않는 것</b>: 레지스트리 원-겹침으로 실제 적을 잡는 것은 여기서 보지 않는다. 그것은
    /// <c>EnemyDetectorTargetSourceTests</c>가 확인한다. 여기서는 감지가 통과할 만큼만 레지스트리에
    /// 최소한으로 등록해 두고, 보는 것은 <b>누가 언제 부르는가</b>뿐이다.
    /// </para>
    /// </remarks>
    public sealed class EnemyDetectorSingleWriterTests
    {
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
        public void TheDetectorHasNoClockOfItsOwn()
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

            Assert.That(
                typeof(EnemyDetector).GetMethod("Update", flags),
                Is.Null,
                "Update에서 찾으면 프레임 시간이 표적의 신선도에 스민다.");
            Assert.That(
                typeof(EnemyDetector).GetMethod("LateUpdate", flags),
                Is.Null,
                "LateUpdate도 프레임 시계다.");
            Assert.That(
                typeof(EnemyDetector).GetMethod("FixedUpdate", flags),
                Is.Null,
                "고정 스텝이라도 트리 밖의 두 번째 시계다. 갱신은 트리의 서비스만 한다.");
            Assert.That(
                typeof(EnemyDetector).GetMethod("Tick", flags),
                Is.Null,
                "부르는 곳이 없는 틱 진입점은 두 번째 시계로 되살아날 자리다.");
        }

        [Test]
        public void TheIntervalLivesOnTheServiceNotTheDetector()
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

            Assert.That(
                typeof(EnemyDetector).GetField("detectionInterval", flags),
                Is.Null,
                "탐지기가 간격을 또 가지면 어느 쪽이 쓰이는지 인스펙터로 알 수 없다.");
            Assert.That(
                typeof(DetectEnemyServiceDefinition).GetField("interval", flags),
                Is.Not.Null,
                "간격은 트리를 그리는 자리에 있어야 그 자리에서 보고 정한다.");
        }

        [Test]
        public void AssemblyDoesNotWriteTheTarget()
        {
            var unit = CreateUnit();

            unit.InitializeUnit();

            Assert.That(
                unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.Target, out Transform _),
                Is.False,
                "조립은 표적을 정하지 않는다. 첫 값은 트리의 서비스가 쓴다.");
        }

        [Test]
        public void TheTreeServiceWritesTheTargetBeforeItsChildRuns()
        {
            var unit = CreateUnit();
            var detector = unit.GetComponent<EnemyDetector>();
            unit.InitializeUnit();
            var enemy = CreateEnemy(new Vector3(0f, 0f, 5f));

            var tree = new BehaviourTreeInstance();
            var service = tree.SetRoot(new DetectEnemyService(detector, 0f, UnitBehaviourKeys.Target, () => 0f));
            Transform seenByChild = null;
            tree.AddChild(service, new ActionBehaviour(context =>
            {
                context.TryGetValue(UnitBehaviourKeys.Target, out seenByChild);
                return BehaviourStatus.Success;
            }));

            var status = tree.Tick(unit.BehaviourContext);

            Assert.That(status, Is.EqualTo(BehaviourStatus.Success));
            Assert.That(
                seenByChild,
                Is.SameAs(enemy.transform),
                "서비스가 자식보다 먼저 표적을 써야 접근 노드가 첫 틱에 읽는다.");
        }

        [Test]
        public void TheServiceClearsTheTargetWhenTheEnemyIsGone()
        {
            // 지우는 쪽도 서비스가 한다. 탐지기가 자기 시계로 지우면 지워지는 시점이
            // 프레임률에 묶이고, 이미 없는 적을 겨누는 시간이 기기마다 달라진다.
            var unit = CreateUnit();
            var detector = unit.GetComponent<EnemyDetector>();
            unit.InitializeUnit();
            var enemy = CreateEnemy(new Vector3(0f, 0f, 5f));

            var tree = new BehaviourTreeInstance();
            var service = tree.SetRoot(new DetectEnemyService(detector, 0f, UnitBehaviourKeys.Target, () => 0f));
            tree.AddChild(service, new ActionBehaviour(_ => BehaviourStatus.Success));

            tree.Tick(unit.BehaviourContext);
            Assert.That(
                unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.Target, out Transform _),
                Is.True,
                "먼저 표적이 잡혀 있어야 지워지는 것을 볼 수 있다.");

            Object.DestroyImmediate(enemy);
            tree.Tick(unit.BehaviourContext);

            Assert.That(
                unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.Target, out Transform _),
                Is.False,
                "적이 사라지면 서비스가 그 자리에서 표적을 지워야 한다.");
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
