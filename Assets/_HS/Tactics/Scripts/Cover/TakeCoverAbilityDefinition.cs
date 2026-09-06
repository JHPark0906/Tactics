using HS.Framework.Ability.Abilities;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 엄폐 확보 어빌리티를 액터에게 부여하기 위한 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// 사거리와 탐색 반경 같은 수치는 이 에셋이 아니라 유닛 정의와 센서에서 읽는다.
    /// 같은 엄폐 어빌리티를 여러 병종이 공유하되 수치는 병종마다 다르기 때문이다.
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Take Cover Ability", fileName = "TakeCoverAbility")]
    public sealed class TakeCoverAbilityDefinition : GameplayAbilityDefinition
    {
        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new TakeCoverAbility();
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 엄폐 확보 어빌리티 정의를 만든다.
        /// </summary>
        /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static TakeCoverAbilityDefinition CreateRuntime(string abilityTagName)
        {
            var definition = CreateInstance<TakeCoverAbilityDefinition>();
            definition.ConfigureRuntime(abilityTagName);
            return definition;
        }
    }
}
