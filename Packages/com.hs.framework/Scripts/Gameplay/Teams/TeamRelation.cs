namespace HS.Framework.Gameplay.Teams
{
    /// <summary>
    /// 두 진영 사이의 관계를 나타낸다.
    /// </summary>
    public enum TeamRelation
    {
        /// <summary>서로 적대하지도 협력하지도 않는 관계이다.</summary>
        Neutral,

        /// <summary>같은 편으로 취급하는 관계이다.</summary>
        Friendly,

        /// <summary>서로 공격 대상으로 취급하는 관계이다.</summary>
        Hostile
    }

    /// <summary>
    /// 진영 사이의 관계를 판정하는 규칙을 정의한다.
    /// 기본 규칙으로 충분하지 않은 프로젝트는 이 계약을 직접 구현해 중립 진영이나
    /// 동맹 관계 같은 자체 규칙으로 교체한다.
    /// </summary>
    public interface ITeamRelationPolicy
    {
        /// <summary>
        /// 기준 진영이 상대 진영을 어떤 관계로 보는지 판정한다.
        /// </summary>
        TeamRelation GetRelation(TeamId source, TeamId other);
    }

    /// <summary>
    /// 프레임워크가 제공하는 기본 진영 관계 규칙이다.
    /// 어느 한쪽이라도 진영이 지정되지 않았으면 중립, 같은 진영이면 아군,
    /// 서로 다른 진영이면 적대로 판정한다.
    /// </summary>
    public sealed class DefaultTeamRelationPolicy : ITeamRelationPolicy
    {
        /// <summary>
        /// 재사용 가능한 기본 규칙 인스턴스이다. 이 규칙은 상태를 가지지 않는다.
        /// </summary>
        public static ITeamRelationPolicy Instance { get; } = new DefaultTeamRelationPolicy();

        /// <inheritdoc />
        public TeamRelation GetRelation(TeamId source, TeamId other)
        {
            if (!source.IsAssigned || !other.IsAssigned)
            {
                return TeamRelation.Neutral;
            }

            return source == other ? TeamRelation.Friendly : TeamRelation.Hostile;
        }
    }
}
