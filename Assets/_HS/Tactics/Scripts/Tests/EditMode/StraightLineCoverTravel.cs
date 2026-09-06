using HS.Tactics.Cover;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 테스트에서 두 점 사이의 직선거리를 반환하는 이동 거리 대역이다.
    /// 경로 서비스 없이 후보의 반경·사거리·지나침·점유·순서 규칙을 검증할 때 사용한다.
    /// 장애물 때문에 우회하는 길이는 검증하지 않으며, 해당 경우는 CoverSelectionTests가 거리 함수를 주입해 확인한다.
    /// </summary>
    internal static class StraightLineCoverTravel
    {
        /// <summary>두 점 사이 직선거리를 이동 거리로 돌려준다. 언제나 갈 수 있다고 답한다.</summary>
        /// <param name="from">출발 월드 좌표이다.</param>
        /// <param name="to">도착 월드 좌표이다.</param>
        /// <param name="distance">잰 거리이다.</param>
        /// <returns>언제나 true이다.</returns>
        internal static bool TryMeasure(Vector3 from, Vector3 to, out float distance)
        {
            distance = Vector3.Distance(from, to);
            return true;
        }

        /// <summary>엄폐 센서를 붙이고 직선 거리로 재도록 맞춘다.</summary>
        /// <param name="target">센서를 붙일 오브젝트이다.</param>
        /// <returns>붙인 센서이다.</returns>
        internal static CoverSensor AttachSensor(GameObject target)
        {
            var sensor = target.AddComponent<CoverSensor>();
            sensor.SetTravelDistance(TryMeasure);
            return sensor;
        }
    }
}
