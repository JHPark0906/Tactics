using System;
using UnityEngine;
using VContainer;

namespace HS.Framework.Gameplay.Teams
{
    /// <summary>
    /// GameObject가 속한 진영을 지정하고 다른 대상과의 관계를 판정하는 공용 컴포넌트이다.
    /// 관계 규칙을 주입하지 않으면 기본 규칙을 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamMember : MonoBehaviour
    {
        [Tooltip("이 대상이 속한 진영이다.")]
        [SerializeField]
        private TeamId teamId;

        private ITeamRelationPolicy _relationPolicy;

        /// <summary>
        /// 이 대상이 속한 진영이다.
        /// </summary>
        public TeamId TeamId => teamId;

        /// <summary>
        /// 관계 판정에 사용하는 규칙이며, 주입 전에는 기본 규칙을 사용한다.
        /// </summary>
        public ITeamRelationPolicy RelationPolicy => _relationPolicy ?? DefaultTeamRelationPolicy.Instance;

        /// <summary>
        /// 진영 관계 판정에 사용할 규칙을 주입받아 기본 규칙을 대체한다.
        /// </summary>
        /// <param name="relationPolicy">진영 관계 판정 규칙이다.</param>
        [Inject]
        public void InjectRelationPolicy(ITeamRelationPolicy relationPolicy)
        {
            _relationPolicy = relationPolicy ?? throw new ArgumentNullException(nameof(relationPolicy));
        }

        /// <summary>
        /// 이 대상이 속한 진영을 변경한다.
        /// </summary>
        public void SetTeam(TeamId value)
        {
            teamId = value;
        }

        /// <summary>
        /// 상대 진영을 어떤 관계로 보는지 판정한다.
        /// </summary>
        public TeamRelation GetRelationTo(TeamId other) => RelationPolicy.GetRelation(teamId, other);

        /// <summary>
        /// 상대 진영을 적대 관계로 보는지 판정한다.
        /// </summary>
        public bool IsHostileTo(TeamId other) => GetRelationTo(other) == TeamRelation.Hostile;

        /// <summary>
        /// 상대 대상을 적대 관계로 보는지 판정한다. 상대가 null이면 false를 반환한다.
        /// </summary>
        public bool IsHostileTo(TeamMember other) => other != null && IsHostileTo(other.TeamId);
    }
}
