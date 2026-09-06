using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Character.Movement
{
    /// <summary>목적지까지의 경로를 동기로 계산해 꺾임점을 채운다.</summary>
    /// <remarks>
    /// 이동기는 경로 계산 방법을 알지 않으며, 응답 프레임에 따른 차이가 없도록 동기 결과만 받는다.
    /// 꺾임점의 첫 항목은 출발점이다. 끝까지 갈 수 없는 부분 경로도 유효한 결과로 허용한다.
    /// </remarks>
    /// <param name="from">출발 월드 좌표이다.</param>
    /// <param name="destination">목적지 월드 좌표이다.</param>
    /// <param name="corners">결과 목록이며 호출 전에 비워져 있다.</param>
    /// <returns>꺾임점을 하나라도 얻으면 true이며, 길이 없으면 false이다.</returns>
    public delegate bool PathPlanner(Vector3 from, Vector3 destination, List<Vector3> corners);
}
