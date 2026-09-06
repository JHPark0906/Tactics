using HS.Framework.Ability.Abilities;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 조준 어빌리티를 액터에게 부여하기 위한 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>각속도는 여기 없다.</b> 그것은 유닛마다 다른 값이라 유닛 정의에 있고, 조립이 방향 구성요소에 넣는다.
    /// 여기 있는 허용 각은 "얼마나 맞으면 조준을 마친 것으로 보는가"라는 규칙이라 유닛이 아니라 이 에셋의 값이다.
    /// </para>
    /// <para>
    /// 허용 각이 0이면 정확히 마주 볼 때까지 끝나지 않는다. 부동소수 계산에서 그 순간은 오지 않으므로 최소값을 둔다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Aim Ability", fileName = "AimAbility")]
    public sealed class AimAbilityDefinition : GameplayAbilityDefinition
    {
        /// <summary>정하지 않았을 때의 허용 각(도)이며 임시값이다.</summary>
        public const float DefaultToleranceDegrees = 5f;

        /// <summary>허용 각이 내려갈 수 없는 최소값(도)이다. 이보다 작으면 조준이 끝나지 않을 수 있다.</summary>
        public const float MinimumToleranceDegrees = 0.1f;

        [Tooltip("대상이 정면에서 이 각(도) 안에 들면 조준을 마친 것으로 본다. 임시값이다.\n" +
                 "작을수록 더 정확히 겨눈 뒤에 쏘므로 사격이 늦어진다.")]
        [SerializeField]
        [Min(MinimumToleranceDegrees)]
        private float toleranceDegrees = DefaultToleranceDegrees;

        /// <summary>마주 봤다고 볼 허용 각(도)이며 항상 최소값 이상이다.</summary>
        public float ToleranceDegrees => Mathf.Max(MinimumToleranceDegrees, toleranceDegrees);

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new AimAbility();
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 조준 어빌리티 정의를 만든다.
        /// </summary>
        /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="toleranceDegrees">마주 봤다고 볼 허용 각(도)이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static AimAbilityDefinition CreateRuntime(
            string abilityTagName,
            float toleranceDegrees = DefaultToleranceDegrees)
        {
            var definition = CreateInstance<AimAbilityDefinition>();
            definition.ConfigureRuntime(abilityTagName);
            definition.toleranceDegrees = toleranceDegrees;
            return definition;
        }
    }
}
