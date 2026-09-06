using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 배치 구역을 <see cref="PartyRules.GridColumns"/>×<see cref="PartyRules.GridRows"/> 칸으로 나누어
    /// 칸 번호와 평면 좌표를 서로 옮긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>격자는 처음 서는 자리까지다.</b> 전투가 시작되면 유닛은 격자를 지키지 않고 자유롭게 움직인다.
    /// 그래서 여기에는 <b>이동도 경로도 점유 관리도 없다.</b> 칸 번호와 좌표를 옮기는 계산만 있다.
    /// 점유는 이미 배치한 항목이 답할 수 있는 물음이므로 <see cref="UnitPlacementPlan"/>이 그대로 답한다 —
    /// 점유표를 따로 두면 같은 물음에 답하는 것이 둘이 되고, 둘은 언젠가 어긋난다.
    /// </para>
    /// <para>
    /// <b>칸 번호는 왼쪽 아래에서 0으로 시작해 오른쪽으로 간다.</b> 한 줄이 끝나면 다음 줄로 올라간다.
    /// 3×3이면 0~2가 아래 줄, 3~5가 가운데, 6~8이 위 줄이다. UI 버튼도 같은 순서로 넣어야
    /// 화면에서 누른 자리와 세계에서 서는 자리가 맞는다.
    /// </para>
    /// <para>
    /// <b>높이는 다루지 않는다.</b> 구역의 높이는 그대로 두고 가로·세로만 나눈다. 칸의 중심 높이는
    /// 구역 중심의 높이이며, 지형에 맞추는 일은 스폰하는 쪽이 한다.
    /// </para>
    /// </remarks>
    public readonly struct PlacementGrid
    {
        /// <summary>배치 구역을 받아 격자를 만든다.</summary>
        /// <param name="area">칸으로 나눌 배치 구역이다.</param>
        public PlacementGrid(PlacementArea area)
        {
            Area = area;
        }

        /// <summary>칸으로 나눈 배치 구역이다.</summary>
        public PlacementArea Area { get; }

        /// <summary>격자의 칸 수이며 <see cref="PartyRules.GridCellCount"/>를 따른다.</summary>
        public int CellCount => PartyRules.GridCellCount;

        /// <summary>칸을 만들 수 없는 구역인지 여부이다.</summary>
        public bool IsEmpty => Area.IsEmpty;

        /// <summary>한 칸의 가로·세로 크기이며 높이는 구역의 높이를 그대로 쓴다.</summary>
        public Vector3 CellSize => new(
            Area.Size.x / PartyRules.GridColumns,
            Area.Size.y,
            Area.Size.z / PartyRules.GridRows);

        /// <summary>
        /// 지정한 지름이 한 칸 안에 들어가는지 확인한다.
        /// </summary>
        /// <remarks>
        /// 두 축의 칸 크기가 다를 수 있으므로 좁은 쪽 기준으로 답한다.
        /// 어느 칸이든 크기가 같으므로 특정 칸 번호는 필요하지 않다.
        /// </remarks>
        /// <param name="diameter">확인할 지름(미터)이다.</param>
        /// <returns>구역이 비어 있지 않고 두 축 모두 지름 이상이면 true이다.</returns>
        public bool CanFit(float diameter)
        {
            if (IsEmpty)
            {
                return false;
            }

            var cellSize = CellSize;
            return diameter <= cellSize.x && diameter <= cellSize.z;
        }

        /// <summary>칸 번호가 격자 안에 있는지 확인한다.</summary>
        /// <param name="cellIndex">확인할 칸 번호이다.</param>
        /// <returns>0 이상 <see cref="CellCount"/> 미만이면 true이다.</returns>
        public bool IsValidCellIndex(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < CellCount;
        }

        /// <summary>칸 번호에 해당하는 칸의 중심 좌표를 구한다.</summary>
        /// <param name="cellIndex">중심을 구할 칸 번호이다.</param>
        /// <param name="cellCenter">칸의 중심 좌표이며 실패하면 기본값이다.</param>
        /// <returns>칸 번호가 격자 안이고 구역이 비어 있지 않으면 true이다.</returns>
        public bool TryGetCellCenter(int cellIndex, out Vector3 cellCenter)
        {
            cellCenter = default;
            if (IsEmpty || !IsValidCellIndex(cellIndex))
            {
                return false;
            }

            var column = cellIndex % PartyRules.GridColumns;
            var row = cellIndex / PartyRules.GridColumns;
            var cellSize = CellSize;

            // 칸의 중심은 구역의 왼쪽 아래 모서리에서 반 칸 들어간 자리에서 시작해 칸 크기만큼 나아간다.
            cellCenter = new Vector3(
                Area.Center.x - Area.Extents.x + ((column + 0.5f) * cellSize.x),
                Area.Center.y,
                Area.Center.z - Area.Extents.z + ((row + 0.5f) * cellSize.z));
            return true;
        }

        /// <summary>
        /// 월드 좌표가 놓인 칸의 번호를 구한다.
        /// </summary>
        /// <remarks>
        /// 높이는 보지 않는다. 클릭한 지점의 높이는 지형을 타지만 칸은 평면으로 나뉘기 때문이다.
        /// 구역 밖을 가리키면 실패로 답한다 — 가장 가까운 칸으로 끌어당기면 구역을 한참 벗어난 클릭도
        /// 배치로 이어져, <b>플레이어가 겨냥하지 않은 자리에 유닛이 선다.</b>
        /// </remarks>
        /// <param name="worldPosition">칸을 알아볼 월드 좌표이다.</param>
        /// <param name="cellIndex">찾은 칸 번호이며 실패하면 -1이다.</param>
        /// <returns>좌표가 구역 안이면 true이다.</returns>
        public bool TryGetCellIndex(Vector3 worldPosition, out int cellIndex)
        {
            cellIndex = -1;
            if (IsEmpty || !Area.ContainsIgnoringHeight(worldPosition))
            {
                return false;
            }

            var cellSize = CellSize;
            var column = Mathf.Clamp(
                Mathf.FloorToInt((worldPosition.x - (Area.Center.x - Area.Extents.x)) / cellSize.x),
                0,
                PartyRules.GridColumns - 1);
            var row = Mathf.Clamp(
                Mathf.FloorToInt((worldPosition.z - (Area.Center.z - Area.Extents.z)) / cellSize.z),
                0,
                PartyRules.GridRows - 1);

            cellIndex = (row * PartyRules.GridColumns) + column;
            return true;
        }
    }
}
