using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 탐지 후보를 거리, X, Z 순서로 정렬한다.
    /// 같은 거리의 후보도 좌표로 차례를 정해 수집 순서가 표적 선택에 영향을 주지 않게 한다.
    /// </summary>
    public static class EnemyCandidateOrdering
    {
        /// <summary>
        /// 후보 목록을 결정적인 차례로 정렬한다.
        /// </summary>
        /// <param name="candidates">정렬할 후보 목록이며 null이면 아무 일도 하지 않는다.</param>
        /// <param name="origin">거리를 잴 기준 좌표이다.</param>
        public static void Sort(List<TeamMember> candidates, Vector3 origin)
        {
            candidates?.Sort((left, right) => Compare(left, right, origin));
        }

        /// <summary>
        /// 두 후보의 차례를 세계 상태만으로 비교한다.
        /// </summary>
        /// <param name="left">앞쪽 후보이다.</param>
        /// <param name="right">뒤쪽 후보이다.</param>
        /// <param name="origin">거리를 잴 기준 좌표이다.</param>
        /// <returns>정렬 비교 결과이다.</returns>
        public static int Compare(TeamMember left, TeamMember right, Vector3 origin)
        {
            var leftPosition = left.transform.position;
            var rightPosition = right.transform.position;
            var distanceComparison = (leftPosition - origin).sqrMagnitude
                .CompareTo((rightPosition - origin).sqrMagnitude);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var xComparison = leftPosition.x.CompareTo(rightPosition.x);
            return xComparison != 0 ? xComparison : leftPosition.z.CompareTo(rightPosition.z);
        }
    }
}
