using UnityEngine;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 수평면 위의 원이다. 유닛의 자리 차지를 나타낸다.
    /// </summary>
    public readonly struct PlanarCircle
    {
        /// <summary>지정한 중심과 반지름으로 원을 만든다.</summary>
        /// <param name="center">원의 중심이다.</param>
        /// <param name="radius">원의 반지름이며 음수는 0으로 본다.</param>
        public PlanarCircle(PlanarPosition center, float radius)
        {
            Center = center;
            Radius = Mathf.Max(0f, radius);
        }

        /// <summary>원의 중심이다.</summary>
        public PlanarPosition Center { get; }

        /// <summary>원의 반지름이며 항상 0 이상이다.</summary>
        public float Radius { get; }

        /// <inheritdoc />
        public override string ToString() => $"Circle({Center}, r={Radius})";
    }

    /// <summary>
    /// 수평면 위의 회전한 직사각형이다. 엄폐물을 나타낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 회전을 각도가 아니라 <b>정면 벡터</b>로 들고 있다. 엄폐물의 회전이 곧 방어 방향이고
    /// 그 방향이 오브젝트의 정면에서 나오므로, 같은 값을 두 형태로 들고 있다가 어긋나는 일을 피한다.
    /// </para>
    /// <para>
    /// 오른쪽 축은 정면을 시계 방향으로 90도 돌려 얻는다. 이 회전 방향을 한곳에 고정해 두어야
    /// 국소 좌표의 부호가 흔들리지 않는다.
    /// </para>
    /// </remarks>
    public readonly struct PlanarRectangle
    {
        /// <summary>지정한 중심과 반크기, 정면으로 직사각형을 만든다.</summary>
        /// <param name="center">직사각형의 중심이다.</param>
        /// <param name="halfExtents">중심에서 각 변까지의 거리이며 음수는 0으로 본다.</param>
        /// <param name="forward">정면 방향이며 길이가 0이면 +Z를 쓴다.</param>
        public PlanarRectangle(PlanarPosition center, PlanarPosition halfExtents, PlanarPosition forward)
        {
            Center = center;
            HalfExtents = new PlanarPosition(Mathf.Max(0f, halfExtents.X), Mathf.Max(0f, halfExtents.Z));
            // Mathf.Epsilon으로 충분하다 — PlanarPosition.Normalized는 임의의 벡터를 반환하지 않고
            // 항상 정확히 PlanarPosition.Zero(길이 0)이거나 길이가 1에 가까운 단위 벡터만 반환하므로,
            // 여기서 SqrMagnitude는 정확히 0이거나 약 1이지 그 사이의 잔차가 나올 수 없다.
            var normalized = forward.Normalized;
            Forward = normalized.SqrMagnitude <= Mathf.Epsilon ? new PlanarPosition(0f, 1f) : normalized;
        }

        /// <summary>직사각형의 중심이다.</summary>
        public PlanarPosition Center { get; }

        /// <summary>중심에서 각 변까지의 거리이며 X는 좌우, Z는 앞뒤이다.</summary>
        public PlanarPosition HalfExtents { get; }

        /// <summary>정면 방향이며 길이가 1이다.</summary>
        public PlanarPosition Forward { get; }

        /// <summary>정면을 시계 방향으로 90도 돌린 오른쪽 축이다.</summary>
        public PlanarPosition Right => Forward.PerpendicularClockwise();

        /// <summary>
        /// 세계 좌표를 이 직사각형의 국소 좌표로 옮긴다.
        /// 국소 좌표에서는 축에 정렬된 상자가 되므로 판정이 단순해진다.
        /// </summary>
        /// <param name="worldPoint">옮길 평면 좌표이다.</param>
        /// <returns>국소 좌표이며 X는 오른쪽 축, Z는 정면 축 성분이다.</returns>
        public PlanarPosition ToLocal(PlanarPosition worldPoint)
        {
            var delta = worldPoint - Center;
            return new PlanarPosition(
                PlanarPosition.Dot(delta, Right),
                PlanarPosition.Dot(delta, Forward));
        }

        /// <summary>국소 좌표를 세계 좌표로 되돌린다.</summary>
        /// <param name="localPoint">되돌릴 국소 좌표이다.</param>
        /// <returns>평면 세계 좌표이다.</returns>
        public PlanarPosition ToWorld(PlanarPosition localPoint)
        {
            return Center + Right * localPoint.X + Forward * localPoint.Z;
        }

        /// <inheritdoc />
        public override string ToString() => $"Rect({Center}, half={HalfExtents}, fwd={Forward})";
    }
}
