namespace HS.Framework.Ability.Effects
{
    /// <summary>효과의 적용 거부, 즉시 실행, 유지 시작을 구분한다.</summary>
    public enum GameplayEffectApplicationResult
    {
        /// <summary>조건에 막혀 효과 실행을 시작하지 않았다.</summary>
        Rejected = 0,

        /// <summary>즉시 효과를 실행했다. 유지되는 기록은 없다.</summary>
        Executed = 1,

        /// <summary>효과가 유지되고 있다. 진행 조건에 따라 억제된 상태일 수 있다.</summary>
        Applied = 2,

        /// <summary>실행 중 콜백이 효과나 실행 요청을 취소했다. 이미 실행된 즉시 변화는 되돌리지 않는다.</summary>
        Cancelled = 3
    }
}
