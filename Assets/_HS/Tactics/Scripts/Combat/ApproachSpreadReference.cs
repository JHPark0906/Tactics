using HS.Tactics.Foundation.Geometry;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Lane;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 적 주위에 자리를 나눌 때 반원을 그릴 기준 방향을 정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>기준은 모든 유닛이 같은 것을 써야 한다.</b> 유닛마다 다른 기준을 쓰면 자리를 나누는 의미가 사라진다.
    /// 아군 A가 북쪽에서, B가 남쪽에서 같은 적을 잡았다고 하자. 각자 접근해 온 방향을 기준으로 삼으면
    /// A의 기준은 북쪽, B의 기준은 남쪽이다. A가 자기 기준에서 +30° 자리를, B가 자기 기준에서 +150° 자리를
    /// 받으면 <b>세계 좌표에서는 같은 자리다.</b> 각자 자기 반원 안에서는 잘 흩어졌지만
    /// <b>두 반원이 겹치는 것은 아무도 보고 있지 않다.</b>
    /// </para>
    /// <para>
    /// 기준이 하나면 <b>자리 번호가 곧 세계 좌표의 자리</b>가 되어, 번호가 다르면 자리도 다르다는 것이 보장된다.
    /// 자리를 나누는 일이 원래 하려던 것이 바로 이 보장이다.
    /// </para>
    /// <para>
    /// <b>왜 레인 축인가.</b> 일자형 맵에서 전투는 레인 축을 따라 벌어진다. 기준을 레인 축으로 두면
    /// 아군이 적 앞쪽에 부채꼴로 늘어서므로 전술적으로도 자연스럽다. 같은 적을 노리는 유닛은 모두 같은 진영이고
    /// 진영이 같으면 진행 방향도 같으므로, 그것만으로 기준이 하나가 된다.
    /// </para>
    /// <para>
    /// <b>레인이 없을 때도 기준은 하나다.</b> 테스트나 레인을 놓지 않은 씬에서는 고정된 세계 축을 쓴다.
    /// 그 상황에서는 방향에 의미가 없고, 중요한 것은 <b>모든 유닛이 같은 것을 쓴다</b>는 점뿐이다.
    /// </para>
    /// </remarks>
    public static class ApproachSpreadReference
    {
        /// <summary>
        /// 레인을 찾을 수 없을 때 쓰는 고정된 기준 방향이다.
        /// 어떤 방향인지는 중요하지 않으며, 모든 유닛이 같은 값을 얻는다는 점만이 중요하다.
        /// </summary>
        public static readonly PlanarPosition FallbackDirection = new(0f, -1f);

        /// <summary>
        /// 레인 형상과 진행 방향에서 기준 방향을 구한다.
        /// </summary>
        /// <remarks>
        /// 전진 방향은 적을 향하므로, 적에서 우리 쪽을 향하는 기준 방향은 그 반대이다.
        /// </remarks>
        /// <param name="geometry">레인의 형상이다.</param>
        /// <param name="orientation">이 유닛이 레인 위에서 나아가는 방향이다.</param>
        /// <returns>적에서 우리 쪽을 향하는 평면 단위 방향이다.</returns>
        public static PlanarPosition Resolve(LaneGeometry geometry, LaneAdvanceOrientation orientation)
        {
            if (!geometry.IsValid)
            {
                return FallbackDirection;
            }

            var advance = PlanarPosition.FromWorld(geometry.GetDirection(orientation));
            if (advance.SqrMagnitude <= Mathf.Epsilon)
            {
                return FallbackDirection;
            }

            return -advance.Normalized;
        }

        /// <summary>
        /// 씬의 레인과 진영에서 기준 방향을 구한다.
        /// </summary>
        /// <remarks>
        /// 레인이 없거나 아직 갖춰지지 않았거나 진영이 지정되지 않았으면 고정된 방향으로 물러난다.
        /// 어느 경우든 모든 유닛이 같은 값을 얻으므로 자리가 겹치지 않는다는 보장은 유지된다.
        /// </remarks>
        /// <param name="lane">지금 쓰이는 전투 레인이며 없으면 null이다.</param>
        /// <param name="team">기준을 구할 유닛의 진영이다.</param>
        /// <returns>적에서 우리 쪽을 향하는 평면 단위 방향이다.</returns>
        public static PlanarPosition Resolve(BattleLane lane, TeamId team)
        {
            if (lane == null
                || !lane.IsConfigured
                || !LaneAdvanceCalculator.TryGetOrientation(team, lane.ForwardTeam, out var orientation))
            {
                return FallbackDirection;
            }

            return Resolve(lane.Geometry, orientation);
        }
    }
}
