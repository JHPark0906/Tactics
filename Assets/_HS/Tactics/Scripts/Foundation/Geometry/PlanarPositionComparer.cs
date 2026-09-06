using System.Collections.Generic;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 평면 좌표를 언제나 같은 차례로 늘어놓는 비교자이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// x가 다르면 x로, 같으면 z로 가른다. 프로젝트 전역에서 동점을 가르는 규칙(먼저 x, 그다음 z)과
    /// 같은 것을 <see cref="PlanarPosition"/>에 대해 쓴다.
    /// </para>
    /// <para>
    /// A* 열린 목록의 우선순위가 같은 노드를 가르는 데 쓴다. <see cref="PlanarPosition"/>은
    /// <see cref="System.IComparable"/>을 구현하지 않으므로, 이 비교자 없이는 우선순위가 같은
    /// 순간 예외가 난다.
    /// </para>
    /// </remarks>
    public sealed class PlanarPositionComparer : IComparer<PlanarPosition>
    {
        /// <summary>이 비교자의 공유 인스턴스이다. 상태가 없으므로 하나면 충분하다.</summary>
        public static readonly PlanarPositionComparer Instance = new();

        /// <inheritdoc />
        public int Compare(PlanarPosition left, PlanarPosition right)
        {
            var xComparison = left.X.CompareTo(right.X);
            return xComparison != 0 ? xComparison : left.Z.CompareTo(right.Z);
        }
    }
}
