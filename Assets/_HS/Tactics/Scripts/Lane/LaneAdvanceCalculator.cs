using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Lane
{
    /// <summary>
    /// 진영과 좌표로부터 전진 방향을 계산하는 순수 로직이다.
    /// </summary>
    /// <remarks>
    /// 방향을 레인 축이 아니라 목표 좌표를 향하도록 계산한다. 일자형 맵에서는 두 방식의 결과가
    /// 사실상 같지만, 목표를 향해 계산해 두면 훗날 맵이 비선형이 되어 목표가 다음 웨이포인트로
    /// 바뀌어도 이 계산식과 이를 쓰는 노드를 그대로 둘 수 있다.
    /// </remarks>
    public static class LaneAdvanceCalculator
    {
        /// <summary>목표에 도달했다고 볼 기본 거리(미터)이다.</summary>
        public const float DefaultArrivalDistance = 1f;

        /// <summary>
        /// 진영이 레인의 어느 쪽으로 나아가는지 판정한다.
        /// 레인의 정방향 진영과 같으면 정방향이고, 그 밖의 진영은 모두 역방향이다.
        /// </summary>
        /// <param name="team">판정할 유닛의 진영이다.</param>
        /// <param name="forwardTeam">레인을 정방향으로 나아가는 진영이다.</param>
        /// <param name="orientation">판정한 진행 방향이며, 판정하지 못했으면 정방향이다.</param>
        /// <returns>진행 방향을 판정했으면 true이고, 어느 한쪽 진영이라도 지정되지 않았으면 false이다.</returns>
        public static bool TryGetOrientation(
            TeamId team,
            TeamId forwardTeam,
            out LaneAdvanceOrientation orientation)
        {
            orientation = LaneAdvanceOrientation.Forward;
            if (!team.IsAssigned || !forwardTeam.IsAssigned)
            {
                return false;
            }

            orientation = team == forwardTeam
                ? LaneAdvanceOrientation.Forward
                : LaneAdvanceOrientation.Reverse;
            return true;
        }

        /// <summary>
        /// 현재 좌표에서 목표를 향하는 수평 단위 방향을 계산한다.
        /// </summary>
        /// <remarks>
        /// 목표에 이미 도달했으면 <see cref="Vector3.zero"/>를 돌려준다. 전진 노드는 방향이 없으면
        /// 실패하므로, 레인 끝에 닿은 유닛이 목표를 지나쳤다가 되돌아오는 진동 없이 전진을 멈춘다.
        /// </remarks>
        /// <param name="position">유닛의 현재 좌표이다.</param>
        /// <param name="target">향해야 할 목표 좌표이다.</param>
        /// <param name="arrivalDistance">도달로 볼 수평 거리(미터)이며 음수는 0으로 본다.</param>
        /// <returns>정규화된 수평 전진 방향이며, 도달했으면 <see cref="Vector3.zero"/>이다.</returns>
        public static Vector3 GetAdvanceDirection(
            Vector3 position,
            Vector3 target,
            float arrivalDistance = DefaultArrivalDistance)
        {
            var toTarget = target - position;
            toTarget.y = 0f;
            var clampedArrivalDistance = Mathf.Max(0f, arrivalDistance);
            return toTarget.sqrMagnitude <= clampedArrivalDistance * clampedArrivalDistance
                ? Vector3.zero
                : toTarget.normalized;
        }
    }
}
