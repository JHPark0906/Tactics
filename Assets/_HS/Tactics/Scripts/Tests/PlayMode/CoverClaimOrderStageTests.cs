using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Tactics.Foundation.Geometry;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Character.Movement;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Lane;
using HS.Tactics.Pathfinding;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// 두 유닛이 같은 엄폐 후보를 놓고 경쟁할 때 선택 순서를 검증한다.
    /// 점유는 이동 도착이 아니라 어빌리티 활성화 중 ClaimCover에서 결정된다.
    /// 두 출발 거리를 충분히 벌려 접근 거리에 따른 선택 차이를 보며, Unity 실행 순서의 재현성을 보장하는 검사는 아니다.
    /// 무대는 스폰·주입·조립·전진·탐지·접근·엄폐 선택·점유 경쟁을 실행한다.
    /// 선택 순서를 격리하기 위해 사격·피해·엄폐물 흡수·파괴·유닛 사망·승패 판정은 실행하지 않는다.
    /// </summary>
    public sealed class CoverClaimOrderStageTests
    {
        private const float StepDuration = 1f / 30f;
        private const float MoveSpeed = 3.5f;
        private const float AttackRange = 10f;
        private const int MaxSteps = 600;

        /// <summary>가까운 쪽 아군이 서는 자리이다.</summary>
        private static readonly Vector3 NearAllyStart = new(-1.5f, 0f, -24f);

        /// <summary>먼 쪽 아군이 서는 자리이며, 가까운 쪽보다 여유 있게 뒤에 둔다.</summary>
        private static readonly Vector3 FarAllyStart = new(1.5f, 0f, -32f);

        private static readonly Vector3 EnemyPosition = new(0f, 0f, 0f);

        /// <summary>지금 돌고 있는 실행이 만든 것들이며, 그 실행이 끝날 때 지운다.</summary>
        private readonly List<GameObject> _runObjects = new();

        /// <summary>지금 돌고 있는 실행이 만든 씬 밖 자산(유닛 정의, 어빌리티 정의)이다.</summary>
        private readonly List<Object> _createdAssets = new();

        private GameObject _ground;
        private float _originalCaptureDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _originalCaptureDeltaTime = Time.captureDeltaTime;

            // 경로와 이동은 평면 경계를 사용한다. 바닥 오브젝트는 무대 표시용이며 판정에 쓰지 않는다.
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "Ground";
            _ground.transform.localScale = new Vector3(14f, 1f, 14f);
        }

        /// <summary>
        /// 무대를 즉시 지운다.
        /// </summary>
        /// <remarks>
        /// 미뤄서 지우면 앞 검사의 레인과 엄폐 지점이 다음 검사에 남는다. 그러면 뒤 검사가 다른 무대를
        /// 보게 되는데 단언은 그대로 통과하므로 아무도 모른다.
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = _originalCaptureDeltaTime;

            // 실행이 예외로 끝났을 때를 위한 안전망이다. 정상 경로에서는 실행이 스스로 지운다.
            DestroyRunObjects();

            foreach (var asset in _createdAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            _createdAssets.Clear();

            Object.DestroyImmediate(_ground);
        }

        /// <summary>
        /// 가까운 쪽이 먼 쪽보다 먼저 엄폐를 고르는지 잰다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>트리를 통해 어빌리티를 켜지 않는다.</b> <c>ActivateAbility(TakeCover)</c> 노드는 「대상이 있고
        /// 아직 사거리 밖」인 그 한 번의 실행에서만 시도되고, 그 뒤에는 추격이 도는 동안 다시 시도되지
        /// 않는다(추격이 자리를 잡으면 Selector가 그 자식에 눌러앉는다). 그 한 번이 마침 엄폐 지점의
        /// 탐색 반경 안일 때만 성립하는 우연이라 순서를 재는 무대로 쓰면 기하 배치의 우연으로 통과한다.
        /// 그래서 이 무대는 트리 대신 어빌리티 시스템을 직접 매 스텝 두드린다 — <c>TryActivate</c>는
        /// 이미 켜져 있으면 아무 일도 하지 않으므로(<see cref="GameplayAbilityActivationResult.AlreadyActive"/>)
        /// 매 스텝 불러도 안전하고, 그러면 사거리 안에 들 때까지 다가가는 내내 기회가 남아 우연에
        /// 기대지 않는다. 트리 노드 자체가 이 어빌리티를 옳게 물었는지는 <c>UnitBehaviourTreeAssetTests</c>가
        /// 정본 에셋으로 따로 고정한다.
        /// </para>
        /// </remarks>
        [Test]
        public void TheCloserAllyClaimsCoverFirst()
        {
            CreateLane();
            CreateBounds();
            var pathfindingService = CreatePathfindingService();
            var registry = CreateSpatialRegistry();
            var covers = CreateCoverPoints();
            CreateEnemy(registry);

            var near = SpawnAlly("NearAlly", NearAllyStart, registry, pathfindingService);
            var far = SpawnAlly("FarAlly", FarAllyStart, registry, pathfindingService);
            var owners = new Dictionary<GameObject, int> { [near.Unit.gameObject] = 1, [far.Unit.gameObject] = 2 };

            // 무대가 다 선 지금 격리를 확인한다 — 경주를 돌리기 전이라, 격리가 깨졌으면 "승자가
            // 달라졌다"로 나타나기 전에 "엄폐 지점이 둘이 아니다" 같은 원인이 그대로 드러난다.
            AssertTheStageIsAlone();

            var claimed = new HashSet<CoverPoint>();
            var order = new List<int>();
            var result = new StageResult();

            // 체력 어트리뷰트가 없는 대역끼리의 초기 탐지에서 EnsureBound 오류가 날 수 있다.
            // 두 유닛의 탐지 순서가 정해져 있지 않으므로 초기 몇 스텝에서만 로그 실패 판정을 끈다.
            // 이후 스텝에는 로그 실패 판정을 복원한다.
            const int StepsWhereCrossDetectionCanFire = 10;
            var step = 0;
            for (; step < MaxSteps; step++)
            {
                LogAssert.ignoreFailingMessages = step < StepsWhereCrossDetectionCanFire;
                try
                {
                    TickAlly(near, "NearAlly");
                    TickAlly(far, "FarAlly");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }

                RecordClaims(covers, claimed, owners, order);
                result.Trace.Add(PlanarPosition.FromWorld(near.Unit.transform.position));

                if (claimed.Count == covers.Count)
                {
                    step++;
                    break;
                }
            }

            result.Steps = step;
            result.ClaimOrder.AddRange(order);
            result.NearEnd = PlanarPosition.FromWorld(near.Unit.transform.position);
            result.FarEnd = PlanarPosition.FromWorld(far.Unit.transform.position);

            Assert.That(step, Is.LessThan(MaxSteps), $"무대 확인: {DescribeStage(result)} — 시간 안에 둘 다 고르지 못했다.");
            Assert.That(order.Count, Is.EqualTo(covers.Count), $"무대 확인: {DescribeStage(result)} — 두 지점이 다 골라지지 않았다.");
            Assert.That(order[0], Is.EqualTo(1), $"가까운 쪽이 먼저 골라야 한다. {DescribeStage(result)}");

            DestroyRunObjects();
        }

        /// <summary>
        /// 무대가 어디까지 갔는지 적는다.
        /// </summary>
        /// <remarks>
        /// 원인을 짐작해 적지 않는다. 무대가 끝난 자리를 그대로 적어 두면 읽는 쪽이 그것으로 판단한다.
        /// </remarks>
        /// <param name="result">한 번 돌린 결과이다.</param>
        /// <returns>무대의 마지막 상태를 적은 문장이다.</returns>
        private static string DescribeStage(StageResult result)
        {
            return $"스텝 {result.Steps}, 고른 유닛 {result.ClaimOrder.Count}명, "
                   + $"가까운 쪽 z={result.NearEnd.Z:F2}, 먼 쪽 z={result.FarEnd.Z:F2}, "
                   + $"경로 꺾임점 최대 {result.MaxPathCorners}개.";
        }

        /// <summary>
        /// 이 실행이 무대를 혼자 쓰고 있는지 확인한다.
        /// </summary>
        /// <remarks>
        /// <b>"지우게 했다"와 "지워졌다"는 다르다.</b> 격리가 깨지면 그 사실이 곧바로 드러나야 한다.
        /// 이 단언이 없으면 같은 고장이 <b>"승자가 달라졌다"로 나타나</b> 이동을 의심하게 만든다.
        /// </remarks>
        private static void AssertTheStageIsAlone()
        {
            Assert.That(
                Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None).Length,
                Is.EqualTo(2),
                "씬에 엄폐 지점이 둘이 아니다. 앞 실행이 남아 있으면 유닛이 남의 엄폐를 본다.");
            Assert.That(
                Object.FindObjectsByType<TacticalUnit>(FindObjectsSortMode.None).Length,
                Is.EqualTo(2),
                "씬에 유닛이 둘이 아니다. 앞 실행 유닛이 남아 있으면 계속 움직인다.");
            Assert.That(
                Object.FindObjectsByType<BattleLane>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1),
                "씬에 레인이 하나가 아니다. 뒤엣것이 정적 참조를 뺏는다.");
        }

        /// <summary>
        /// 이번 실행이 만든 것을 지운다.
        /// </summary>
        /// <remarks>
        /// 지우기 전에 비활성으로 만든다. 그래야 이동기가 루프에서 빠지고 수명주기도 더는 돌지 않는다.
        /// 도는 채로 지우면 그 프레임에 무엇이 남는지 분명하지 않다.
        /// </remarks>
        private void DestroyRunObjects()
        {
            foreach (var created in _runObjects)
            {
                if (created == null)
                {
                    continue;
                }

                created.SetActive(false);
                Object.DestroyImmediate(created);
            }

            _runObjects.Clear();
        }

        /// <summary>이번 스텝에 새로 점유된 엄폐 지점을 순서대로 기록한다.</summary>
        /// <remarks>CoverPoint.IsOccupied와 CoverPoint.Occupant를 읽어 기록한다.</remarks>
        private static void RecordClaims(
            IReadOnlyList<CoverPoint> covers,
            ISet<CoverPoint> claimed,
            IReadOnlyDictionary<GameObject, int> owners,
            ICollection<int> order)
        {
            for (var index = 0; index < covers.Count; index++)
            {
                var cover = covers[index];
                if (!cover.IsOccupied || claimed.Contains(cover))
                {
                    continue;
                }

                claimed.Add(cover);
                if (owners.TryGetValue(cover.Occupant, out var owner))
                {
                    order.Add(owner);
                }
            }
        }

        /// <summary>아군이 전진할 레인을 세운다.</summary>
        private void CreateLane()
        {
            var laneObject = new GameObject("BattleLane");
            _runObjects.Add(laneObject);

            var start = new GameObject("Start").transform;
            var end = new GameObject("End").transform;
            start.SetParent(laneObject.transform);
            end.SetParent(laneObject.transform);
            start.position = new Vector3(0f, 0f, -60f);
            end.position = new Vector3(0f, 0f, 60f);

            var lane = laneObject.AddComponent<BattleLane>();
            lane.SetLane(start, end, new TeamId(1));
        }

        /// <summary>
        /// 이동기가 경로를 계획할 전투 경계를 세운다.
        /// </summary>
        /// <remarks>
        /// 대상을 못 찾은 동안은 레인의 끝(z=60)을 향해 걷는다 — 경계가 그 지점을 담지 못하면
        /// <c>Plan</c>이 매번 경계 밖으로 거절해 <c>MoveTo</c>가 항상 실패하고, 유닛은 시작 자리에서
        /// 한 걸음도 못 뗀다. 그래서 두 아군의 출발 자리(z=-24·-32)뿐 아니라 그 목적지까지 넉넉히 담는다.
        /// </remarks>
        private BattleBounds CreateBounds()
        {
            var boundsObject = new GameObject("Bounds");
            _runObjects.Add(boundsObject);
            var bounds = boundsObject.AddComponent<BattleBounds>();

            var field = typeof(BattleBounds).GetField(
                "halfExtents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "무대 확인: BattleBounds의 halfExtents 필드 이름이 바뀌었다.");
            field.SetValue(bounds, new Vector2(10f, 65f));

            return bounds;
        }

        /// <summary>이동기와 엄폐 센서가 함께 쓸 경로 계획 서비스를 세운다.</summary>
        private BattlePathfindingService CreatePathfindingService()
        {
            var serviceObject = new GameObject("PathfindingService");
            _runObjects.Add(serviceObject);
            return serviceObject.AddComponent<BattlePathfindingService>();
        }

        /// <summary>탐지기가 물을 공간 레지스트리를 세운다.</summary>
        private UnitSpatialRegistry CreateSpatialRegistry()
        {
            var registryObject = new GameObject("Registry");
            _runObjects.Add(registryObject);
            return registryObject.AddComponent<UnitSpatialRegistry>();
        }

        /// <summary>양쪽 아군이 다투는 두 엄폐 지점을 세운다. 위치는 위협 사거리 안, 접근로 위 근처로 둔다.</summary>
        private List<CoverPoint> CreateCoverPoints()
        {
            return new List<CoverPoint>
            {
                CreateCoverPoint("CoverA", new Vector3(-2f, 0f, -8f)),
                CreateCoverPoint("CoverB", new Vector3(2f, 0f, -8f)),
            };
        }

        /// <summary>어트리뷰트 집합 없이 최소한으로만 갖춘 엄폐 지점을 놓는다. 그 결핍을 한 번 경고한다.</summary>
        private CoverPoint CreateCoverPoint(string objectName, Vector3 position)
        {
            var coverObject = new GameObject(objectName);
            _runObjects.Add(coverObject);
            coverObject.transform.position = position;
            LogAssert.Expect(LogType.Warning, new Regex("어트리뷰트 집합에"));
            return coverObject.AddComponent<CoverPoint>();
        }

        /// <summary>양쪽 아군이 노릴 위협을 세우고 레지스트리에 등록한다. 그 자체는 유닛이 아니다.</summary>
        private GameObject CreateEnemy(UnitSpatialRegistry registry)
        {
            var enemyObject = new GameObject("Enemy");
            _runObjects.Add(enemyObject);
            enemyObject.transform.position = EnemyPosition;
            var team = enemyObject.AddComponent<TeamMember>();
            team.SetTeam(new TeamId(2));
            registry.Register(team, enemyObject.transform, 0.5f);
            return enemyObject;
        }

        /// <summary>
        /// 아군 유닛 하나를 실제 조립 경로(정의 → 의존성 주입 → 조립)로 세운다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 활성 오브젝트에 <see cref="TacticalUnit"/>을 붙이면 그 자리에서 <c>Awake</c>가 곧바로 돈다
        /// (EditMode와 달리 PlayMode에서는 컴포넌트를 붙이는 즉시 돈다). 정의를 주기도 전에 조립이
        /// 끝나 버리면 <c>SetDefinition</c>은 조용히 무시되고, <c>EnsureAbilitySystem</c>이 이미 붙여 둔
        /// <see cref="GameplayAbilitySystemComponent"/>를 나중에 또 붙이려는 호출은 중복 추가로 막혀
        /// null을 돌려준다 — <c>UnitSpawner</c>가 쓰는 순서(비활성 → 구성요소·정의·의존성 → 활성화)를
        /// 그대로 따라야 조립이 한 번, 올바른 상태로 일어난다.
        /// </para>
        /// <para>
        /// 어빌리티 시스템 구성요소는 직접 붙이지 않는다. <c>EnsureAbilitySystem</c>이 없으면 스스로 붙이므로
        /// 조립이 끝난 뒤 <see cref="TacticalUnit.AbilitySystem"/>에서 읽는다. 그 <c>abilitySet</c>은 비워 둔
        /// 채 조립 뒤에 접근·엄폐 확보 둘만 직접 부여한다 — 이 무대가 재려는 것이 그 둘뿐이라 유닛 정의에
        /// 전체 어빌리티 집합을 싣지 않는다.
        /// </para>
        /// </remarks>
        private SpawnedAlly SpawnAlly(
            string objectName, Vector3 start, UnitSpatialRegistry registry, BattlePathfindingService pathfindingService)
        {
            var unitObject = new GameObject(objectName);
            _runObjects.Add(unitObject);
            unitObject.SetActive(false);
            unitObject.transform.position = start;

            var unit = unitObject.AddComponent<TacticalUnit>();
            var mover = unitObject.AddComponent<PlanarCharacterMover>();
            var detector = unitObject.AddComponent<EnemyDetector>();
            detector.InjectSpatialRegistry(registry);
            var coverSensor = unitObject.AddComponent<CoverSensor>();
            var coverState = unitObject.AddComponent<UnitCoverState>();

            var definition = UnitDefinition.CreateRuntime(
                objectName, 100, new TeamId(1), MoveSpeed, attackRange: AttackRange);
            _createdAssets.Add(definition);
            unit.SetDefinition(definition);

            var builder = new ContainerBuilder();
            builder.RegisterInstance<IUnitSpatialRegistry>(registry);
            builder.RegisterInstance(pathfindingService);
            using (var container = builder.Build())
            {
                unit.InjectRuntimeDependencies(null, container);
            }

            // 이 무대의 유닛 정의는 어빌리티 집합을 비워 둔다 — 접근·엄폐 확보만 재므로 체력을 싣지
            // 않는다. 조립 코드는 그 사실을 체력 어트리뷰트가 없다는 경고로 알린다. 무해하다: 사격이
            // 이 무대의 범위 밖이라 체력을 읽는 코드가 아예 돌지 않는다.
            LogAssert.Expect(LogType.Warning, new Regex("체력 어트리뷰트"));
            unitObject.SetActive(true);

            // 조립이 CoverSensor의 거리 측정을 BattlePathfindingService에 연결한다.
            // 이 무대는 주입된 실제 경로 계획으로 엄폐 접근 거리를 잰다.
            var abilities = unit.AbilitySystem;
            var approach = ApproachAbilityDefinition.CreateRuntime(UnitAbilityTags.Approach);
            var takeCover = TakeCoverAbilityDefinition.CreateRuntime(UnitAbilityTags.TakeCover);
            _createdAssets.Add(approach);
            _createdAssets.Add(takeCover);
            abilities.System.GrantAbility(approach);
            abilities.System.GrantAbility(takeCover);

            return new SpawnedAlly(unit, mover, detector, abilities, coverSensor, coverState);
        }

        /// <summary>
        /// 아군 한 명을 한 스텝 돌린다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 대상을 못 찾았으면 레인 끝을 향해 걷는다. 찾았으면 엄폐 확보를 먼저 두드리고, 그것이 이번
        /// 스텝에 활성화되지 못했을 때만 접근을 두드린다 — 실제 트리에서도 엄폐가 교전·추격 가지보다
        /// 위에 서므로, 둘 다 매번 두드리면 <see cref="ApproachAbility.OnTick"/>이 매 틱 다시 부르는
        /// <c>MoveTo</c>가 방금 <c>TakeCoverAbility.OnActivate</c>가 낸 이동 명령을 같은 틱에 덮어써
        /// 이동 수단을 두고 다투게 된다. 이 무대가 재는 것은 점유(<c>ClaimCover</c>) 그 자체라 다퉈도
        /// 점유 순서는 흔들리지 않지만, 우선순위를 지키면 그 다툼 자체가 없다.
        /// </para>
        /// <para>
        /// <c>TryActivate</c>는 이미 도는 어빌리티에는 조용히 막히므로
        /// (<see cref="GameplayAbilityActivationResult.AlreadyActive"/>) 매 스텝 불러도 상태를 흔들지 않는다.
        /// </para>
        /// </remarks>
        /// <param name="ally">돌릴 아군이다.</param>
        /// <param name="label">진단 로그에 적을 이름표이다.</param>
        private void TickAlly(SpawnedAlly ally, string label)
        {
            var target = ally.Detector.RefreshTarget();
            if (target == null)
            {
                ally.Mover.MoveTo(new Vector3(0f, 0f, 60f));
            }
            else
            {
                var takeCoverTag = GameplayTag.Parse(UnitAbilityTags.TakeCover);
                var result = ally.Abilities.System.TryActivate(takeCoverTag);

                // 결과가 바뀔 때만 적어 스텝마다 쌓이는 소음을 막는다 — 언제 어떤 이유로 막혔는지가
                // 바로 이 전이(edge)에 있다.
                if (result != ally.LastTakeCoverResult)
                {
                    ally.LastTakeCoverResult = result;
                    Debug.Log(
                        $"[진단] {label}: TakeCover 활성화 시도 결과={result}, 자리={ally.Unit.transform.position}, " +
                        $"대상={target.position}, IsInCover={ally.CoverState.IsInCover}, " +
                        $"HasCoverClaim={ally.CoverState.HasCoverClaim}");

                    if (result == GameplayAbilityActivationResult.Rejected)
                    {
                        var found = ally.CoverSensor.TryFindCover(target.position, out var coverPoint, AttackRange);
                        Debug.Log(
                            $"[진단] {label}: CoverSensor.TryFindCover 직접 호출 결과={found}, " +
                            $"coverPoint={(coverPoint != null ? coverPoint.name : "null")}");
                    }
                }

                if (!ally.Abilities.System.IsActive(takeCoverTag))
                {
                    ally.Abilities.System.TryActivate(GameplayTag.Parse(UnitAbilityTags.Approach));
                }

                ally.Abilities.System.Tick(StepDuration);
            }

            ally.Mover.Tick(StepDuration);
        }

        /// <summary>무대가 세운 아군 하나와 그 협력자들을 함께 든다.</summary>
        private sealed class SpawnedAlly
        {
            public SpawnedAlly(
                TacticalUnit unit, PlanarCharacterMover mover, EnemyDetector detector,
                GameplayAbilitySystemComponent abilities, CoverSensor coverSensor, UnitCoverState coverState)
            {
                Unit = unit;
                Mover = mover;
                Detector = detector;
                Abilities = abilities;
                CoverSensor = coverSensor;
                CoverState = coverState;
            }

            public TacticalUnit Unit { get; }

            public PlanarCharacterMover Mover { get; }

            public EnemyDetector Detector { get; }

            public GameplayAbilitySystemComponent Abilities { get; }

            public CoverSensor CoverSensor { get; }

            public UnitCoverState CoverState { get; }

            /// <summary>진단 로그가 전이를 감지하는 데 쓰는, 지난 스텝의 TakeCover 활성화 시도 결과이다.</summary>
            public GameplayAbilityActivationResult LastTakeCoverResult { get; set; }
                = (GameplayAbilityActivationResult)(-1);
        }

        /// <summary>한 번 돌린 결과이며, 무엇을 확인할지는 담지 않는다.</summary>
        private sealed class StageResult
        {
            /// <summary>가까운 쪽 아군이 스텝마다 지난 자리이다.</summary>
            public List<PlanarPosition> Trace { get; } = new();

            /// <summary>엄폐를 고른 유닛의 번호를 고른 차례대로 담는다.</summary>
            public List<int> ClaimOrder { get; } = new();

            /// <summary>실제로 돈 스텝 수이다.</summary>
            public int Steps { get; set; }

            /// <summary>가까운 쪽 아군이 끝난 자리이다.</summary>
            public PlanarPosition NearEnd { get; set; }

            /// <summary>먼 쪽 아군이 끝난 자리이다.</summary>
            public PlanarPosition FarEnd { get; set; }

            /// <summary>
            /// 이 실행에서 본 경로 꺾임점의 최대 개수이다.
            /// </summary>
            /// <remarks>
            /// 둘이면 경로가 곧은 선이라는 뜻이고, 그때는 경로를 따라 재는 것과 직선으로 재는 것이
            /// 같은 값을 낸다. 두 재는 법을 견주는 관측이 <b>같게 나왔을 때 그것이 "차이가 없다"인지
            /// "차이가 날 자리가 없었다"인지</b>를 이 수가 가른다.
            /// </remarks>
            public int MaxPathCorners { get; set; }
        }
    }
}
