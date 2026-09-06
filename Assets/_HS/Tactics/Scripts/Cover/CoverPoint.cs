using System;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Units;
using R3;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 유닛이 몸을 숨길 수 있는 지점이다. 한 번에 한 유닛만 차지하며, 체력을 가져 부서질 수 있다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>엄폐물은 중립 유닛이다.</b> 진영, 체력, 어빌리티 시스템을 가진 액터이되 행동 트리와 이동은 없다. 진영은
    /// 미지정으로 두며, 진영 관계 규칙이 미지정을 중립으로 보므로 적이 엄폐물을 겨누지 않고 승패 집계에도 들어가지 않는다.
    /// 그 둘에 엄폐물을 위한 규칙은 따로 없다.
    /// </para>
    /// <para>
    /// <b>체력과 흡수 확률은 어트리뷰트 집합이 갖는다.</b> 엄폐물 프리팹은 <see cref="AttributeSetComponent"/>에 엄폐물
    /// 어트리뷰트 묶음(체력, 최대 체력, 흡수 확률)을 연결하고, 체력 문과 이 지점은 그 집합에서 이름으로 정의를 찾는다.
    /// 묶음이 없으면 체력 문이 정의를 몰라 오류를 남기고, 흡수 확률은 0으로 읽혀 대신 맞아 주지 않는다.
    /// 흡수 확률이 빠진 것은 깨어날 때 한 번 알린다.
    /// </para>
    /// <para>
    /// <b>부서지는 것은 어빌리티다.</b> 체력 문이 보낸 사망 이벤트를 <c>DeathAbility</c>가 받아 죽은 상태 태그만
    /// 붙인다. 이 컴포넌트는 자기 오브젝트의 그 태그 변화를 직접 구독해 <see cref="MarkDestroyed"/>를 스스로
    /// 부른다. 엄폐물에는 소멸 어빌리티가 없으므로 <c>Object.Destroy</c>는 아무도 부르지 않는다 — 부서진
    /// 엄폐물은 파괴된 것으로 표시된 채 그대로 씬에 남는다. 대신 맞아 주는 것은 엄폐 중인 유닛 쪽 회피
    /// 어빌리티가 하며, 이 지점은 확률만 알려 준다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(HealthAttributeComponent))]
    [RequireComponent(typeof(GameplayAbilitySystemComponent))]
    public sealed class CoverPoint : MonoBehaviour
    {
        [Tooltip("이 엄폐물이 평면 위에서 막는 반너비(X)·반깊이(Z)이다(미터). " +
                 "지금은 엄폐물 프리팹이 아직 없어 유닛 반지름 기본값에 맞춘 임시값이다 — " +
                 "실제 시각 크기가 정해지면 프리팹마다 맞춰 조정한다.")]
        [SerializeField]
        private Vector2 halfExtents = new(0.5f, 0.5f);

        /// <summary>죽은 상태를 나타내는 태그이다. 이 태그를 얻는 순간 이 지점을 파괴된 것으로 표시한다.</summary>
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        private GameObject _occupant;
        private HealthAttributeComponent _health;
        private bool _isDestroyed;
        private IDisposable _deathSubscription;

        /// <summary>엄폐 지점의 월드 좌표이다.</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// 이 엄폐물이 평면 위에서 막는 영역이다. 시야 그래프와 걸음 자르기가 장애물로 쓴다.
        /// </summary>
        /// <remarks>
        /// 방향은 이 오브젝트의 <c>transform.forward</c> 그대로 쓴다. 엄폐 선택 규칙은 방향을 보지
        /// 않으므로(<see cref="CoverSelection"/> 참고) 레벨 디자이너가 놓은 회전을 그대로 반영해도
        /// 선택에는 영향이 없다.
        /// </remarks>
        public PlanarRectangle Bounds => new(
            PlanarPosition.FromWorld(Position),
            new PlanarPosition(halfExtents.x, halfExtents.y),
            PlanarPosition.FromWorld(transform.forward));

        /// <summary>이 엄폐물이 파괴되어 더는 장애물이 아니게 되면 발행된다.</summary>
        /// <remarks>
        /// <see cref="MarkDestroyed"/>가 맨 처음 하는 일이다. 시야 그래프를 들고 있는 쪽이 이 신호로
        /// 다시 굽는다 — 이미 걷고 있는 유닛은 자기 경로를 따로 들고 있어 이 이벤트를 몰라도 된다
        /// (제거된 장애물은 지나갈 수 있는 자리를 넓힐 뿐 옛 경로를 틀리게 만들지 않는다).
        /// </remarks>
        public event Action Destroyed;

        /// <summary>
        /// 엄폐 중인 유닛이 맞을 때 이 엄폐물이 대신 받을 확률이다. 어트리뷰트 집합의 흡수 확률을 읽으며, 없으면 0이다.
        /// </summary>
        public float AbsorbChance
        {
            get
            {
                if (TryGetComponent<AttributeSetComponent>(out var attributeSet)
                    && attributeSet.Attributes.TryFindDefinition(UnitAttributeIds.CoverAbsorbChance, out var absorbChance))
                {
                    return Mathf.Clamp01(attributeSet.Attributes.GetCurrentValue(absorbChance));
                }

                return 0f;
            }
        }

        /// <summary>
        /// 이 엄폐물의 체력 문이다. 처음 쓸 때 어트리뷰트 집합에서 체력 정의를 찾아 연결한다.
        /// </summary>
        public HealthAttributeComponent Health
        {
            get
            {
                if (_health == null)
                {
                    _health = GetComponent<HealthAttributeComponent>();
                    if (_health != null && TryGetComponent<AttributeSetComponent>(out var attributeSet))
                    {
                        HealthAttributeWiring.TryConfigure(_health, attributeSet.Attributes);
                    }
                }

                return _health;
            }
        }

        /// <summary>파괴되어 더는 쓸 수 없는지 여부이다.</summary>
        public bool IsDestroyed => _isDestroyed;

        /// <summary>어떤 유닛이 차지하고 있는지 여부이다.</summary>
        public bool IsOccupied => _occupant != null;

        /// <summary>지금 차지하고 있는 유닛이며 없으면 null이다.</summary>
        public GameObject Occupant => _occupant;

        /// <summary>
        /// 유닛이 이 지점을 차지한다. 이미 같은 유닛이 차지하고 있으면 그대로 성공한다.
        /// </summary>
        /// <param name="occupant">차지할 유닛이다.</param>
        /// <returns>차지했으면 true이며, 파괴되었거나 다른 유닛이 차지하고 있으면 false이다.</returns>
        public bool TryOccupy(GameObject occupant)
        {
            if (occupant == null)
            {
                return false;
            }

            if (_isDestroyed || (IsOccupied && _occupant != occupant))
            {
                return false;
            }

            _occupant = occupant;
            return true;
        }

        /// <summary>차지하고 있던 유닛이 이 지점을 놓는다. 다른 유닛이 차지하고 있으면 아무 일도 하지 않는다.</summary>
        /// <param name="occupant">놓는 유닛이다.</param>
        public void Release(GameObject occupant)
        {
            if (occupant != null && _occupant == occupant)
            {
                _occupant = null;
            }
        }

        /// <summary>
        /// 이 지점을 파괴된 것으로 표시하고, 차지하고 있던 유닛의 예약을 놓게 한다.
        /// </summary>
        public void MarkDestroyed()
        {
            _isDestroyed = true;
            Destroyed?.Invoke();
            var occupant = _occupant;
            _occupant = null;
            if (occupant != null && occupant.TryGetComponent<UnitCoverState>(out var coverState))
            {
                coverState.ReleaseCover();
            }
        }

        /// <summary>엄폐 후보로 변환한다. 파괴된 자리는 점유된 것과 같게 다뤄 후보에서 빠지게 한다.</summary>
        /// <returns>이 지점의 후보 표현이다.</returns>
        public CoverCandidate ToCandidate()
        {
            return new CoverCandidate(Position, IsOccupied || _isDestroyed);
        }

        /// <summary>흡수 확률 정의가 없어 대신 맞아 주지 않게 될 엄폐물은 깨어날 때 한 번 알린다.</summary>
        private void Awake()
        {
            if (!TryGetComponent<AttributeSetComponent>(out var attributeSet)
                || !attributeSet.Attributes.TryFindDefinition(UnitAttributeIds.CoverAbsorbChance, out _))
            {
                Debug.LogWarning(
                    $"[CoverPoint] {name}의 어트리뷰트 집합에 {UnitAttributeIds.CoverAbsorbChance}가 없어 대신 맞아 주지 않는다. " +
                    "엄폐물 어트리뷰트 묶음을 AttributeSetComponent에 연결해야 한다.",
                    this);
            }
        }

        /// <summary>
        /// 자기 오브젝트의 어빌리티 시스템에서 죽은 상태 태그의 변화를 구독한다. 죽으면 다음 고정 스텝을
        /// 기다리지 않고 곧바로 이 지점을 파괴된 것으로 표시한다.
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

        /// <summary>죽은 상태 태그를 얻는 순간 이 지점을 파괴된 것으로 표시한다.</summary>
        /// <param name="change">어빌리티 시스템 태그 컨테이너의 변화이다.</param>
        private void OnTagChanged(GameplayTagChange change)
        {
            if (change.ChangeKind == GameplayTagChangeKind.Gained && change.Tag.Matches(DeadStateTag))
            {
                MarkDestroyed();
            }
        }
    }
}
