using System;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Character;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Units;
using R3;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 유닛이 확보한 엄폐 지점과 실제 엄폐 여부를 관리하는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 엄폐 지점을 확보하는 것과 그 자리에서 실제로 엄폐하는 것은 다르다. 확보는 다른 유닛이
    /// 같은 지점을 고르지 못하게 예약하는 것이고, 엄폐는 유닛이 그 자리에 도착해야 성립한다.
    /// 그래서 <see cref="IsInCover"/>는 확보 여부와 도착 여부를 함께 본다.
    /// </para>
    /// <para>
    /// <b>도착 판정과 이동기의 멈춤 거리의 관계는 여기 한 곳에서 정한다.</b> 이동기는 목적지 앞 멈춤 거리 안에
    /// 들면 도착했다고 하고, 엄폐 확보는 그 말을 듣고 끝난다. 그 자리가 여기서 도착으로 인정되지 않으면 확보는
    /// 자리를 놓고, 다음 판단이 같은 자리를 다시 잡고, 이동기는 곧바로 도착했다고 하는 되풀이가 생긴다.
    /// 그래서 설정한 도착 반경을 쓰되 <b>이동기의 멈춤 거리 아래로는 내려가지 않게 한다</b>. 멈춤 거리는
    /// <see cref="IStoppingDistanceProvider"/>로 이동기에 묻고, 알려 주지 않는 이동기라면 설정한 반경이 전부다.
    /// </para>
    /// <para>
    /// <b>엄폐 중이라는 판정은 여기 한 곳에 있다.</b> 그 판정이 바뀌면 어빌리티 시스템의 태그 그릇에 엄폐 중 태그를
    /// 넣고 뺀다. 회피 어빌리티는 그 태그를 트리거로 켜지고 꺼지므로 판정을 따로 하지 않는다. 도착은 위치에 달려 있어
    /// 고정 스텝마다 다시 보고, 확보와 해제는 그 자리에서 곧바로 맞춘다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitCoverState : MonoBehaviour, IUnitCoverState
    {
        /// <summary>엄폐 중임을 나타내는 상태 태그이다.</summary>
        private static readonly GameplayTag InCoverTag = GameplayTag.Parse(UnitAbilityTags.InCoverState);

        /// <summary>죽은 상태를 나타내는 태그이다. 이 태그를 얻는 순간 확보한 엄폐 지점을 놓는다.</summary>
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        /// <summary>
        /// 이동기의 멈춤 거리에 더하는 여유(미터)이다. 이동기는 남은 경로 길이로 멈춤을 재고 여기서는 평면 거리로
        /// 재므로, 마지막 직선 구간에서 둘이 같아야 하는 값이 반올림으로 아주 조금 어긋나는 것을 덮는다.
        /// </summary>
        private const float ArrivalSlack = 0.05f;

        [Tooltip("엄폐 지점에서 이 거리 안에 있으면 그 자리에 자리 잡은 것으로 본다(미터).\n" +
                 "이동기가 이보다 멀리서 멈추면 그 멈춤 거리에 여유를 더한 값으로 올려 쓴다 — 이동기가 멈춘 자리는 도착이어야 한다.")]
        [SerializeField]
        [Min(0.05f)]
        private float coverArrivalRadius = 1f;

        private CoverPoint _claimedCover;
        private GameplayAbilitySystemComponent _abilitySystem;
        private IStoppingDistanceProvider _stoppingDistanceSource;
        private bool _hasInCoverTag;
        private bool _hasWarnedAboutArrivalRadius;
        private IDisposable _deathSubscription;

        /// <summary>엄폐 지점에 도착한 것으로 보는 거리(미터)이며, 이동기가 멈추는 거리보다 작지 않다.</summary>
        public float CoverArrivalRadius
        {
            get
            {
                var configured = Mathf.Max(0.05f, coverArrivalRadius);
                var source = ResolveStoppingDistanceSource();
                if (source == null)
                {
                    return configured;
                }

                var minimum = source.StoppingDistance + ArrivalSlack;
                if (configured >= minimum)
                {
                    return configured;
                }

                WarnAboutArrivalRadiusOnce(configured, minimum);
                return minimum;
            }
        }

        /// <inheritdoc />
        public CoverPoint ClaimedCover => _claimedCover != null ? _claimedCover : null;

        /// <summary>엄폐 지점을 확보한 상태인지 여부이며 아직 도착하지 않았을 수도 있다.</summary>
        public bool HasCoverClaim => ClaimedCover != null;

        /// <inheritdoc />
        public bool IsInCover
        {
            get
            {
                var cover = ClaimedCover;
                if (cover == null)
                {
                    return false;
                }

                var offset = cover.Position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= CoverArrivalRadius * CoverArrivalRadius)
                {
                    return true;
                }

                // 실제 유닛은 엄폐물 중심이 아닌 충돌 영역 밖에 선다. 크기가 큰 엄폐물에도
                // 같은 규약을 적용하고, 이동기가 외부 목적지 앞에서 멈추는 거리까지 인정한다.
                if (!TryGetComponent<TacticalUnit>(out var unit))
                {
                    return false;
                }

                var radius = unit.Definition != null ? unit.Definition.Radius : 0.5f;
                var stoppingDistance = ResolveStoppingDistanceSource()?.StoppingDistance ?? 0f;
                return CoverApproachGeometry.DistanceOutside(
                    cover.Bounds, PlanarPosition.FromWorld(transform.position), radius)
                    <= stoppingDistance + CoverApproachGeometry.ClearancePadding + ArrivalSlack;
            }
        }

        /// <summary>
        /// 엄폐 지점을 확보한다. 이미 다른 지점을 확보하고 있었으면 그 지점을 먼저 놓아 준다.
        /// </summary>
        /// <param name="coverPoint">확보할 엄폐 지점이다.</param>
        /// <returns>확보에 성공했으면 true, 다른 유닛이 이미 점유하고 있으면 false이다.</returns>
        public bool ClaimCover(CoverPoint coverPoint)
        {
            if (coverPoint == null)
            {
                return false;
            }

            if (ClaimedCover == coverPoint)
            {
                return true;
            }

            if (!coverPoint.TryOccupy(gameObject))
            {
                return false;
            }

            ReleaseCover();
            _claimedCover = coverPoint;
            RefreshInCoverTag();
            return true;
        }

        /// <summary>
        /// 확보한 엄폐 지점을 놓아 준다. 확보한 지점이 없으면 아무 일도 하지 않는다.
        /// </summary>
        public void ReleaseCover()
        {
            var cover = ClaimedCover;
            if (cover != null)
            {
                cover.Release(gameObject);
            }

            _claimedCover = null;
            RefreshInCoverTag();
        }

        /// <summary>
        /// 엄폐 중 여부를 다시 판정해 어빌리티 시스템의 태그를 맞춘다. 고정 스텝마다 불리며, 위치를 코드로 옮긴 뒤
        /// 곧바로 반영하고 싶을 때 직접 부를 수 있다. 어빌리티 시스템이 없으면 아무 일도 하지 않는다.
        /// </summary>
        public void RefreshInCoverTag()
        {
            var inCover = IsInCover;
            if (inCover == _hasInCoverTag)
            {
                return;
            }

            if (_abilitySystem == null && !TryGetComponent(out _abilitySystem))
            {
                return;
            }

            var tags = _abilitySystem.System.Tags;
            if (inCover)
            {
                tags.AddTag(InCoverTag);
            }
            else
            {
                tags.RemoveTag(InCoverTag);
            }

            _hasInCoverTag = inCover;
        }

        private void FixedUpdate()
        {
            RefreshInCoverTag();
        }

        /// <summary>같은 오브젝트에서 멈춤 거리를 알려 주는 이동기를 찾는다. 없으면 null이며 다음에 다시 찾는다.</summary>
        private IStoppingDistanceProvider ResolveStoppingDistanceSource()
        {
            if (_stoppingDistanceSource == null)
            {
                _stoppingDistanceSource = GetComponent<IStoppingDistanceProvider>();
            }

            return _stoppingDistanceSource;
        }

        /// <summary>설정한 도착 반경이 이동기의 멈춤 거리보다 작아 올려 쓴다는 것을 유닛마다 한 번 알린다.</summary>
        /// <param name="configured">설정한 도착 반경(미터)이다.</param>
        /// <param name="minimum">대신 쓰는 값(미터)이다.</param>
        private void WarnAboutArrivalRadiusOnce(float configured, float minimum)
        {
            if (_hasWarnedAboutArrivalRadius)
            {
                return;
            }

            _hasWarnedAboutArrivalRadius = true;
            Debug.LogWarning(
                $"[UnitCoverState] {name}의 엄폐 도착 반경 {configured:0.##}m가 이동기의 멈춤 거리보다 작아 {minimum:0.##}m로 올려 쓴다. " +
                "이동기가 멈춘 자리가 도착으로 인정되어야 엄폐를 놓았다 잡기를 되풀이하지 않는다.",
                this);
        }

        /// <summary>
        /// 자기 오브젝트의 어빌리티 시스템에서 죽은 상태 태그의 변화를 구독한다. 죽으면 다음 고정 스텝을
        /// 기다리지 않고 곧바로 엄폐 예약을 놓는다.
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
            ReleaseCover();
        }

        private void OnDestroy()
        {
            ReleaseCover();
        }

        /// <summary>죽은 상태 태그를 얻는 순간 확보한 엄폐 예약을 놓는다.</summary>
        /// <param name="change">어빌리티 시스템 태그 컨테이너의 변화이다.</param>
        private void OnTagChanged(GameplayTagChange change)
        {
            if (change.ChangeKind == GameplayTagChangeKind.Gained && change.Tag.Matches(DeadStateTag))
            {
                ReleaseCover();
            }
        }
    }
}
