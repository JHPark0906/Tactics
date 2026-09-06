namespace HS.Tactics.Lane
{
    /// <summary>
    /// 유닛이 레인 위에서 나아가는 방향이다.
    /// 진영마다 목표가 반대편이므로 레인 하나를 두 진영이 함께 쓴다.
    /// </summary>
    public enum LaneAdvanceOrientation
    {
        /// <summary>레인의 시작점에서 종점을 향해 나아간다.</summary>
        Forward = 0,

        /// <summary>레인의 종점에서 시작점을 향해 나아간다.</summary>
        Reverse = 1
    }
}
