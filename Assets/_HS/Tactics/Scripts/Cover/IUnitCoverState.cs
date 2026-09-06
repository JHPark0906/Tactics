namespace HS.Tactics.Cover
{
    /// <summary>
    /// 유닛이 확보한 엄폐와 현재 엄폐 중인지 여부를 제공한다.
    /// 엄폐 중 여부는 예약 지점에 도착했는지로 정한다. 명중률이나 위협의 방향은 이 계약에서 판정하지 않는다.
    /// 엄폐의 피해 흡수는 회피 어빌리티와 엄폐물의 규칙으로 처리한다.
    /// </summary>
    public interface IUnitCoverState
    {
        /// <summary>
        /// 엄폐 지점을 확보했고 그 자리에 도착해 실제로 엄폐 중인지 여부이다.
        /// 확보만 하고 이동 중이면 false이다.
        /// </summary>
        bool IsInCover { get; }

        /// <summary>
        /// 확보한 엄폐 지점이며 확보한 지점이 없으면 null이다.
        /// 이동 중일 수도 있으므로 엄폐 중인지는 <see cref="IsInCover"/>로 판단한다.
        /// </summary>
        CoverPoint ClaimedCover { get; }

    }
}
