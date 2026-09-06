using HS.Framework.Ability.Abilities;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 접근 어빌리티를 액터에게 부여하기 위한 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <b>사거리는 여기 없다.</b> 그것은 병종마다 다른 값이라 유닛 정의에 있다. 여기 있는 것은 "사거리의 몇
    /// 안쪽에서 멈추는가"와 "적 주위 자리를 어떻게 나누는가"라는 규칙이며, 같은 규칙을 여러 병종이 나눠 쓴다.
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Approach Ability", fileName = "ApproachAbility")]
    public sealed class ApproachAbilityDefinition : GameplayAbilityDefinition
    {
        /// <summary>정하지 않았을 때 사거리에 곱할 비율이며 임시값이다.</summary>
        public const float DefaultRangeRatio = 0.9f;

        /// <summary>정하지 않았을 때 적 주위에 나눌 자리의 수이며 임시값이다.</summary>
        public const int DefaultSlotCount = 6;

        /// <summary>정하지 않았을 때 자리를 펼칠 각도의 폭(도)이며 임시값이다.</summary>
        public const float DefaultArcDegrees = 180f;

        private static ApproachAbilityDefinition _fallback;

        [Tooltip("사거리에 이 비율을 곱한 거리에서 멈추고, 적 주위 자리도 그 거리에 놓는다. 임시값이다.\n" +
                 "1이면 사거리 끝에서 멈추므로 대상이 한 걸음만 물러나도 사거리를 벗어난다.")]
        [SerializeField]
        [Range(0.1f, 1f)]
        private float rangeRatio = DefaultRangeRatio;

        [Tooltip("적 주위에 나눌 자리의 수이다. 1 이하이면 나누지 않고 적의 자리로 곧장 간다. 임시값이다.")]
        [SerializeField]
        [Min(1)]
        private int slotCount = DefaultSlotCount;

        [Tooltip("자리를 펼칠 각도의 폭(도)이다. 180이면 우리 쪽을 향한 반원이다. 임시값이다.")]
        [SerializeField]
        [Range(0f, 360f)]
        private float arcDegrees = DefaultArcDegrees;

        /// <summary>사거리에 곱해 멈출 거리를 정하는 비율이다.</summary>
        public float RangeRatio => Mathf.Clamp(rangeRatio, 0.1f, 1f);

        /// <summary>적 주위에 나눌 자리의 수이며 항상 1 이상이다.</summary>
        public int SlotCount => Mathf.Max(1, slotCount);

        /// <summary>자리를 펼칠 각도의 폭(도)이다.</summary>
        public float ArcDegrees => Mathf.Clamp(arcDegrees, 0f, 360f);

        /// <summary>
        /// 다른 정의로 부여된 접근 어빌리티가 쓸 기본 규칙이다.
        /// </summary>
        /// <remarks>
        /// 값을 읽을 곳이 없을 때 코드 안의 숫자로 물러나면 그 수가 어디서 왔는지 알 수 없다.
        /// 여기 하나를 두어 기본 규칙도 다른 에셋과 같은 자리에서 읽게 한다.
        /// </remarks>
        public static ApproachAbilityDefinition Fallback =>
            _fallback != null ? _fallback : _fallback = CreateInstance<ApproachAbilityDefinition>();

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new ApproachAbility();
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 접근 어빌리티 정의를 만든다.
        /// </summary>
        /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="rangeRatio">사거리에 곱해 멈출 거리를 정하는 비율이다.</param>
        /// <param name="slotCount">적 주위에 나눌 자리의 수이다.</param>
        /// <param name="arcDegrees">자리를 펼칠 각도의 폭(도)이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static ApproachAbilityDefinition CreateRuntime(
            string abilityTagName,
            float rangeRatio = DefaultRangeRatio,
            int slotCount = DefaultSlotCount,
            float arcDegrees = DefaultArcDegrees)
        {
            var definition = CreateInstance<ApproachAbilityDefinition>();
            definition.ConfigureRuntime(abilityTagName);
            definition.rangeRatio = rangeRatio;
            definition.slotCount = slotCount;
            definition.arcDegrees = arcDegrees;
            return definition;
        }
    }
}
