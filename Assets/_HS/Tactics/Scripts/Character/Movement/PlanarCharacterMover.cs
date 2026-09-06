using System;
using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Character;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Foundation.Simulation;
using R3;
using UnityEngine;

namespace HS.Tactics.Character.Movement
{
    /// <summary>고정 스텝에서 평면 경로를 따라 위치를 적분하는 ICharacterMover 구현이다.</summary>
    /// <remarks>
    /// <para>경로는 SetPathPlanner로 주입한다. 계획자가 없거나 경로 계산이 실패하면 MoveTo도 실패한다.
    /// 전투 씬에는 BattlePathfindingService가 필요하다. 속도·가속도·폴백 각속도·자동 감속은 이 구성요소가 가진다.</para>
    /// <para>ApplyFacing은 ICharacterFacing에 이동 방향을 전달하며, 해당 구성요소가 없을 때만 직접 회전한다.</para>
    /// <para>SetObstacles로 주입한 장애물과 SetNearbyUnitsQuery로 조회한 유닛만 충돌에 반영한다.
    /// 다른 유닛은 매 Tick의 이동 구간마다 다시 조회한다. 같은 자리를 향하는 유닛끼리의 우선권은
    /// Unity의 FixedUpdate 실행 순서에 따르며, 먼저 이동한 유닛의 위치를 뒤 유닛이 읽는다.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlanarCharacterMover : MonoBehaviour,
        ICharacterComponent,
        ICharacterMover,
        IStoppingDistanceProvider
    {
        /// <summary>
        /// 목적지가 이보다 적게 움직였으면 길을 다시 찾지 않는다(미터).
        /// </summary>
        /// <remarks>
        /// 추격 노드는 매 틱 목적지를 다시 주므로, 이 문턱이 없으면 매 틱 경로 계산이 걸리고
        /// 그 사이 적분이 한 걸음도 나가지 못한다.
        /// </remarks>
        private const float DestinationChangeThreshold = 0.05f;

        /// <summary>죽은 상태를 나타내는 태그이다. 이 태그를 얻는 순간 이동을 멈춘다.</summary>
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        [Tooltip("목적지 도착으로 판정하는 잔여 거리(미터)이다.")]
        [SerializeField]
        [Min(0f)]
        private float stoppingDistance = 0.5f;

        /// <inheritdoc />
        public float StoppingDistance => stoppingDistance;

        /// <summary>최고 이동 속도(초당 미터)이며 유닛 조립에서 정의 값으로 지정한다.</summary>
        [Tooltip("최고 이동 속도(초당 미터)이다.")]
        [SerializeField]
        [Min(0f)]
        private float speed = 3.5f;

        /// <summary>속도가 오르내리는 한계(미터/초²)이다.</summary>
        [Tooltip("속도가 오르내리는 한계(미터/초²)이다.")]
        [SerializeField]
        [Min(0f)]
        private float acceleration = 8f;

        /// <summary>ICharacterFacing이 없을 때 적용할 최고 각속도(초당 도)이다.</summary>
        [Tooltip("방향 구성요소가 없을 때 적용할 최고 각속도(초당 도)이다.")]
        [SerializeField]
        [Min(0f)]
        private float angularSpeed = 120f;

        /// <summary>목적지에 가까워질수록 속도를 미리 줄일지 여부이다.</summary>
        [Tooltip("목적지에 가까워질수록 속도를 미리 줄일지 여부이다.")]
        [SerializeField]
        private bool autoBraking = true;

        /// <summary>
        /// 이번 스텝이 장애물에 막혀 거의 못 나아간 것이 이만큼 이어지면 지금 자리에서 길을 다시 얻는다.
        /// </summary>
        /// <remarks>
        /// 미끄러져서 상당히 나아갔으면(막혔어도) 여기 안 걸린다. 좁은 틈에 여럿이 동시에 막아
        /// 정말 못 나아가는 경우만 걸리며, 그것은 규칙의 귀결이지 되풀이해서 재시도할 결함이 아니다.
        /// 다만 다시 얻는 길이 그 사이 열렸을 수 있으므로 아주 재시도하지 않는 것도 아니다.
        /// </remarks>
        private const int StuckStepThreshold = 3;

        /// <summary>이만큼도 못 나아갔으면 이번 스텝을 정체로 센다(미터).</summary>
        private const float StuckMovementEpsilon = 0.001f;

        private bool _isInitialized;
        private ICharacterFacing _facing;
        private PlanarPosition _destination;
        private readonly List<PlanarPosition> _plannedCorners = new();
        private readonly List<Vector3> _plannedWorldCorners = new();
        private int _cornerIndex;
        private float _currentSpeed;
        private bool _hasDestination;
        private float _cornerHeight;
        private PathPlanner _pathPlanner;
        private IReadOnlyList<PlanarRectangle> _obstacles = Array.Empty<PlanarRectangle>();
        private float _collisionRadius;
        private int _stuckStepCount;
        private NearbyUnitsQuery _nearbyUnitsQuery;
        private readonly List<PlanarCircle> _nearbyUnitsBuffer = new();
        private IDisposable _deathSubscription;

        /// <summary>
        /// 이번 스텝이 훑는 자리를 넉넉히 담는 원의 반지름을 정할 때, 스텝 거리의 절반과 자신의 반지름에
        /// 더 얹는 여유(미터)이다.
        /// </summary>
        /// <remarks>
        /// 다른 유닛의 반지름은 여기서 모른다 — <see cref="NearbyUnitsQuery"/> 구현(예: 공간 레지스트리의
        /// 겹침 판정)이 그쪽 반지름을 이미 반영하므로, 이쪽은 자신의 스윕만 정확히 덮으면 된다. 그래도
        /// 부동소수 오차로 경계에 걸친 후보를 놓치지 않도록 작은 여유를 둔다.
        /// </remarks>
        private const float SweepPad = 0.05f;

        /// <summary>경로를 그 자리에서 얻는 방법이 끼워져 있는지 여부이다.</summary>
        public bool HasPathPlanner => _pathPlanner != null;

        /// <summary>다른 유닛을 찾는 방법이 끼워져 있는지 여부이다. 없으면 다른 유닛을 하나도 안 본다.</summary>
        public bool HasNearbyUnitsQuery => _nearbyUnitsQuery != null;

        /// <summary>경로를 얻는 방법을 끼운다.</summary>
        /// <remarks>
        /// 끼우지 않으면 <see cref="MoveTo"/>가 그대로 실패한다 — 물러설 대체 경로 출처가 없다.
        /// </remarks>
        /// <param name="planner">경로를 얻는 방법이다.</param>
        public void SetPathPlanner(PathPlanner planner)
        {
            _pathPlanner = planner;
        }

        /// <summary>이번 걸음이 피해야 하는 장애물과 유닛의 반지름을 끼운다.</summary>
        /// <remarks>
        /// 끼우지 않으면(기본) 장애물을 하나도 안 본다. 어디서 이 값을
        /// 가져오는지는 여기서 모른다. 부르는 쪽이 정한다.
        /// </remarks>
        /// <param name="obstacles">막는 직사각형 장애물 목록이며 비어 있거나 null이어도 된다.</param>
        /// <param name="collisionRadius">유닛의 반지름(미터)이다. 음수를 주면 0으로 본다.</param>
        public void SetObstacles(IReadOnlyList<PlanarRectangle> obstacles, float collisionRadius)
        {
            _obstacles = obstacles ?? Array.Empty<PlanarRectangle>();
            _collisionRadius = Mathf.Max(0f, collisionRadius);
        }

        /// <summary>이번 걸음이 피해야 하는 다른 유닛을 찾는 방법을 끼운다.</summary>
        /// <remarks>
        /// 끼우지 않으면(기본) 다른 유닛을 하나도 안 본다 — 장애물만 있을 때처럼 걷는다. 다른 유닛이
        /// 어디 있는지, 어떻게 찾는지는 여기서 모른다. 부르는 쪽이 정한다.
        /// </remarks>
        /// <param name="query">방법이며, null이면 다시 아무도 안 보는 상태로 돌아간다.</param>
        public void SetNearbyUnitsQuery(NearbyUnitsQuery query)
        {
            _nearbyUnitsQuery = query;
        }

        /// <summary>최고 이동 속도를 바꾼다.</summary>
        /// <remarks>
        /// 유닛 정의마다 이동 속도가 다르므로(<c>UnitDefinition.MoveSpeed</c>) 조립하는 쪽이 그 값으로
        /// 인스펙터 기본값을 덮어쓸 수 있어야 한다.
        /// </remarks>
        /// <param name="speed">새 최고 이동 속도(초당 미터)이다. 음수를 주면 0으로 본다.</param>
        public void SetSpeed(float speed)
        {
            this.speed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 직전 스텝이 끝났을 때의 로직 위치이다. 표시 계층이 두 스텝 사이를 메우는 데 쓴다.
        /// </summary>
        /// <remarks>
        /// <b>이 값은 표시용이다.</b> 로직은 언제나 지금 위치만 보며, 두 스텝 사이의 중간값을 판단에 쓰지 않는다.
        /// 중간값은 프레임 시각에 달려 있어, 그것이 판단으로 돌아오는 순간 기기 성능이 결과를 바꾼다.
        /// </remarks>
        public Vector3 PreviousLogicPosition { get; private set; }

        /// <summary>가장 최근 스텝이 끝났을 때의 로직 위치이며, 로직이 보는 진짜 위치이다.</summary>
        public Vector3 CurrentLogicPosition { get; private set; }

        /// <summary>가장 최근 스텝이 끝났을 때의 이동 속도(초당 미터)이다.</summary>
        /// <remarks>
        /// <b>이 값은 표시용이다.</b> 걷기 애니메이션처럼 화면이 "지금 움직이고 있는가"를 읽는 자리를 위한 것이며,
        /// 로직은 이 값을 밖에서 다시 읽지 않는다. 읽기만 하므로 화면 프레임에서 읽어도 결과가 흔들리지 않는다.
        /// </remarks>
        public float CurrentSpeed => _currentSpeed;

        /// <inheritdoc />
        public bool HasReachedDestination
        {
            get
            {
                if (!_isInitialized)
                {
                    return false;
                }

                if (!_hasDestination)
                {
                    return true;
                }

                // 경로를 아직 못 받았으면 "아직 모른다"이지 "도착했다"가 아니다.
                // 남은 거리를 재는 계산은 꺾임점이 없을 때 0을 돌려주는데, 그것은 길 끝에 닿은 것과
                // 구별되지 않는다. 여기서 걸러 내지 않으면 유닛이 출발도 전에 도착했다고 말한다.
                if (_plannedCorners.Count == 0)
                {
                    return false;
                }

                // 남은 거리는 목적지까지의 직선이 아니라 경로를 따라 잰다. 직선으로 재면 꺾인 길에서
                // 실제보다 가깝게 보여 모서리 앞에서 도착했다고 말한다.
                var current = PlanarPosition.FromWorld(transform.position);
                return PlanarPathFollower.RemainingDistance(current, _plannedCorners, _cornerIndex) <= stoppingDistance;
            }
        }

        /// <inheritdoc />
        public void Initialize(CharacterBase characterBase)
        {
            _facing = GetComponent<ICharacterFacing>();
            CurrentLogicPosition = transform.position;
            PreviousLogicPosition = CurrentLogicPosition;
            _isInitialized = true;
        }

        /// <summary>스폰·재배치·순간이동의 월드 위치와 자세를 적용하고 표시용 위치 이력을 함께 맞춘다.</summary>
        /// <remarks>
        /// 초기화 전에도 부를 수 있다. 진행 중인 이동 요청은 목적지와 속도를 유지하되 새 자리에서 경로를
        /// 다시 얻는다. 새 경로가 없으면 이동 요청을 중단하고 기존 Stop 규약대로 속도를 감속한다.
        /// 높이는 새 위치의 것을 쓰며, 이전 자리에서 이어지는 꺾임점이나 보간 구간은 남기지 않는다.
        /// 바라볼 대상과 이동 속도 설정은 위치 변경의 대상이 아니다.
        /// 이 메서드는 명시적 위치 설정이며 프레임마다 이동을 적분하는 용도가 아니다. 경로를 따라
        /// 나아가는 일은 이후 FixedUpdate가 맡고, 화면 프레임은 자식 외형만 보간한다.
        /// </remarks>
        /// <param name="position">새 월드 위치이며, 이후 이동에도 이 높이를 쓴다.</param>
        /// <param name="rotation">즉시 적용할 월드 회전이다.</param>
        public void Relocate(Vector3 position, Quaternion rotation)
        {
            var resumeMovement = _hasDestination;
            var destination = _destination.ToWorld(position.y);
            Stop();

            transform.SetPositionAndRotation(position, rotation);
            CurrentLogicPosition = position;
            PreviousLogicPosition = position;
            _cornerHeight = position.y;
            _stuckStepCount = 0;

            if (resumeMovement)
            {
                MoveTo(destination);
            }
        }

        /// <inheritdoc />
        public bool MoveTo(Vector3 destination)
        {
            if (!_isInitialized || _pathPlanner == null)
            {
                return false;
            }

            var planar = PlanarPosition.FromWorld(destination);

            // 목적지가 거의 그대로면 길을 다시 찾지 않는다. 추격 노드는 매 틱 이 메서드를 부르는데,
            // 매번 다시 계획하면 그때마다 경로 계산이 걸리고 그 사이 Tick이 한 걸음도 못 나간다.
            if (_hasDestination && PlanarPosition.SqrDistance(_destination, planar)
                <= DestinationChangeThreshold * DestinationChangeThreshold)
            {
                return true;
            }

            // 경로를 그 자리에서 얻는다. 동기 계약이라 기다릴 것이 없고, 곧바로 새 길을 처음부터 따라간다.
            _plannedWorldCorners.Clear();
            if (!_pathPlanner(transform.position, destination, _plannedWorldCorners) || _plannedWorldCorners.Count == 0)
            {
                return false;
            }

            _destination = planar;
            _hasDestination = true;
            _cornerIndex = 1;
            AdoptCorners(_plannedWorldCorners);
            return true;
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (!_isInitialized)
            {
                return;
            }

            _hasDestination = false;

            // 속도를 여기서 0으로 끊지 않는다. 속도는 가속도가 허락하는 만큼만 변할 수 있는 값인데,
            // 끊으면 그 관계를 깨고 순간이동시키는 것이 된다.
            //
            // 그리고 이 메서드는 "유닛이 선다"가 아니라 "이 노드가 더는 이동을 몰지 않는다"로도 불린다.
            // 행동 트리가 분기를 바꿀 때마다 이전 이동 노드가 초기화되며 불리므로, 여기서 끊으면
            // 분기가 바뀔 때마다 가속을 처음부터 다시 밟는다.
            //
            // 대신 목적지가 없는 동안 Tick이 가속도만큼씩 속도를 줄인다. 곧바로 다시 출발하면
            // 속도가 거의 남아 있고, 오래 서 있으면 0까지 내려간다.

            // 붙잡아 둔 경로도 버린다. 남겨 두면 다시 출발할 때 옛 길을 한 스텝 따라간다.
            _plannedCorners.Clear();
        }

        /// <summary>
        /// 자기 오브젝트의 어빌리티 시스템에서 죽은 상태 태그의 변화를 구독한다. 죽으면 다음 고정 스텝을
        /// 기다리지 않고 곧바로 이동을 멈춘다.
        /// </summary>
        private void OnEnable()
        {
            if (_deathSubscription == null && TryGetComponent<GameplayAbilitySystemComponent>(out var abilitySystem))
            {
                _deathSubscription = abilitySystem.System.Tags.Changed.Subscribe(OnTagChanged);
            }
        }

        private void OnDisable()
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
        }

        /// <summary>죽은 상태 태그를 얻는 순간 이동을 멈춘다.</summary>
        /// <param name="change">어빌리티 시스템 태그 컨테이너의 변화이다.</param>
        private void OnTagChanged(GameplayTagChange change)
        {
            if (change.ChangeKind == GameplayTagChangeKind.Gained && change.Tag.Matches(DeadStateTag))
            {
                Stop();
            }
        }

        /// <summary>고정 주기마다 자기 이동을 한 걸음 계산한다.</summary>
        /// <remarks>
        /// 로직은 고정 주기에서 돌고 그림은 매 프레임 그린다는 규칙을 이 컴포넌트가 스스로 따른다.
        /// 바깥의 루프가 틱을 주는 구조에서는 주입이 오지 않으면 유닛이 제자리에 서는데, 자기가 돌면 그런 조합이 없다.
        /// </remarks>
        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>받은 시간만큼 목적지를 향해 나아간다.</summary>
        /// <param name="deltaTime">이번 걸음이 나타내는 시간(초)이다.</param>
        /// <remarks>
        /// 받은 시간만큼만 나아간다. 프레임 시간이 아니므로 기기가 느려도 한 번에 더 가지 않는다.
        /// </remarks>
        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
            {
                return;
            }

            // 멈춰 있어도 두 값을 맞춰 두어야 표시 계층이 지난 스텝 자리로 되돌아가지 않는다.
            PreviousLogicPosition = CurrentLogicPosition;
            if (!_hasDestination)
            {
                // 갈 곳이 없는 동안에도 속도는 가속도가 허락하는 만큼씩만 줄어든다.
                // 곧바로 다시 출발하면 속도가 남아 있고, 오래 서 있으면 0까지 내려간다.
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, acceleration * deltaTime);
                return;
            }

            if (_plannedCorners.Count == 0)
            {
                return;
            }

            // 속도를 그대로 쓰지 않고 가속과 감속을 거친다. 남은 거리는 목적지까지의 직선이 아니라
            // 경로를 따라 잰다. 직선으로 재면 꺾인 길에서 실제보다 가깝게 보여 너무 일찍 속도를 줄이고
            // 남은 길을 기어간다.
            var current = PlanarPosition.FromWorld(transform.position);
            var speedStep = SpeedRamp.Advance(
                _currentSpeed,
                speed,
                acceleration,
                PlanarPathFollower.RemainingDistance(current, _plannedCorners, _cornerIndex),
                deltaTime,
                autoBraking);
            _currentSpeed = speedStep.Speed;

            var clamped = AdvanceAlongPath(current, speedStep.Distance);

            // 높이는 마지막으로 받은 경로에서 가져온다. 판단에 쓰이지 않는 표시용 값이며,
            // 새 경로를 기다리는 동안에도 값이 있어야 하므로 붙잡아 둔 것을 쓴다.
            transform.position = clamped.Position.ToWorld(_cornerHeight);
            ApplyFacing(clamped.Position - current, deltaTime);
            CurrentLogicPosition = transform.position;
            UpdateStuckTracking(current, clamped);
        }

        /// <summary>경로의 각 구간을 충돌 검사하고 실제로 닿은 꺾임점만 통과 처리한다.</summary>
        private ObstacleClampedStep AdvanceAlongPath(PlanarPosition current, float distance)
        {
            var remaining = distance;
            while (remaining > 0f && _cornerIndex < _plannedCorners.Count)
            {
                var corner = _plannedCorners[_cornerIndex];
                var toCorner = corner - current;
                var segmentLength = toCorner.Magnitude;
                var reachesCorner = segmentLength <= remaining;
                var target = reachesCorner ? corner : current + toCorner.Normalized * remaining;

                // 한 틱이 여러 모서리를 지나도 구간마다 검사한다. 처음과 끝을 직선으로 검사하면
                // 경로가 피한 장애물 안쪽을 가로지르는 선분이 되어 정상적인 우회를 막는다.
                QueryNearbyUnits(current, target);
                var step = PlanarObstacleAvoidance.Advance(
                    current, target, _collisionRadius, _obstacles, _nearbyUnitsBuffer);
                if (reachesCorner && step.Position.Equals(corner))
                {
                    _cornerIndex++;
                }

                current = step.Position;
                if (step.WasBlocked)
                {
                    return step;
                }

                remaining = reachesCorner ? remaining - segmentLength : 0f;
            }

            return new ObstacleClampedStep(current, false);
        }

        /// <summary>
        /// 이번 걸음이 훑는 구간을 넉넉히 담는 원으로 다른 유닛을 묻는다. 방법이 안 끼워져 있으면 빈
        /// 결과로 둔다.
        /// </summary>
        /// <param name="from">이번 스텝을 시작하는 자리이다.</param>
        /// <param name="to">막는 것이 없다면 이번 스텝이 도착할 자리이다.</param>
        private void QueryNearbyUnits(PlanarPosition from, PlanarPosition to)
        {
            if (_nearbyUnitsQuery == null)
            {
                _nearbyUnitsBuffer.Clear();
                return;
            }

            var half = PlanarPosition.Distance(from, to) * 0.5f;
            var sweep = new PlanarCircle(
                new PlanarPosition((from.X + to.X) * 0.5f, (from.Z + to.Z) * 0.5f),
                half + _collisionRadius + SweepPad);
            _nearbyUnitsQuery(sweep, _nearbyUnitsBuffer);
        }

        /// <summary>
        /// 이번 스텝이 장애물에 막혀 거의 못 나아갔으면 정체로 센다. <see cref="StuckStepThreshold"/>만큼
        /// 이어지면 지금 자리에서 길을 다시 얻는다.
        /// </summary>
        /// <param name="before">이번 스텝을 시작한 자리이다.</param>
        /// <param name="clamped">장애물을 반영한 이번 스텝의 결과이다.</param>
        private void UpdateStuckTracking(PlanarPosition before, ObstacleClampedStep clamped)
        {
            var moved = PlanarPosition.Distance(before, clamped.Position);
            if (!clamped.WasBlocked || moved > StuckMovementEpsilon)
            {
                _stuckStepCount = 0;
                return;
            }

            _stuckStepCount++;
            if (_stuckStepCount < StuckStepThreshold)
            {
                return;
            }

            _stuckStepCount = 0;
            Replan();
        }

        /// <summary>지금 자리에서 같은 목적지까지의 길을 다시 얻는다.</summary>
        /// <remarks>경로를 얻는 방법이 없으면(있을 수 없는 상태이지만) 아무 일도 하지 않는다.</remarks>
        private void Replan()
        {
            if (_pathPlanner == null)
            {
                return;
            }

            _plannedWorldCorners.Clear();
            var destinationWorld = _destination.ToWorld(_cornerHeight);
            if (!_pathPlanner(transform.position, destinationWorld, _plannedWorldCorners) || _plannedWorldCorners.Count == 0)
            {
                // 새 길도 없다. 다음 정체 판정에서 다시 시도한다.
                return;
            }

            _cornerIndex = 1;
            AdoptCorners(_plannedWorldCorners);
        }

        /// <summary>받은 꺾임점을 평면 좌표로 옮겨 붙잡고, 표시용 높이를 지금 꺾임점에서 읽는다.</summary>
        /// <param name="corners">출발점을 첫 항목으로 담은 월드 좌표 꺾임점이며 비어 있지 않다.</param>
        private void AdoptCorners(IReadOnlyList<Vector3> corners)
        {
            _plannedCorners.Clear();
            for (var index = 0; index < corners.Count; index++)
            {
                _plannedCorners.Add(PlanarPosition.FromWorld(corners[index]));
            }

            _cornerHeight = corners[Mathf.Clamp(_cornerIndex, 0, corners.Count - 1)].y;
        }

        /// <summary>
        /// 이번 스텝에 움직인 쪽을 바라보게 한다. 바라보는 방향을 도맡는 구성요소가 있으면 그쪽에 방향만 알린다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>회전의 주인이 따로 있으면 여기서 돌리지 않는다.</b> <see cref="ICharacterFacing"/>을 구현한
        /// 구성요소가 같은 캐릭터에 있으면 이번 스텝의 이동 방향을 알리기만 한다. 그 구성요소가 제 각속도로
        /// 돌며, 조준 같은 다른 요구와 이동 방향을 한곳에서 견준다. 둘이 같은 스텝에 회전을 쓰면 구성요소
        /// 순서에 따라 결과가 흔들린다.
        /// </para>
        /// <para>
        /// <b>없으면 여기서 돌린다.</b> 돌리지 않으면 유닛이 한 방향을 본 채 옆으로 미끄러진다. 바라보는
        /// 방향은 표시만이 아니다 — <b>사격은 대상이 정면에 있을 때만 나가므로</b> 회전은 판단에 들어간다.
        /// </para>
        /// <para>
        /// <b>가는 길은 바뀌지 않는다.</b> 위치는 경로 추종기가 정하고 여기서는 바라보는 방향만 바꾼다.
        /// 그래서 이것은 조향이 아니다.
        /// </para>
        /// </remarks>
        /// <param name="movement">이번 스텝에 움직인 평면 변위이다.</param>
        /// <param name="deltaTime">이 스텝의 길이(초)이다.</param>
        private void ApplyFacing(PlanarPosition movement, float deltaTime)
        {
            if (_facing != null)
            {
                _facing.ReportMovementDirection(movement.ToWorld());
                return;
            }

            var facing = PlanarFacing.Advance(
                PlanarPosition.FromWorld(transform.forward),
                movement,
                angularSpeed * deltaTime);

            var forward = facing.ToWorld();
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
