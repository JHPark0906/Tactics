using System;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 엄폐 중인 유닛이 맞을 때 확률로 피해를 엄폐물에 넘기는 어빌리티이다. 활성 동안 유닛 체력 어트리뷰트의 기본값
    /// 변화에 끼어들어, 회피에 성공한 피해는 유닛에 닿지 않고 엄폐물이 대신 받는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>피해를 어디서 가로채는가.</b> 체력 문은 피해를 어트리뷰트 기본값 변화에서 판정하므로, 사격이 직접 주든 효과로
    /// 주든 모두 같은 필터를 지난다. 이 어빌리티는 활성화될 때 그 필터에 자리를 잡고 끝날 때 뺀다. 회복(증가)과 저장
    /// 복원은 건드리지 않는다.
    /// </para>
    /// <para>
    /// <b>회피는 굴림으로 정해지고 효과 적용 결과와 무관하게 성립한다.</b> 굴림이 엄폐물의 흡수 확률 아래면 유닛의 체력은
    /// 그대로 두고(그래서 유닛 쪽 피해 알림도 없다) 엄폐물의 효과 실행기에 피해 효과를 적용한다. 엄폐물이 그 효과에
    /// 면역이면 피해는 어디에도 닿지 않고 사라진다. 엄폐가 유닛을 지켰다는 사실이 엄폐물의 사정에 달리지 않게 하기
    /// 위해서이다.
    /// </para>
    /// <para>
    /// <b>부서졌거나 죽은 엄폐물은 굴리지 않는다.</b> 엄폐물이 부서지면 점유가 풀리며 엄폐 중 태그가 떨어져 이 어빌리티도
    /// 끝나지만, 같은 필터 호출 안에서 잇달아 오는 피해가 있을 수 있으므로 매번 엄폐물의 상태를 다시 본다.
    /// </para>
    /// <para>
    /// 굴림은 Unity 난수로 한다. 검사는 굴림이 아니라 흡수 확률을 0이나 1로 두어 결과를 정한다.
    /// </para>
    /// </remarks>
    public sealed class CoverEvasionAbility : GameplayAbility, IAttributeBaseValueFilter
    {
        private IDisposable _filterHandle;
        private UnitCoverState _coverState;

        /// <inheritdoc />
        /// <remarks>엄폐 상태를 가진 액터만 회피할 수 있다.</remarks>
        public override bool CanActivate()
        {
            return System != null && System.Owner != null && System.Owner.TryGetComponent<UnitCoverState>(out _);
        }

        /// <inheritdoc />
        /// <remarks>체력 정의가 없으면 필터를 걸지 않는다. 그 빠짐은 유닛이 체력을 조립할 때 이미 알린다.</remarks>
        protected override void OnActivate()
        {
            System.Owner.TryGetComponent(out _coverState);
            if (System.Attributes.TryFindDefinition(UnitAttributeIds.Health, out var health))
            {
                _filterHandle = System.Attributes.AddBaseValueFilter(health, this);
            }
        }

        /// <inheritdoc />
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            return GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            _filterHandle?.Dispose();
            _filterHandle = null;
            _coverState = null;
        }

        /// <inheritdoc />
        float IAttributeBaseValueFilter.FilterBaseValue(
            AttributeDefinition definition,
            float currentBaseValue,
            float proposedBaseValue,
            in AttributeChangeContext context)
        {
            if (proposedBaseValue >= currentBaseValue || context.Cause is AttributeSetSnapshot)
            {
                return proposedBaseValue;
            }

            var cover = ResolveUsableCover();
            if (cover == null || !HitChanceCalculator.Roll(cover.AbsorbChance))
            {
                return proposedBaseValue;
            }

            HandDamageToCover(cover, currentBaseValue - proposedBaseValue, context.Instigator);
            return currentBaseValue;
        }

        /// <summary>지금 대신 맞아 줄 수 있는 엄폐물을 고른다. 엄폐 중이 아니거나 엄폐물이 부서졌으면 null이다.</summary>
        private CoverPoint ResolveUsableCover()
        {
            if (_coverState == null || !_coverState.IsInCover)
            {
                return null;
            }

            var cover = _coverState.ClaimedCover;
            if (cover == null || cover.IsDestroyed || cover.Health == null || cover.Health.IsDead)
            {
                return null;
            }

            return cover;
        }

        /// <summary>회피한 피해를 정의의 피해 효과로 엄폐물에 넘긴다. 효과나 실행기가 없으면 피해는 사라진다.</summary>
        /// <param name="cover">대신 맞아 줄 엄폐물이다.</param>
        /// <param name="damage">유닛이 받을 뻔한 피해량이다.</param>
        /// <param name="instigator">원래의 공격자이며 엄폐물 쪽 알림에 그대로 실린다.</param>
        private void HandDamageToCover(CoverPoint cover, float damage, GameObject instigator)
        {
            var damageEffect = Definition is CoverEvasionAbilityDefinition definition ? definition.DamageEffect : null;
            if (damageEffect == null || !cover.TryGetComponent<GameplayEffectComponent>(out var coverEffects))
            {
                return;
            }

            var context = new GameplayEffectContext(instigator, this, System.Attributes)
                .SetByCaller(DamageEffectDefinition.DamageTagName, damage);
            coverEffects.ApplyEffect(damageEffect, this, context);
        }
    }
}
