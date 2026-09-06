using HS.Framework.Ability.Abilities;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 엄폐 유지 어빌리티를 액터에게 부여하기 위한 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// 이 어빌리티를 끼우는 분기는 언제나 교전보다 아래에 놓인다.
    /// 그 순서가 흔들리면 유닛이 엄폐에 앉은 채 사격하지 않으므로, 우선순위는 정의가 아니라 분기 제공자가 고정한다.
    /// 자기 이름의 파일에 있어야 에셋으로 만들 수 있으므로 엄폐 확보 정의와 파일을 나눈다.
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Maintain Cover Ability", fileName = "MaintainCoverAbility")]
    public sealed class MaintainCoverAbilityDefinition : GameplayAbilityDefinition
    {
        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new MaintainCoverAbility();
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 엄폐 유지 어빌리티 정의를 만든다.
        /// </summary>
        /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static MaintainCoverAbilityDefinition CreateRuntime(string abilityTagName)
        {
            var definition = CreateInstance<MaintainCoverAbilityDefinition>();
            definition.ConfigureRuntime(abilityTagName);
            return definition;
        }
    }
}
