using System.Collections.Generic;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 후보 중에서 교전 대상을 고르는 순수 판정 로직이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 후보를 어떻게 모으는지(물리 질의, 등록 목록 등)와 분리해 두었으므로
    /// 물리 없이도 선정 규칙만 따로 검증할 수 있다. 실제 수집은 <see cref="EnemyDetector"/>가 맡는다.
    /// </para>
    /// <para>
    /// <b>표적은 반경만 본다.</b> 각도도 가림도 보지 않는다. 엄폐는 보이지 않게 하는 것이 아니라 대신 맞아 주는 것이므로,
    /// 반경 안의 적은 벽 뒤에 있든 등 뒤에 있든 엄폐 중이든 표적이 된다. 시야 판정을 여기에 들이면
    /// 엄폐 뒤의 적을 겨누지 못하게 되어 그 규칙과 어긋난다.
    /// </para>
    /// </remarks>
    public static class EnemyTargetSelector
    {
        /// <summary>
        /// 적대 관계인 후보 중 반경 안에서 가장 가까운 대상을 고른다.
        /// </summary>
        /// <param name="self">대상을 찾는 유닛의 진영 구성요소이며, 거리는 이 유닛의 위치에서 잰다.</param>
        /// <param name="candidates">검사할 후보 목록이며 null 항목과 자기 자신은 건너뛴다.</param>
        /// <param name="maxDistance">대상으로 삼을 최대 거리(미터)이다.</param>
        /// <returns>선정한 대상의 진영 구성요소이며, 조건에 맞는 후보가 없으면 null이다.</returns>
        public static TeamMember SelectNearestHostileInRange(
            TeamMember self,
            IReadOnlyList<TeamMember> candidates,
            float maxDistance)
        {
            if (self == null || candidates == null)
            {
                return null;
            }

            TeamMember nearest = null;
            var nearestSquaredDistance = float.MaxValue;
            var range = Mathf.Max(0f, maxDistance);
            var maxSquaredDistance = range * range;
            var origin = self.transform.position;

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!IsHostileCandidate(self, candidate))
                {
                    continue;
                }

                var squaredDistance = (candidate.transform.position - origin).sqrMagnitude;
                if (squaredDistance > maxSquaredDistance || squaredDistance >= nearestSquaredDistance)
                {
                    continue;
                }

                nearestSquaredDistance = squaredDistance;
                nearest = candidate;
            }

            return nearest;
        }

        /// <summary>
        /// 후보가 대상이 될 수 있는 적대 진영인지 판정한다.
        /// 자기 자신과 진영이 지정되지 않은 대상은 제외한다.
        /// </summary>
        /// <param name="self">대상을 찾는 유닛의 진영 구성요소이다.</param>
        /// <param name="candidate">검사할 후보이다.</param>
        /// <returns>적대 관계로 교전 대상이 될 수 있으면 true이다.</returns>
        public static bool IsHostileCandidate(TeamMember self, TeamMember candidate)
        {
            if (self == null || candidate == null || candidate == self)
            {
                return false;
            }

            // 같은 GameObject에 붙은 다른 진영 구성요소를 자기 자신으로 오인하지 않도록 루트도 비교한다.
            if (candidate.transform == self.transform)
            {
                return false;
            }

            // 죽은 유닛은 대상에서 뺀다. 시신이 씬에 남아 있어도 계속 겨누거나 쫓지 않게 하기 위함이다.
            // 체력 구성요소가 없는 대상은 죽을 수 없는 것으로 보고 통과시킨다.
            var damageable = candidate.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsDead)
            {
                return false;
            }

            return self.IsHostileTo(candidate);
        }
    }
}
