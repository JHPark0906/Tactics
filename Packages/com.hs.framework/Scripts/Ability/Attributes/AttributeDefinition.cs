using UnityEngine;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// 능력치 하나가 무엇인지 데이터로 정의한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>어떤 능력치가 존재하는지는 게임이 정한다.</b> 프레임워크는 값을 담고 계산하는 그릇만 제공하며,
    /// 체력이나 공격력 같은 이름을 알지 못한다. 게임은 필요한 만큼 이 에셋을 만들어 쓰고,
    /// 코드는 그 에셋을 참조해 값을 읽는다. 그래서 다른 게임에 그대로 가져가도 정의만 새로 만들면 된다.
    /// </para>
    /// <para>
    /// <b>상한을 다른 어트리뷰트로 둘 수 있다.</b> 체력의 상한은 최대 체력이며, 최대 체력이 버프로 오르면
    /// 체력의 상한도 함께 올라야 한다. 상한을 고정 숫자로만 두면 이런 관계를 표현할 수 없으므로
    /// <see cref="CapAttribute"/>에 다른 정의를 연결할 수 있게 한다.
    /// 연결하면 <see cref="MaxValue"/>는 쓰이지 않는다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "AttributeDefinition", menuName = "HS Framework/Ability/Attribute Definition")]
    public sealed class AttributeDefinition : ScriptableObject
    {
        [Tooltip("저장과 조회에 사용할 고유 식별자이다. 비워 두면 에셋 이름을 사용한다.")]
        [SerializeField]
        private string id = string.Empty;

        [Tooltip("UI와 디버그에 표시할 이름이다. 비워 두면 식별자를 사용한다.")]
        [SerializeField]
        private string displayName = string.Empty;

        [Tooltip("수정자가 없을 때의 기본값이다.")]
        [SerializeField]
        private float defaultBaseValue;

        [Tooltip("값이 내려갈 수 있는 하한이다.")]
        [SerializeField]
        private float minValue;

        [Tooltip("상한을 사용할지 여부이다. 상한 어트리뷰트를 연결하면 이 설정은 쓰이지 않는다.")]
        [SerializeField]
        private bool hasMaxValue;

        [Tooltip("상한 어트리뷰트를 연결하지 않았을 때 사용할 고정 상한이다.")]
        [SerializeField]
        private float maxValue = 1f;

        [Tooltip("이 어트리뷰트의 상한 역할을 하는 다른 어트리뷰트이다. 비워 두면 고정 상한 설정을 따른다.")]
        [SerializeField]
        private AttributeDefinition capAttribute;

        [Tooltip("상한이 바뀌었을 때 기본값을 어떻게 다룰지 정한다.")]
        [SerializeField]
        private AttributeCapPolicy capPolicy = AttributeCapPolicy.ClampBaseValue;

        /// <summary>저장과 조회에 사용할 고유 식별자이며, 지정하지 않았으면 에셋 이름을 쓴다.</summary>
        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        /// <summary>UI와 디버그에 표시할 이름이며, 지정하지 않았으면 식별자를 쓴다.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;

        /// <summary>수정자가 없을 때의 기본값이다.</summary>
        public float DefaultBaseValue => defaultBaseValue;

        /// <summary>값이 내려갈 수 있는 하한이다.</summary>
        public float MinValue => minValue;

        /// <summary>
        /// 상한 어트리뷰트를 연결하지 않았을 때 고정 상한을 사용하는지 여부이다.
        /// 상한 어트리뷰트가 있으면 항상 false로 취급한다.
        /// </summary>
        public bool HasMaxValue => capAttribute == null && hasMaxValue;

        /// <summary>고정 상한 값이며 <see cref="HasMaxValue"/>가 참일 때만 의미가 있다.</summary>
        public float MaxValue => maxValue;

        /// <summary>이 어트리뷰트의 상한 역할을 하는 어트리뷰트이며, 없으면 null이다.</summary>
        public AttributeDefinition CapAttribute => capAttribute;

        /// <summary>상한이 바뀌었을 때 기본값을 다루는 방식이다.</summary>
        public AttributeCapPolicy CapPolicy => capPolicy;

        /// <summary>
        /// 에셋을 만들지 않고 코드에서 어트리뷰트 정의를 생성한다.
        /// 테스트와 에디터 도구가 사용하며, 게임 실행 중에는 에셋 정의를 사용한다.
        /// </summary>
        /// <param name="id">고유 식별자이다.</param>
        /// <param name="defaultBaseValue">수정자가 없을 때의 기본값이다.</param>
        /// <param name="minValue">값의 하한이다.</param>
        /// <param name="capAttribute">상한 역할을 할 어트리뷰트이며 없으면 null이다.</param>
        /// <param name="capPolicy">상한이 바뀌었을 때 기본값을 다루는 방식이다.</param>
        /// <returns>생성한 어트리뷰트 정의이다.</returns>
        public static AttributeDefinition CreateRuntime(
            string id,
            float defaultBaseValue = 0f,
            float minValue = 0f,
            AttributeDefinition capAttribute = null,
            AttributeCapPolicy capPolicy = AttributeCapPolicy.ClampBaseValue)
        {
            var definition = CreateInstance<AttributeDefinition>();
            definition.name = id;
            definition.id = id;
            definition.defaultBaseValue = defaultBaseValue;
            definition.minValue = minValue;
            definition.capAttribute = capAttribute;
            definition.capPolicy = capPolicy;
            return definition;
        }

        /// <summary>
        /// 에셋을 만들지 않고 고정 상한을 가진 어트리뷰트 정의를 생성한다.
        /// </summary>
        /// <param name="id">고유 식별자이다.</param>
        /// <param name="defaultBaseValue">수정자가 없을 때의 기본값이다.</param>
        /// <param name="minValue">값의 하한이다.</param>
        /// <param name="maxValue">값의 고정 상한이다.</param>
        /// <returns>생성한 어트리뷰트 정의이다.</returns>
        public static AttributeDefinition CreateRuntimeWithMaxValue(
            string id,
            float defaultBaseValue,
            float minValue,
            float maxValue)
        {
            var definition = CreateRuntime(id, defaultBaseValue, minValue);
            definition.hasMaxValue = true;
            definition.maxValue = maxValue;
            return definition;
        }

        /// <inheritdoc />
        public override string ToString() => $"Attribute({Id})";
    }
}
