using UnityEngine;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// 어트리뷰트 기본값이 왜, 누구 때문에 바뀌는지를 실어 나른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>값만으로는 부족한 자리가 있다.</b> 체력이 0이 되었을 때 누가 쓰러뜨렸는지, 피해가 들어올 때 누가 쏘았는지는
    /// 값의 변화에 딸린 사실이며, 그것을 알아야 하는 쪽(사망 알림, 피해 가로채기)이 기본값을 바꾸는 쪽과
    /// 다른 층에 있다. 그래서 기본값을 바꾸는 호출이 이 값을 함께 넘기고, 필터와 변화 알림이 그대로 받는다.
    /// </para>
    /// <para>
    /// 원인은 무엇이든 될 수 있다. 효과 기록, 어빌리티, 스냅숏, 컴포넌트 자신이 모두 원인이 되며
    /// 프레임워크는 그 종류를 해석하지 않는다. 받는 쪽이 자기가 아는 원인만 골라 다룬다.
    /// </para>
    /// </remarks>
    public readonly struct AttributeChangeContext
    {
        /// <summary>이 변화를 일으킨 것이며 없으면 null이다. 효과 기록, 어빌리티, 스냅숏 등이 온다.</summary>
        public object Cause { get; }

        /// <summary>이 변화를 일으킨 액터이며 알 수 없으면 null이다. 피해라면 쏜 쪽이다.</summary>
        public GameObject Instigator { get; }

        /// <summary>변화 원인을 생성한다.</summary>
        /// <param name="cause">이 변화를 일으킨 것이며 없으면 null이다.</param>
        /// <param name="instigator">이 변화를 일으킨 액터이며 알 수 없으면 null이다.</param>
        public AttributeChangeContext(object cause, GameObject instigator = null)
        {
            Cause = cause;
            Instigator = instigator;
        }

        /// <summary>원인을 밝히지 않은 변화이다.</summary>
        public static AttributeChangeContext None => default;

        /// <summary>원인이나 액터 가운데 하나라도 밝혀져 있는지 여부이다.</summary>
        public bool HasCause => Cause != null || Instigator != null;

        /// <inheritdoc />
        public override string ToString()
        {
            return HasCause
                ? $"Cause={Cause ?? "없음"}, Instigator={(Instigator != null ? Instigator.name : "없음")}"
                : "None";
        }
    }

    /// <summary>
    /// 어트리뷰트 기본값이 바뀌기 직전에 끼어들어 제안된 값을 고칠 수 있는 것이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>기본값 경로에만 걸린다.</b> 즉시 효과, 주기 실행, 직접 쓰기처럼 되돌아가지 않는 변화가 이 자리를 지난다.
    /// 수정자(버프)는 원인이 사라지면 되돌아가는 변화라 여기 걸리지 않는다. 어트리뷰트 계층이 기본값과 수정자의
    /// 역할을 나눈 것과 같은 선이다.
    /// </para>
    /// <para>
    /// <b>돌려준 값이 새 제안값이다.</b> 현재값을 그대로 돌려주면 이번 변화는 없던 것이 된다.
    /// 보호막이 피해를 대신 받는 것, 엄폐물이 피해를 가로채는 것, 어떤 원인의 변화를 무시하는 것이 모두
    /// 이 한 가지 규칙으로 표현된다. 돌려준 값은 다시 하한과 상한 안으로 넣어진다.
    /// </para>
    /// <para>
    /// 여러 필터가 등록되어 있으면 등록한 순서로 부르며, 앞 필터가 돌려준 값을 뒤 필터가 받는다.
    /// 필터 안에서 같은 집합의 다른 어트리뷰트를 바꾸는 것은 괜찮으나, 같은 어트리뷰트의 기본값을 다시 쓰면
    /// 알림 순서가 어긋나므로 하지 않는다.
    /// </para>
    /// </remarks>
    public interface IAttributeBaseValueFilter
    {
        /// <summary>
        /// 제안된 기본값을 검토해 실제로 적용할 값을 돌려준다.
        /// </summary>
        /// <param name="definition">바뀌려는 어트리뷰트 정의이다.</param>
        /// <param name="currentBaseValue">지금의 기본값이다.</param>
        /// <param name="proposedBaseValue">바꾸려는 기본값이며 이미 하한과 상한 안에 있다.</param>
        /// <param name="context">이 변화의 원인이다.</param>
        /// <returns>실제로 적용할 기본값이다. 현재값을 돌려주면 변화가 없던 것이 된다.</returns>
        float FilterBaseValue(
            AttributeDefinition definition,
            float currentBaseValue,
            float proposedBaseValue,
            in AttributeChangeContext context);
    }
}
