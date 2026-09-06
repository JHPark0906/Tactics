using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;

namespace HS.Tactics.Character.Movement
{
    /// <summary>
    /// 지정한 원과 겹칠 만한 다른 유닛의 원을 모은다.
    /// </summary>
    /// <remarks>
    /// <see cref="PathPlanner"/>와 같은 이유로 델리게이트다 —
    /// <see cref="PlanarCharacterMover"/>는 다른 유닛을 어디서 찾는지 모른다. 실제 구현은 이 값을
    /// 스텝마다 다시 계산해 내는 것을 기대한다 — 다른 유닛도 매 스텝 움직이기 때문이다.
    /// </remarks>
    /// <param name="sweep">이번 스텝이 훑는 자리를 넉넉히 담는 원이다.</param>
    /// <param name="results">결과를 담을 목록이다. <see cref="HS.Tactics.Units.IUnitSpatialRegistry.CollectOverlapping"/>과
    /// 같은 관례로, 구현이 먼저 비운 뒤 채운다.</param>
    public delegate void NearbyUnitsQuery(PlanarCircle sweep, List<PlanarCircle> results);
}
