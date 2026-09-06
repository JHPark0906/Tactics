using System;
using UnityEngine;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 수평면 위의 좌표이다. 높이를 아예 갖지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 별도 타입인가.</b> 로직이 높이를 보지 않는다는 규칙은 주석으로 부탁하면 반드시 샌다.
    /// 읽을 값 자체가 없으면 어길 수가 없으므로, 로직이 다루는 위치를 이 타입으로 못 박는다.
    /// 표시 계층은 여기에 자기 높이를 얹어 세계 좌표를 만든다.
    /// </para>
    /// <para>
    /// <b>세계 좌표와의 경계.</b> <see cref="FromWorld"/>가 높이를 <b>버리고</b>,
    /// <see cref="ToWorld"/>가 높이를 <b>받아서</b> 되돌린다. 두 방향 모두 높이를 명시적으로 다루므로
    /// 어디서 높이가 사라지고 어디서 다시 붙는지가 코드에 드러난다.
    /// </para>
    /// <para>
    /// 이 프로젝트의 수평면은 XZ이다. Unity의 2D 물리가 쓰는 XY가 아니므로,
    /// 씬 뷰에서 보이는 배치와 로직이 보는 평면이 같다.
    /// </para>
    /// </remarks>
    public readonly struct PlanarPosition : IEquatable<PlanarPosition>
    {
        /// <summary>원점이다.</summary>
        public static readonly PlanarPosition Zero = new(0f, 0f);

        /// <summary>지정한 좌표로 평면 위치를 만든다.</summary>
        /// <param name="x">가로 좌표이다.</param>
        /// <param name="z">세로 좌표이다.</param>
        public PlanarPosition(float x, float z)
        {
            X = x;
            Z = z;
        }

        /// <summary>가로 좌표이다.</summary>
        public float X { get; }

        /// <summary>세로 좌표이다.</summary>
        public float Z { get; }

        /// <summary>원점에서의 거리의 제곱이며 제곱근을 쓰지 않는 비교에 쓴다.</summary>
        public float SqrMagnitude => X * X + Z * Z;

        /// <summary>원점에서의 거리이다.</summary>
        public float Magnitude => Mathf.Sqrt(SqrMagnitude);

        /// <summary>
        /// 길이가 이 값 이하면 방향을 정할 수 없는 "사실상 0"으로 본다.
        /// </summary>
        /// <remarks>
        /// 이 벡터는 임의의 두 좌표를 뺀 결과일 수 있어, 수학적으로 정확히 0이어야 하는 경우에도
        /// 실제 부동소수점 연산은 잔차를 남길 수 있다.
        /// <see cref="Mathf.Epsilon"/>(표현 가능한 가장 작은 양수, 약 1.4e-45)은 그 잔차보다 수십
        /// 자릿수 작아 걸리지 않는다 — 그러면 잡음에 불과한 방향이 길이 1로 나눠져 그럴듯한 단위
        /// 벡터처럼 보이게 된다. 이 값은 그 잔차보다 확실히 크면서도 이 프로젝트의 실제 크기(미터
        /// 단위)에 비하면 여전히 무의미하게 작다.
        /// </remarks>
        private const float NegligibleMagnitude = 1e-4f;

        /// <summary>
        /// 길이를 1로 맞춘 방향이다. 길이가 <see cref="NegligibleMagnitude"/> 이하면 <see cref="Zero"/>를 돌려준다.
        /// </summary>
        public PlanarPosition Normalized
        {
            get
            {
                var magnitude = Magnitude;
                return magnitude <= NegligibleMagnitude ? Zero : new PlanarPosition(X / magnitude, Z / magnitude);
            }
        }

        /// <summary>
        /// 세계 좌표에서 높이를 버리고 평면 위치를 얻는다.
        /// </summary>
        /// <param name="worldPosition">변환할 세계 좌표이다.</param>
        /// <returns>높이를 버린 평면 위치이다.</returns>
        public static PlanarPosition FromWorld(Vector3 worldPosition)
        {
            return new PlanarPosition(worldPosition.x, worldPosition.z);
        }

        /// <summary>
        /// 높이를 붙여 세계 좌표로 되돌린다. 높이는 표시 계층이 정한다.
        /// </summary>
        /// <param name="height">붙일 높이이다.</param>
        /// <returns>세계 좌표이다.</returns>
        public Vector3 ToWorld(float height = 0f)
        {
            return new Vector3(X, height, Z);
        }

        /// <summary>두 평면 위치 사이의 거리의 제곱이다.</summary>
        /// <param name="from">시작 위치이다.</param>
        /// <param name="to">끝 위치이다.</param>
        /// <returns>거리의 제곱이다.</returns>
        public static float SqrDistance(PlanarPosition from, PlanarPosition to)
        {
            return (to - from).SqrMagnitude;
        }

        /// <summary>두 평면 위치 사이의 거리이다.</summary>
        /// <param name="from">시작 위치이다.</param>
        /// <param name="to">끝 위치이다.</param>
        /// <returns>거리이다.</returns>
        public static float Distance(PlanarPosition from, PlanarPosition to)
        {
            return (to - from).Magnitude;
        }

        /// <summary>두 평면 벡터의 내적이다.</summary>
        /// <param name="left">왼쪽 벡터이다.</param>
        /// <param name="right">오른쪽 벡터이다.</param>
        /// <returns>내적 값이다.</returns>
        public static float Dot(PlanarPosition left, PlanarPosition right)
        {
            return left.X * right.X + left.Z * right.Z;
        }

        /// <summary>
        /// 이 벡터를 시계 방향으로 90도 돌린 벡터이다.
        /// 정면에서 오른쪽 축을 얻을 때 쓰며, 회전 방향을 한곳에 고정해 두어야 결과가 흔들리지 않는다.
        /// </summary>
        /// <returns>수직인 벡터이다.</returns>
        public PlanarPosition PerpendicularClockwise()
        {
            return new PlanarPosition(Z, -X);
        }

        /// <summary>두 위치를 더한다.</summary>
        public static PlanarPosition operator +(PlanarPosition left, PlanarPosition right)
        {
            return new PlanarPosition(left.X + right.X, left.Z + right.Z);
        }

        /// <summary>두 위치를 뺀다.</summary>
        public static PlanarPosition operator -(PlanarPosition left, PlanarPosition right)
        {
            return new PlanarPosition(left.X - right.X, left.Z - right.Z);
        }

        /// <summary>방향을 뒤집는다.</summary>
        public static PlanarPosition operator -(PlanarPosition value)
        {
            return new PlanarPosition(-value.X, -value.Z);
        }

        /// <summary>배율을 곱한다.</summary>
        public static PlanarPosition operator *(PlanarPosition value, float scale)
        {
            return new PlanarPosition(value.X * scale, value.Z * scale);
        }

        /// <summary>배율을 곱한다.</summary>
        public static PlanarPosition operator *(float scale, PlanarPosition value)
        {
            return value * scale;
        }

        /// <inheritdoc />
        public bool Equals(PlanarPosition other)
        {
            return X.Equals(other.X) && Z.Equals(other.Z);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PlanarPosition other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => X.GetHashCode() ^ (Z.GetHashCode() << 2);

        /// <inheritdoc />
        public override string ToString() => $"({X}, {Z})";
    }
}
