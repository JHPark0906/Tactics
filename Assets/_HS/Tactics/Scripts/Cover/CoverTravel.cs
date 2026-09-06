using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 두 점 사이를 실제로 걸어갔을 때의 거리를 잰다.
    /// </summary>
    /// <param name="from">출발 월드 좌표이다.</param>
    /// <param name="to">도착 월드 좌표이다.</param>
    /// <param name="distance">잰 이동 거리이며, 갈 수 없으면 정해지지 않는다.</param>
    /// <returns>끝까지 갈 수 있으면 true이다.</returns>
    /// <remarks>
    /// <b>갈 수 없는 것을 거리로 표현하지 않는다.</b> 아주 먼 값으로 돌려주면 다른 후보가 모두 사라졌을 때
    /// 갈 수 없는 자리가 뽑히고, 유닛은 그리로 가지 못한 채 멈춘다. 그래서 거리와 성패를 갈라 답한다.
    /// </remarks>
    public delegate bool CoverTravelDistance(Vector3 from, Vector3 to, out float distance);
}
