using System;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 유닛을 놓을 수 있는 직육면체 구역을 월드 좌표로 표현하는 값이다.
    /// </summary>
    /// <remarks>
    /// 맵이 일자형이라 구역은 축에 정렬된 상자로 충분하며, 회전은 다루지 않는다.
    /// MonoBehaviour와 분리된 순수 값이라 EditMode 테스트에서 씬 없이 검증할 수 있다.
    /// </remarks>
    public readonly struct PlacementArea : IEquatable<PlacementArea>
    {
        /// <summary>지정한 중심과 크기로 배치 구역을 생성한다. 크기는 음수를 넣어도 절댓값으로 보정한다.</summary>
        /// <param name="center">구역의 월드 중심이다.</param>
        /// <param name="size">구역의 각 축 길이이다.</param>
        public PlacementArea(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        /// <summary>구역이 지정되지 않은 상태를 나타내는 빈 구역이다.</summary>
        public static PlacementArea None => default;

        /// <summary>구역의 월드 중심이다.</summary>
        public Vector3 Center { get; }

        /// <summary>구역의 각 축 길이이며 항상 0 이상이다.</summary>
        public Vector3 Size { get; }

        /// <summary>중심에서 각 면까지의 거리이다.</summary>
        public Vector3 Extents => Size * 0.5f;

        /// <summary>구역을 Unity의 경계 상자로 표현한 값이다.</summary>
        public Bounds Bounds => new(Center, Size);

        /// <summary>바닥 면적이 없어 유닛을 놓을 수 없는 구역인지 여부이다.</summary>
        public bool IsEmpty => Size.x <= 0f || Size.z <= 0f;

        /// <summary>지정한 월드 좌표가 구역 안(높이 포함)에 있는지 확인한다.</summary>
        /// <param name="worldPosition">확인할 월드 좌표이다.</param>
        /// <returns>구역 안이면 true이다.</returns>
        public bool Contains(Vector3 worldPosition)
        {
            return !IsEmpty && Bounds.Contains(worldPosition);
        }

        /// <summary>
        /// 지정한 월드 좌표가 높이를 무시하고 구역의 바닥 면적 안에 있는지 확인한다.
        /// 지면을 클릭해 얻은 좌표는 높이가 조금씩 어긋나므로 배치 판정에는 이쪽을 쓴다.
        /// </summary>
        /// <param name="worldPosition">확인할 월드 좌표이다.</param>
        /// <returns>바닥 면적 안이면 true이다.</returns>
        public bool ContainsIgnoringHeight(Vector3 worldPosition)
        {
            if (IsEmpty)
            {
                return false;
            }

            var extents = Extents;
            return Mathf.Abs(worldPosition.x - Center.x) <= extents.x
                && Mathf.Abs(worldPosition.z - Center.z) <= extents.z;
        }

        /// <summary>
        /// 지정한 월드 좌표를 구역 안으로 끌어당긴다. 이미 구역 안이면 좌표를 그대로 돌려준다.
        /// 구역이 비어 있으면 보정할 기준이 없으므로 입력 좌표를 그대로 돌려준다.
        /// </summary>
        /// <param name="worldPosition">보정할 월드 좌표이다.</param>
        /// <returns>구역 안으로 보정한 좌표이다.</returns>
        public Vector3 ClampToArea(Vector3 worldPosition)
        {
            return IsEmpty ? worldPosition : Bounds.ClosestPoint(worldPosition);
        }

        /// <inheritdoc />
        public bool Equals(PlacementArea other) => Center == other.Center && Size == other.Size;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PlacementArea other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Center.GetHashCode() ^ (Size.GetHashCode() << 2);

        /// <inheritdoc />
        public override string ToString() => IsEmpty ? "PlacementArea(None)" : $"PlacementArea({Center}, {Size})";
    }
}
