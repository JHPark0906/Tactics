using System;
using HS.Framework.AI.Behaviour;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Ability.BehaviourTree
{
    /// <summary>태그로 지정한 어빌리티를 활성화하는 자리의 설명이다.</summary>
    /// <remarks>
    /// <para>
    /// 어빌리티 시스템은 유닛마다 다르므로 만들 때 유닛에서 찾는다. 유닛에
    /// <see cref="GameplayAbilitySystemComponent"/>가 없으면 null을 돌려 그 자리가 트리에서 빠진다.
    /// </para>
    /// <para>
    /// <b>태그 이름이 어긋나면 만들지 않는다.</b> 실행 자리는 잘못된 태그에 예외를 던지는데, 에셋에
    /// 적힌 오타 하나가 트리 전체를 만들다 말게 하면 어느 자리가 문제인지 알 수 없다. 여기서는
    /// 그 자리만 비운다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class ActivateAbilityDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("활성화할 어빌리티의 식별 태그 이름이다.")]
        private string abilityTagName;

        /// <inheritdoc />
        public override string DisplayName
            => GameplayTag.TryParse(abilityTagName, out var tag) ? $"Ability: {tag}" : "Ability";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (!GameplayTag.TryParse(abilityTagName, out var tag))
            {
                return null;
            }

            return context.Owner != null && context.Owner.TryGetComponent<GameplayAbilitySystemComponent>(out var system)
                ? new ActivateAbilityBehaviour(system.System, tag)
                : null;
        }
    }
}
