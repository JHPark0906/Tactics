using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 주변 엄폐 지점 중 지금 쓸 수 있는 곳을 찾아 주는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// 선택 규칙 자체는 <see cref="CoverSelection"/>에 순수 로직으로 있고 이 컴포넌트는 후보를 모아
    /// 넘기는 역할만 한다. 엄폐 지점은 레벨 디자이너가 배치한 뒤 움직이지 않으므로 후보 목록을
    /// 한 번 모아 두고 재사용하며, 런타임에 지점이 추가되거나 사라지면
    /// <see cref="RefreshCoverPoints"/>로 다시 모은다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CoverSensor : MonoBehaviour
    {
        [Tooltip("엄폐 지점을 찾는 최대 거리(미터)이다.")]
        [SerializeField]
        [Min(0f)]
        private float searchRadius = 12f;

        private readonly List<CoverPoint> _coverPoints = new();
        private CoverTravelDistance _measureTravelDistance = NoPathfindingService;
        private readonly List<CoverCandidate> _candidates = new();
        private bool _isSourceInitialized;

        /// <summary>같은 자리에서 이미 재 본 후보의 결과이며, 후보 좌표로 찾는다.</summary>
        private readonly Dictionary<Vector3, TravelMeasurement> _travelMemo = new();

        /// <summary>지금 담긴 기억이 어느 자리에서 잰 것인지이다.</summary>
        private Vector3 _travelMemoOrigin;

        /// <summary>기억이 한 번이라도 채워졌는지 여부이다.</summary>
        private bool _hasTravelMemo;

        /// <summary>
        /// 기억을 거쳐 거리를 재는 수단이며 한 번만 만들어 돌려 쓴다.
        /// </summary>
        /// <remarks>
        /// 부를 때마다 만들면 <b>매 틱 대리자 하나씩 쓰레기가 쌓인다.</b> 엄폐 고르기는 유닛마다 매 틱 일어난다.
        /// </remarks>
        private CoverTravelDistance _memoizedTravelDistance;

        /// <summary>엄폐 지점을 찾는 최대 거리(미터)이다.</summary>
        public float SearchRadius => Mathf.Max(0f, searchRadius);

        /// <summary>
        /// 후보 목록을 언제나 같은 순서로 늘어놓는다.
        /// </summary>
        /// <remarks>
        /// 씬에서 엄폐 지점을 찾는 질의는 정렬을 보장하지 않고, 넘겨받은 목록의 순서도 호출부마다 다를 수 있다.
        /// 그 순서를 그대로 쓰면 점수가 같은 엄폐가 둘 이상일 때 <b>실행할 때마다 다른 자리를 고른다.</b>
        /// 좌표만으로 순서를 정해 두면 실행 환경이 달라도 같은 선택이 나온다.
        /// </remarks>
        private void SortCoverPointsDeterministically()
        {
            _coverPoints.Sort(CompareCoverPoints);
        }

        /// <summary>두 엄폐 지점의 순서를 좌표만으로 비교한다.</summary>
        /// <param name="left">앞쪽 지점이다.</param>
        /// <param name="right">뒤쪽 지점이다.</param>
        /// <returns>정렬 비교 결과이다.</returns>
        private static int CompareCoverPoints(CoverPoint left, CoverPoint right)
        {
            var leftPosition = left.Position;
            var rightPosition = right.Position;
            var xComparison = leftPosition.x.CompareTo(rightPosition.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            var zComparison = leftPosition.z.CompareTo(rightPosition.z);
            return zComparison != 0 ? zComparison : leftPosition.y.CompareTo(rightPosition.y);
        }

        /// <summary>후보로 삼고 있는 엄폐 지점 목록이다.</summary>
        public IReadOnlyList<CoverPoint> CoverPoints => _coverPoints;

        /// <summary>
        /// 씬에 배치된 엄폐 지점을 모두 모아 후보 목록으로 삼는다.
        /// </summary>
        public void RefreshCoverPoints()
        {
            SetCoverPoints(FindObjectsByType<CoverPoint>(FindObjectsSortMode.None));
        }

        /// <summary>
        /// 엄폐 후보까지의 이동 거리를 재는 수단을 지정한다.
        /// 후보 선택과 경로 계산을 분리하며, null이면 모든 후보를 도달할 수 없는 것으로 본다.
        /// internal 진입점으로 게임 조립과 테스트에서 측정 방법을 지정한다.
        /// </summary>
        internal void SetTravelDistance(CoverTravelDistance measureTravelDistance)
        {
            _measureTravelDistance = measureTravelDistance ?? NoPathfindingService;
            ForgetMeasurements();
        }

        /// <summary>
        /// 경로 측정 수단이 없을 때 사용하는 대체 함수이다.
        /// 거리를 0으로 두고 항상 도달할 수 없는 것으로 답한다.
        /// </summary>
        private static bool NoPathfindingService(Vector3 from, Vector3 to, out float distance)
        {
            distance = 0f;
            return false;
        }

        /// <summary>
        /// 같은 출발점과 후보에 대한 이동 거리 결과를 재사용한다.
        /// 측정 수단·후보 목록·출발점·후보 위치가 바뀌면 다시 잰다.
        /// 경로 그래프 변경은 감지하지 않으므로, 유닛과 후보가 정지해 있으면 변경 전 거리나 도달 실패가 유지될 수 있다.
        /// </summary>
        private bool MeasureThroughMemo(Vector3 from, Vector3 to, out float distance)
        {
            if (!_hasTravelMemo || !_travelMemoOrigin.Equals(from))
            {
                _travelMemo.Clear();
                _travelMemoOrigin = from;
                _hasTravelMemo = true;
            }
            else if (_travelMemo.TryGetValue(to, out var remembered))
            {
                distance = remembered.Distance;
                return remembered.IsReachable;
            }

            var isReachable = _measureTravelDistance(from, to, out distance);
            _travelMemo[to] = new TravelMeasurement(isReachable, distance);
            return isReachable;
        }

        /// <summary>기억한 측정 결과를 모두 버린다.</summary>
        private void ForgetMeasurements()
        {
            _travelMemo.Clear();
            _hasTravelMemo = false;
        }

        /// <summary>한 후보를 한 자리에서 재 본 결과이다.</summary>
        private readonly struct TravelMeasurement
        {
            /// <summary>측정 결과를 담는다.</summary>
            /// <param name="isReachable">끝까지 갈 수 있으면 true이다.</param>
            /// <param name="distance">잰 이동 거리이다.</param>
            internal TravelMeasurement(bool isReachable, float distance)
            {
                IsReachable = isReachable;
                Distance = distance;
            }

            /// <summary>끝까지 갈 수 있는지 여부이다.</summary>
            internal bool IsReachable { get; }

            /// <summary>잰 이동 거리이며, 갈 수 없으면 뜻이 없다.</summary>
            internal float Distance { get; }
        }

        /// <summary>
        /// 후보로 쓸 엄폐 지점을 직접 지정한다.
        /// 스폰하는 쪽이 배치한 지점을 알고 있거나 테스트에서 후보를 고정할 때 사용한다.
        /// </summary>
        /// <param name="coverPoints">후보로 삼을 엄폐 지점이며 null 항목은 무시한다.</param>
        public void SetCoverPoints(IEnumerable<CoverPoint> coverPoints)
        {
            _coverPoints.Clear();
            if (coverPoints != null)
            {
                foreach (var coverPoint in coverPoints)
                {
                    if (coverPoint != null)
                    {
                        _coverPoints.Add(coverPoint);
                    }
                }
            }

            SortCoverPointsDeterministically();
            _isSourceInitialized = true;
            ForgetMeasurements();
        }

        /// <summary>
        /// 지정한 위협에 대해 쓸 수 있는 엄폐 지점을 찾는다.
        /// </summary>
        /// <param name="threatPosition">위협의 월드 좌표이다.</param>
        /// <param name="coverPoint">찾은 엄폐 지점이며 없으면 null이다.</param>
        /// <param name="maxThreatDistance">
        /// 엄폐 지점에서 위협까지 허용하는 최대 거리이며, 기본값은 제한 없음이다.
        /// 사거리를 넘기면 그 자리에서 실제로 쏠 수 있는 엄폐만 찾는다.
        /// </param>
        /// <returns>쓸 수 있는 엄폐 지점을 찾았으면 true이다.</returns>
        public bool TryFindCover(
            Vector3 threatPosition,
            out CoverPoint coverPoint,
            float maxThreatDistance = float.PositiveInfinity)
        {
            if (!_isSourceInitialized)
            {
                RefreshCoverPoints();
            }

            _candidates.Clear();
            for (var i = _coverPoints.Count - 1; i >= 0; i--)
            {
                if (_coverPoints[i] == null)
                {
                    _coverPoints.RemoveAt(i);
                }
            }

            foreach (var candidate in _coverPoints)
            {
                _candidates.Add(candidate.ToCandidate());
            }

            var index = CoverSelection.SelectBestIndex(
                _candidates,
                transform.position,
                threatPosition,
                SearchRadius,
                maxThreatDistance,
                _memoizedTravelDistance ??= MeasureThroughMemo);
            coverPoint = index == CoverSelection.NoCoverIndex ? null : _coverPoints[index];
            return coverPoint != null;
        }
    }
}
