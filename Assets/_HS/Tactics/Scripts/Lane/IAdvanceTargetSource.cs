using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Lane
{
    /// <summary>
    /// 유닛이 전진할 목표 좌표를 공급하는 계약이다.
    /// </summary>
    /// <remarks>
    /// 행동 트리의 전진 노드는 "목표로 이동한다"만 알고, 그 목표가 어디인지는 이 계약의 구현이 정한다.
    /// 지금은 일자형 맵이라 <see cref="BattleLane"/>이 레인의 끝점을 목표로 준다. 맵이 비선형이 되면
    /// 웨이포인트를 따라가는 구현으로 이 계약만 갈아 끼우면 되고, 노드와 행동 트리, 유닛 조립 코드는
    /// 손대지 않는다. 그것이 이 계약을 따로 둔 이유이다.
    /// </remarks>
    public interface IAdvanceTargetSource
    {
        /// <summary>
        /// 지정한 진영의 유닛이 지금 향해야 할 목표 좌표를 구한다.
        /// </summary>
        /// <param name="team">목표를 구할 유닛의 진영이다.</param>
        /// <param name="position">
        /// 유닛의 현재 좌표이다. 일자형 레인은 쓰지 않지만, 경로가 여러 구간으로 나뉘는 구현은
        /// 이 좌표로 다음 목표를 고른다.
        /// </param>
        /// <param name="target">구한 목표 좌표이며, 구하지 못했으면 <see cref="Vector3.zero"/>이다.</param>
        /// <returns>목표를 구했으면 true이고, 진영이나 경로 설정이 갖춰지지 않았으면 false이다.</returns>
        bool TryGetAdvanceTarget(TeamId team, Vector3 position, out Vector3 target);
    }
}
