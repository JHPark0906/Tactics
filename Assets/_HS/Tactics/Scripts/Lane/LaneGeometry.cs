using UnityEngine;

namespace HS.Tactics.Lane
{
    /// <summary>
    /// 레인의 시작점과 종점이 이루는 기하 정보이다.
    /// </summary>
    /// <remarks>
    /// MonoBehaviour와 떨어진 값 형식이라 씬 없이도 계산을 검증할 수 있다.
    /// 맵이 일자형이므로 레인은 두 지점으로 충분하며, 전투는 수평면에서 벌어지므로
    /// 길이와 방향은 y를 뺀 수평 성분으로 계산한다. 목표 좌표만 원래 높이를 그대로 남겨
    /// 경사가 있는 지형에서도 배치된 지점을 그대로 가리킨다.
    /// </remarks>
    public readonly struct LaneGeometry
    {
        /// <summary>레인으로 인정하는 최소 수평 길이(미터)이며, 이보다 짧으면 방향을 정할 수 없다.</summary>
        public const float MinimumLength = 0.01f;

        /// <summary>시작점과 종점으로 레인 기하를 생성한다.</summary>
        /// <param name="start">레인의 시작 좌표이다.</param>
        /// <param name="end">레인의 종점 좌표이다.</param>
        public LaneGeometry(Vector3 start, Vector3 end)
        {
            Start = start;
            End = end;
        }

        /// <summary>레인의 시작 좌표이다.</summary>
        public Vector3 Start { get; }

        /// <summary>레인의 종점 좌표이다.</summary>
        public Vector3 End { get; }

        /// <summary>시작점에서 종점까지의 수평 길이(미터)이다.</summary>
        public float Length => GetHorizontalDelta().magnitude;

        /// <summary>방향을 정할 수 있을 만큼 두 지점이 떨어져 있는지 여부이다.</summary>
        public bool IsValid => GetHorizontalDelta().sqrMagnitude >= MinimumLength * MinimumLength;

        /// <summary>
        /// 시작점에서 종점을 향하는 수평 단위 방향이며, 레인이 무효하면 <see cref="Vector3.zero"/>이다.
        /// </summary>
        public Vector3 Forward => IsValid ? GetHorizontalDelta().normalized : Vector3.zero;

        /// <summary>지정한 진행 방향에서 목표로 삼을 레인의 끝점이다.</summary>
        /// <param name="orientation">유닛이 나아가는 방향이다.</param>
        /// <returns>정방향이면 종점, 역방향이면 시작점이다.</returns>
        public Vector3 GetGoal(LaneAdvanceOrientation orientation)
        {
            return orientation == LaneAdvanceOrientation.Reverse ? Start : End;
        }

        /// <summary>지정한 진행 방향의 전진 단위 방향이다.</summary>
        /// <param name="orientation">유닛이 나아가는 방향이다.</param>
        /// <returns>레인 축을 따르는 단위 방향이며 레인이 무효하면 <see cref="Vector3.zero"/>이다.</returns>
        public Vector3 GetDirection(LaneAdvanceOrientation orientation)
        {
            return orientation == LaneAdvanceOrientation.Reverse ? -Forward : Forward;
        }

        /// <summary>시작점에서 종점으로 향하는 수평 변위를 구한다.</summary>
        private Vector3 GetHorizontalDelta()
        {
            var delta = End - Start;
            delta.y = 0f;
            return delta;
        }
    }
}
