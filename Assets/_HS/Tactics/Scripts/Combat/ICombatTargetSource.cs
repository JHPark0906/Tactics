using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 유닛이 지금 겨누고 있는 대상을 알려 주는 계약이다.
    /// </summary>
    /// <remarks>
    /// 대상을 어떻게 고르는지는 이 계약이 관여하지 않는다. 반경 안에서 찾는 <see cref="EnemyDetector"/>가 기본 구현이고,
    /// 대상을 다른 방식으로 정하는 프로젝트나 검증 코드는 같은 계약을 구현해 갈아 끼우면 된다.
    /// 사격 어빌리티는 이 계약만 알고 있으므로 대상 선정 방식이 바뀌어도 사격 쪽을 고치지 않는다.
    /// </remarks>
    public interface ICombatTargetSource
    {
        /// <summary>지금 겨누고 있는 대상이며, 없으면 null이다.</summary>
        Transform CurrentTarget { get; }
    }
}
