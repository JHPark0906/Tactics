using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Tactics.Foundation.Simulation;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Lane;
using HS.Tactics.Pathfinding;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// 실제 경로 계획 서비스를 주입한 유닛의 추격 슬롯이 세계 좌표에서 갈리는지 검증한다.
    /// 전장 경계를 세워 도달 가능한 슬롯을 제공하며, 대상 위치로 폴백한 경우와 정상 슬롯 분산을 구분한다.
    /// 각 실행은 독립적으로 무대를 만들고 즉시 정리하며, 시작 시 남은 유닛·레인이 없는지 검사한다.
    /// 레인의 정적 참조가 다른 실행에 영향을 주지 않도록 실행 단위 수명으로 격리한다.
    /// </summary>
    public sealed class ApproachSpreadSlotTests
    {
        private const float Tolerance = 0.001f;

        // 테스트에서 나눌 슬롯의 수이다.
        private const int ApproachSlotCount = 6;

        // 테스트에서 슬롯을 펼칠 각도이다.
        private const float ApproachArcDegrees = 180f;

        // 테스트에서 사거리에 곱해 멈출 거리를 정하는 비율이다.
        private const float ApproachRangeRatio = 0.9f;

        // 정의가 없어 사거리를 모를 때 쓰는 테스트의 대체 거리이다.
        private const float FallbackHoldDistance = 8f;

        /// <summary>지금 돌고 있는 실행이 만든 것들이며, 그 실행이 끝날 때 지운다.</summary>
        private readonly List<GameObject> _runObjects = new();

        /// <summary>실행이 시작됐고 아직 끝나지 않았는지 여부이다.</summary>
        private bool _runIsOpen;

        private GameObject _boundsObject;
        private GameObject _serviceObject;
        private BattlePathfindingService _pathfindingService;
        private IObjectResolver _container;

        [SetUp]
        public void SetUp()
        {
            _boundsObject = new GameObject("Bounds");
            _boundsObject.AddComponent<BattleBounds>();

            _serviceObject = new GameObject("PathfindingService");
            _pathfindingService = _serviceObject.AddComponent<BattlePathfindingService>();

            var builder = new ContainerBuilder();
            builder.RegisterInstance(_pathfindingService);
            _container = builder.Build();
        }

        /// <summary>
        /// 만든 것을 즉시 지운다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>미뤄서 지우면 안 된다.</b> 실행 중의 보통 파괴는 프레임 끝까지 미뤄지는데, 검사들은
        /// 한 프레임 안에서 잇달아 돌 수 있다. 그러면 앞 검사가 만든 레인이 남아 있고,
        /// <b>레인이 없을 때를 보려던 검사가 레인이 있는 상태를 보게 된다.</b> 단언은 그대로 통과하므로
        /// 그 검사가 무의미해진 것을 아무도 모른다.
        /// </para>
        /// <para>
        /// 여기서 <see cref="DestroyRunObjects"/>를 다시 부르는 것은 <b>실행이 예외로 끝났을 때를 위한
        /// 안전망</b>이다. 정상 경로에서는 실행이 끝날 때 그 실행이 스스로 지운다.
        /// </para>
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            _runIsOpen = false;
            DestroyRunObjects();

            (_container as System.IDisposable)?.Dispose();
            Object.DestroyImmediate(_serviceObject);
            Object.DestroyImmediate(_boundsObject);
        }
        
        /// <summary>
        /// 무대 확인이 빈 무대와 남은 것이 있는 무대를 실제로 갈라 보는지 확인한다.
        /// </summary>
        /// <remarks>
        /// <b>이 검사가 지키는 것은 프로덕션이 아니라 다른 검사들이다.</b>
        /// <see cref="DescribeTheStageIfNotEmpty"/>가 무엇이 있든 늘 <c>null</c>을 돌려주게 되어도
        /// 나머지 검사는 전부 초록으로 남는다. 그러면 <b>격리가 깨진 날에 아무 신호도 나지 않고</b>,
        /// 그 사실은 "유닛 배치가 이상하다"로만 나타나 자리 계산을 의심하게 만든다.
        /// 그래서 확인 장치 자체를 여기서 한 번 걸어 본다.
        /// </remarks>
        [Test]
        public void TheStageCheckTellsAnEmptyStageFromACrowdedOne()
        {
            Assert.That(
                DescribeTheStageIfNotEmpty(),
                Is.Null,
                "검사가 시작되는 시점의 무대가 비어 있지 않다.");

            var leftoverUnit = new GameObject("Unit");
            leftoverUnit.AddComponent<TacticalUnit>();
            try
            {
                Assert.That(
                    DescribeTheStageIfNotEmpty(),
                    Is.Not.Null,
                    "씬에 유닛이 하나 있는데 무대 확인이 빈 무대라고 답했다.");
            }
            finally
            {
                DestroyAtOnce(leftoverUnit);
            }

            var leftoverLane = new GameObject("BattleLane");
            leftoverLane.AddComponent<BattleLane>();
            try
            {
                Assert.That(
                    DescribeTheStageIfNotEmpty(),
                    Is.Not.Null,
                    "씬에 레인이 하나 있는데 무대 확인이 빈 무대라고 답했다.");
            }
            finally
            {
                DestroyAtOnce(leftoverLane);
            }

            Assert.That(
                DescribeTheStageIfNotEmpty(),
                Is.Null,
                "남은 것을 모두 지웠는데 무대 확인이 아직 무엇인가 있다고 답했다.");
        }

        /// <summary>
        /// 실제 경로 계획 서비스를 사용하는 두 유닛이 서로 다른 슬롯을 목적지로 선택하는지 검증한다.
        /// 각 목적지가 대상 위치로 폴백하지 않았음을 먼저 확인한 뒤 두 목적지의 분리를 비교한다.
        /// </summary>
        [Test]
        public void TwoUnitsChasingTheSameTargetLandOnDifferentSlots()
        {
            using var run = BeginRun();
            CreateLane();

            // 목표가 (0,0,20)에 서므로(CreateUnitWithBranch 참고) 원점 중심의 기본 경계로는 자리가 경계 밖으로
            // 나가 전부 "못 감"으로 보인다. 목표 쪽으로 경계를 옮겨 자리 판정이 실제로 갈 수 있는 채로 돈다.
            _boundsObject.transform.position = new Vector3(0f, 0f, 20f);

            var firstBranch = CreateUnitWithBranch(
                out var firstMover, out var firstContext, out var firstTarget, assignTeam: true, slotSeed: 0);
            var secondBranch = CreateUnitWithBranch(
                out var secondMover, out var secondContext, out var secondTarget, assignTeam: true, slotSeed: 1);

            firstBranch.Tick(firstContext);
            secondBranch.Tick(secondContext);

            Assert.That(
                firstMover.LastDestination.Value,
                Is.Not.EqualTo(firstTarget.transform.position),
                "첫 유닛이 적의 자리 그대로로 물러났다. 여섯 자리가 모두 못 가는 것으로 보인 것이며, "
                + "경로 계획 서비스가 실제로 배선됐는지부터 봐야 한다(자리 번호 문제가 아니다).");
            Assert.That(
                secondMover.LastDestination.Value,
                Is.Not.EqualTo(secondTarget.transform.position),
                "두 번째 유닛이 적의 자리 그대로로 물러났다. 여섯 자리가 모두 못 가는 것으로 보인 것이며, "
                + "경로 계획 서비스가 실제로 배선됐는지부터 봐야 한다(자리 번호 문제가 아니다).");

            AssertApart(
                firstMover,
                secondMover,
                "같은 적을 노리는 두 유닛이 서로 다른 자리 번호를 받았는데 목적지가 갈리지 않았다.");
        }

        /// <summary>
        /// 한 실행을 시작하며, 그 실행이 무대를 혼자 쓰고 있는지 확인한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>"지우게 했다"와 "지워졌다"는 다르다.</b> 격리가 깨지면 그 사실이 그 자리에서 드러나야 한다.
        /// 이 확인이 없으면 같은 고장이 <b>"자리가 안 갈린다"로 나타나</b> 자리 번호를 의심하게 만든다.
        /// </para>
        /// <para>
        /// 돌려주는 범위를 <c>using</c>으로 받아야 실행이 끝날 때 그 실행이 만든 것이 지워진다.
        /// </para>
        /// </remarks>
        /// <returns>실행의 수명을 나타내는 범위이며, 벗어날 때 그 실행이 만든 것을 지운다.</returns>
        private RunScope BeginRun()
        {
            Assert.That(
                _runIsOpen,
                Is.False,
                "앞 실행이 아직 끝나지 않았는데 새 실행이 시작됐다. "
                + "실행 범위가 겹치면 어느 실행이 무엇을 만들었는지 갈라지지 않는다.");

            Assert.That(
                DescribeTheStageIfNotEmpty(),
                Is.Null,
                "실행을 시작하는데 무대가 비어 있지 않다. "
                + "이 실행은 자기가 세운 유닛과 레인만 씬에 있다는 전제로 자리를 확인한다.");

            _runIsOpen = true;
            return new RunScope(this);
        }

        /// <summary>실행을 끝내고 그 실행이 만든 것을 지운다.</summary>
        private void EndRun()
        {
            _runIsOpen = false;
            DestroyRunObjects();
        }

        /// <summary>
        /// 지금 씬에 있는 것을 적은 문장을 돌려주며, 이 픽스처가 세는 것이 하나도 없으면 <c>null</c>이다.
        /// </summary>
        /// <remarks>
        /// 세는 대상은 <see cref="TacticalUnit"/>과 <see cref="BattleLane"/>이다. 둘 다 컴포넌트라 씬에서 찾을 수 있고,
        /// 뒤 실행에 실제로 닿는 것도 이 둘이다 — 유닛은 자리를 나눌 때 함께 놓이는 것이고, 레인은
        /// <see cref="BattleLane.Active"/>라는 정적 참조 하나로 잡히므로 레인을 세우지 않은 실행에도 걸린다.
        /// 대상 오브젝트는 컴포넌트가 없어 이 방법으로 셀 수 없으며, 그래서 여기서 세지 않는다.
        /// </remarks>
        /// <returns>씬에 있는 유닛과 레인의 수를 적은 문장이며, 둘 다 없으면 <c>null</c>이다.</returns>
        private static string DescribeTheStageIfNotEmpty()
        {
            var units = Object.FindObjectsByType<TacticalUnit>(FindObjectsSortMode.None).Length;
            var lanes = Object.FindObjectsByType<BattleLane>(FindObjectsSortMode.None).Length;

            if (units == 0 && lanes == 0)
            {
                return null;
            }

            return $"씬에 유닛이 {units}, 레인이 {lanes} 있다.";
        }

        /// <summary>
        /// 이번 실행이 만들었다고 기록하고 그 오브젝트를 그대로 돌려준다.
        /// </summary>
        /// <remarks>
        /// 실행 밖에서 무대를 세우면 시작 시점의 확인을 건너뛴 것이므로 그 자리에서 걸리게 한다.
        /// 기록을 먼저 하고 확인하는 것은, 확인이 걸려도 <c>TearDown</c>이 이 오브젝트를 지울 수 있게 하기 위해서다.
        /// </remarks>
        /// <param name="created">이번 실행이 만든 오브젝트이다.</param>
        /// <returns>받은 오브젝트를 그대로 돌려준다.</returns>
        private GameObject Track(GameObject created)
        {
            _runObjects.Add(created);

            Assert.That(
                _runIsOpen,
                Is.True,
                "실행 범위 밖에서 무대에 오브젝트를 세웠다. 실행을 시작할 때 하는 무대 확인을 거치지 않았다.");

            return created;
        }

        /// <summary>이번 실행이 만든 것을 지운다.</summary>
        /// <remarks>
        /// 지우기 전에 비활성으로 만든다. 그래야 이동기가 루프에서 빠지고 수명주기가 더는 돌지 않는다.
        /// 도는 채로 지우면 그 프레임에 무엇이 남는지 분명하지 않다.
        /// </remarks>
        private void DestroyRunObjects()
        {
            foreach (var created in _runObjects)
            {
                DestroyAtOnce(created);
            }

            _runObjects.Clear();
        }

        /// <summary>오브젝트를 멈춘 뒤 그 자리에서 지운다.</summary>
        /// <param name="target">지울 오브젝트이며 이미 없으면 아무것도 하지 않는다.</param>
        private static void DestroyAtOnce(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            Object.DestroyImmediate(target);
        }

        /// <summary>두 유닛의 목적지가 갈렸는지 확인한다.</summary>
        /// <param name="first">첫 유닛의 이동 구성요소이다.</param>
        /// <param name="second">두 번째 유닛의 이동 구성요소이다.</param>
        /// <param name="because">갈려야 하는 까닭이다.</param>
        private static void AssertApart(FakeTickableMover first, FakeTickableMover second, string because)
        {
            Assert.That(first.LastDestination.HasValue, Is.True);
            Assert.That(second.LastDestination.HasValue, Is.True);
            Assert.That(
                Vector3.Distance(first.LastDestination.Value, second.LastDestination.Value),
                Is.GreaterThan(Tolerance),
                because);
        }

        /// <summary>시종점이 갖춰진 전투 레인을 씬에 만든다.</summary>
        private void CreateLane()
        {
            var laneObject = Track(new GameObject("BattleLane"));

            var start = new GameObject("Start").transform;
            var end = new GameObject("End").transform;
            start.SetParent(laneObject.transform);
            end.SetParent(laneObject.transform);
            start.position = new Vector3(0f, 0f, -50f);
            end.position = new Vector3(0f, 0f, 50f);

            var lane = laneObject.AddComponent<BattleLane>();
            lane.SetLane(start, end, new TeamId(1));
        }

        /// <summary>유닛 하나를 세우고 추격 자리를 조립하며 대상 오브젝트도 돌려준다.</summary>
        /// <remarks>
        /// <para>
        /// <b>비활성 상태로 조립한다.</b> 이 픽스처는 PlayMode라 활성 오브젝트에 컴포넌트를 붙이면
        /// <c>Awake</c>가 그 자리에서 돌아 <see cref="TacticalUnit.InitializeUnit"/>까지 즉시 끝난다.
        /// <see cref="TacticalUnit.SetDefinition"/>은 <b>조립 전에만 반영되므로</b>, 활성 오브젝트에 붙이고
        /// 나중에 정의를 넣으면 경고만 남고 조용히 무시된다 — 그러면 이 유닛은 언제나 대체값
        /// (<see cref="FallbackHoldDistance"/>)으로만 자리를 놓아, 정의가 실제로 반영되는지를 이 검사가
        /// 확인하지 못한다. <see cref="HS.Tactics.Placement.UnitSpawner"/>가 쓰는 것과 같은 순서(비활성으로 만들고
        /// 정의·진영을 넣은 뒤 활성화)를 따라야 진짜 정의값으로 자리가 놓인다.
        /// </para>
        /// </remarks>
        /// <param name="mover">만든 이동 구성요소이다.</param>
        /// <param name="context">대상이 담긴 행동 컨텍스트이다.</param>
        /// <param name="target">만든 대상 오브젝트이다.</param>
        /// <param name="assignTeam">진영을 지정할지 여부이다.</param>
        /// <param name="slotSeed">이 유닛의 고정된 자리 번호이다.</param>
        /// <returns>조립한 추격 자리이다.</returns>
        private IBehaviour CreateUnitWithBranch(
            out FakeTickableMover mover,
            out IBehaviourContext context,
            out GameObject target,
            bool assignTeam = false,
            int slotSeed = 0)
        {
            var unitObject = Track(new GameObject("Unit"));
            unitObject.SetActive(false);
            var unit = unitObject.AddComponent<TacticalUnit>();

            if (assignTeam)
            {
                unitObject.GetComponent<TeamMember>().SetTeam(new TeamId(1));
                unit.SetDefinition(UnitDefinition.CreateRuntime("SpreadTester", 100, attackRange: 10f));
            }

            // 활성화하는 순간 Awake가 돌아 위에서 넣은 정의와 진영을 그대로 반영해 조립된다.
            unitObject.SetActive(true);

            // 도달 판정(IsSlotReachable)이 실제 경로 계획 서비스를 쓰게 한다 — 넣지 않으면 어느 자리도
            // 갈 수 없는 것으로 보여 모든 유닛이 적의 자리로 물러난다.
            unit.InjectRuntimeDependencies(null, _container);

            var createdMover = new FakeTickableMover();
            mover = createdMover;

            target = Track(new GameObject("Target"));
            target.transform.position = new Vector3(0f, 0f, 20f);

            var behaviourContext = new BehaviourContext();
            behaviourContext.SetValue(UnitBehaviourKeys.Target, target.transform);
            context = behaviourContext;

            // 멈출 거리는 사거리에 비율을 곱한 값이다. 수를 여기 다시 적는 것이 아니라 같은 관계를 옮긴 것이므로,
            // 사거리나 비율이 바뀌면 이 자리도 따라간다.
            // 정의가 없는 유닛은 사거리를 모르므로 대신 쓰는 값을 둔다.
            var attackRange = unit.Definition != null ? unit.Definition.AttackRange : FallbackHoldDistance;
            var holdDistance = Mathf.Max(0.1f, attackRange * ApproachRangeRatio);

            // 프로덕션(ApproachAbility·UnitBehaviourDefinitions)은 유닛의 GetInstanceID로 번호를 임시로
            // 정한다. 여기서 같은 것을 쓰면 두 유닛이 우연히 같은 자리로 겹칠 수 있어 검사가 가끔 실패하므로,
            // 번호는 호출하는 쪽이 직접 정해 자리가 갈리는지를 결정적으로 본다.
            return new SpreadChaseTargetBehaviour(
                createdMover,
                UnitBehaviourKeys.Target,
                new ApproachSpreadSettings(holdDistance, ApproachSlotCount, ApproachArcDegrees),
                () => slotSeed,
                () => ApproachSpreadReference.Resolve(BattleLane.Active, ResolveTeam(unit)),
                unit.IsSlotReachable);
        }

        /// <summary>유닛의 진영을 얻으며 없으면 지정되지 않은 진영이다.</summary>
        /// <param name="unit">진영을 물을 유닛이다.</param>
        /// <returns>유닛의 진영이다.</returns>
        private static TeamId ResolveTeam(TacticalUnit unit)
        {
            if (unit == null)
            {
                return default;
            }

            var team = unit.Team;
            return team != null ? team.TeamId : default;
        }

        /// <summary>한 실행의 수명을 나타내며, 범위를 벗어나면 그 실행이 만든 것을 지운다.</summary>
        private sealed class RunScope : System.IDisposable
        {
            private readonly ApproachSpreadSlotTests _fixture;

            /// <summary>실행을 끝낼 픽스처를 받아 범위를 만든다.</summary>
            /// <param name="fixture">이 실행을 시작한 픽스처이다.</param>
            internal RunScope(ApproachSpreadSlotTests fixture)
            {
                _fixture = fixture;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                _fixture.EndRun();
            }
        }

        /// <summary>루프에 등록할 수 있는 테스트용 이동 구성요소이다.</summary>
        private sealed class FakeTickableMover : ICharacterMover
        {
            /// <summary>도착했는지 여부이다.</summary>
            public bool HasReachedDestination => false;

            /// <summary>마지막으로 받은 목적지이며 없으면 값이 없다.</summary>
            public Vector3? LastDestination { get; private set; }

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination)
            {
                LastDestination = destination;
                return true;
            }

            /// <inheritdoc />
            public void Stop()
            {
            }

            /// <inheritdoc />
            public void Tick(float deltaTime)
            {
            }
        }
    }
}
