using System.Collections.Generic;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 배치 단계에서 어떤 유닛을 어디에 놓았는지 관리하고 배치 규칙을 판정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GameObject를 다루지 않는 순수 클래스라서 EditMode 테스트에서 씬이나 수명주기 없이 검증할 수 있다.
    /// 실제 유닛 인스턴스는 <see cref="UnitPlacementController"/>가 항목 식별자에 맞춰 따로 보관하며,
    /// 세계에 무언가를 만들기 전에 반드시 이 계획으로 먼저 규칙을 확인한다.
    /// </para>
    /// <para>
    /// 배치 단계에서는 유닛을 자유롭게 옮기고 회수할 수 있으므로, 이 계획도 추가·이동·제거를 모두 지원하며
    /// 회수한 자리는 즉시 다시 채울 수 있다.
    /// </para>
    /// </remarks>
    public sealed class UnitPlacementPlan
    {
        /// <summary>
        /// 스테이지당 배치할 수 있는 유닛 수의 기본 상한이며 <see cref="PartyRules.MaxPartySize"/>를 따른다.
        /// 배치 상한은 유닛 수로 계산한다.
        /// </summary>
        public const int DefaultCapacity = PartyRules.MaxPartySize;

        private readonly List<PlacementEntry> _entries = new();
        private int _nextEntryId = 1;

        /// <summary>지정한 구역과 상한으로 배치 계획을 생성한다.</summary>
        /// <param name="area">유닛을 놓을 수 있는 구역이다.</param>
        /// <param name="capacity">배치할 수 있는 유닛 수의 상한이며 범위를 벗어나면 보정한다.</param>
        public UnitPlacementPlan(PlacementArea area, int capacity = DefaultCapacity)
        {
            Area = area;
            Capacity = ClampCapacity(capacity);
        }

        /// <summary>유닛을 놓을 수 있는 구역이다.</summary>
        public PlacementArea Area { get; private set; }

        /// <summary>
        /// 구역을 칸으로 나눈 격자이며, 배치는 이 칸 위에서만 일어난다.
        /// </summary>
        /// <remarks>
        /// 구역이 바뀌면 칸도 함께 바뀌므로 따로 보관하지 않고 그때그때 만든다.
        /// 값 하나뿐인 구조체라 만드는 비용이 없고, 보관하면 <see cref="SetArea"/>와 어긋날 자리가 생긴다.
        /// </remarks>
        public PlacementGrid Grid => new(Area);

        /// <summary>배치할 수 있는 유닛 수의 상한이며 항상 1 이상이다.</summary>
        public int Capacity { get; private set; }

        /// <summary>현재 배치한 유닛 수이다.</summary>
        public int Count => _entries.Count;

        /// <summary>더 배치할 수 있는 남은 수이다.</summary>
        public int RemainingCapacity => Mathf.Max(0, Capacity - _entries.Count);

        /// <summary>상한까지 모두 배치했는지 여부이다.</summary>
        public bool IsFull => _entries.Count >= Capacity;

        /// <summary>배치한 항목을 배치 순서대로 열거한다.</summary>
        public IReadOnlyList<PlacementEntry> Entries => _entries;

        /// <summary>
        /// 배치 가능 구역을 갱신한다. 이미 배치한 항목은 옮기지 않으므로,
        /// 구역이 좁아지면 구역 밖에 남는 항목이 생길 수 있다.
        /// </summary>
        /// <param name="area">새로 적용할 구역이다.</param>
        public void SetArea(PlacementArea area)
        {
            Area = area;
        }

        /// <summary>
        /// 배치 수 상한을 갱신한다. 이미 배치한 항목은 제거하지 않으므로,
        /// 상한을 낮추면 일시적으로 상한을 넘긴 상태가 될 수 있다.
        /// </summary>
        /// <param name="capacity">새로 적용할 상한이며 1 미만이면 1로 보정한다.</param>
        public void SetCapacity(int capacity)
        {
            Capacity = ClampCapacity(capacity);
        }

        /// <summary>
        /// 상한을 파티 규칙이 허용하는 범위로 보정한다.
        /// </summary>
        /// <remarks>
        /// <b>여기가 파티 규칙이 강제되는 한 자리다.</b> 스테이지가 상한을 더 낮게 잡는 것은 허용하지만
        /// <see cref="PartyRules.MaxPartySize"/>보다 크게 잡는 것은 받아들이지 않는다. 씬에 저장된 값이
        /// 규칙보다 클 수 있고, 그것을 그대로 따르면 <b>경험치를 나누는 인원수와 실제로 데려간 인원수가 갈린다.</b>
        /// </remarks>
        /// <param name="capacity">보정할 상한이다.</param>
        /// <returns>1 이상 <see cref="PartyRules.MaxPartySize"/> 이하로 보정한 상한이다.</returns>
        private static int ClampCapacity(int capacity)
        {
            return Mathf.Clamp(capacity, 1, PartyRules.MaxPartySize);
        }

        /// <summary>
        /// 유닛을 배치할 수 있는지 판정하고, 가능하면 계획에 추가한다.
        /// 정의 유무, 구역 유무, 상한, 구역 포함 여부 순으로 확인한다.
        /// </summary>
        /// <param name="definition">배치할 유닛의 정의이다.</param>
        /// <param name="position">배치할 월드 좌표이다.</param>
        /// <param name="entry">추가한 항목이며 실패하면 기본값이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryPlace(UnitDefinition definition, Vector3 position, out PlacementEntry entry)
        {
            entry = default;
            if (!Grid.TryGetCellIndex(position, out var cellIndex))
            {
                return Area.IsEmpty ? PlacementResult.NoPlacementZone : PlacementResult.OutsidePlacementZone;
            }

            return TryPlaceAtCell(definition, cellIndex, out entry);
        }

        /// <summary>
        /// 격자 칸을 골라 유닛을 배치한다.
        /// </summary>
        /// <remarks>
        /// UI의 칸 버튼이 부르는 자리이며, 월드 좌표로 들어온 요청도 칸을 알아낸 뒤 여기로 모인다.
        /// <b>배치가 실제로 일어나는 곳은 이 하나다.</b>
        /// </remarks>
        /// <param name="definition">배치할 유닛의 정의이다.</param>
        /// <param name="cellIndex">유닛을 세울 칸의 번호이다.</param>
        /// <param name="entry">추가한 항목이며 실패하면 기본값이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryPlaceAtCell(UnitDefinition definition, int cellIndex, out PlacementEntry entry)
        {
            entry = default;
            if (definition == null)
            {
                return PlacementResult.MissingDefinition;
            }

            if (Area.IsEmpty)
            {
                return PlacementResult.NoPlacementZone;
            }

            var grid = Grid;
            if (!grid.TryGetCellCenter(cellIndex, out var cellCenter))
            {
                return PlacementResult.OutsidePlacementZone;
            }

            if (!grid.CanFit(definition.Radius * 2f))
            {
                return PlacementResult.CellTooSmall;
            }

            if (IsFull)
            {
                return PlacementResult.CapacityReached;
            }

            if (IsCellOccupied(cellIndex))
            {
                return PlacementResult.CellOccupied;
            }

            entry = new PlacementEntry(_nextEntryId++, definition, cellIndex, cellCenter);
            _entries.Add(entry);
            return PlacementResult.Success;
        }

        /// <summary>그 칸에 이미 유닛이 서 있는지 확인한다.</summary>
        /// <param name="cellIndex">확인할 칸의 번호이다.</param>
        /// <returns>그 칸을 쓰는 항목이 있으면 true이다.</returns>
        public bool IsCellOccupied(int cellIndex)
        {
            return TryGetEntryAtCell(cellIndex, out _);
        }

        /// <summary>그 칸에 선 배치 항목을 찾는다.</summary>
        /// <param name="cellIndex">찾을 칸의 번호이다.</param>
        /// <param name="entry">찾은 항목이며 없으면 기본값이다.</param>
        /// <returns>그 칸을 쓰는 항목을 찾았으면 true이다.</returns>
        public bool TryGetEntryAtCell(int cellIndex, out PlacementEntry entry)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].CellIndex == cellIndex)
                {
                    entry = _entries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// 이미 배치한 유닛을 구역 안의 다른 좌표로 옮긴다.
        /// 배치 단계에서는 이동이 자유로우므로 상한은 다시 확인하지 않는다.
        /// </summary>
        /// <param name="entryId">옮길 항목의 식별자이다.</param>
        /// <param name="position">새로 배치할 월드 좌표이다.</param>
        /// <param name="movedEntry">옮긴 뒤의 항목이며 실패하면 기본값이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryMove(int entryId, Vector3 position, out PlacementEntry movedEntry)
        {
            movedEntry = default;
            if (!Grid.TryGetCellIndex(position, out var cellIndex))
            {
                return Area.IsEmpty ? PlacementResult.NoPlacementZone : PlacementResult.OutsidePlacementZone;
            }

            return TryMoveToCell(entryId, cellIndex, out movedEntry);
        }

        /// <summary>
        /// 이미 배치한 유닛을 다른 칸으로 옮긴다. 상한은 다시 확인하지 않는다.
        /// </summary>
        /// <remarks>
        /// 제자리로 옮기는 것은 받아들인다 — 자기 칸을 자기가 막고 있다고 거절하면
        /// <b>플레이어에게는 방금 집어 든 유닛을 도로 놓지 못하는 것으로 보인다.</b>
        /// </remarks>
        /// <param name="entryId">옮길 항목의 식별자이다.</param>
        /// <param name="cellIndex">옮겨 갈 칸의 번호이다.</param>
        /// <param name="movedEntry">옮긴 뒤의 항목이며 실패하면 기본값이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryMoveToCell(int entryId, int cellIndex, out PlacementEntry movedEntry)
        {
            movedEntry = default;
            var index = IndexOf(entryId);
            if (index < 0)
            {
                return PlacementResult.UnknownEntry;
            }

            if (Area.IsEmpty)
            {
                return PlacementResult.NoPlacementZone;
            }

            var grid = Grid;
            if (!grid.TryGetCellCenter(cellIndex, out var cellCenter))
            {
                return PlacementResult.OutsidePlacementZone;
            }

            if (!grid.CanFit(_entries[index].Definition.Radius * 2f))
            {
                return PlacementResult.CellTooSmall;
            }

            if (TryGetEntryAtCell(cellIndex, out var occupant) && occupant.Id != entryId)
            {
                return PlacementResult.CellOccupied;
            }

            movedEntry = _entries[index].WithCell(cellIndex, cellCenter);
            _entries[index] = movedEntry;
            return PlacementResult.Success;
        }

        /// <summary>
        /// 배치한 유닛을 계획에서 회수한다. 회수한 자리는 즉시 다시 채울 수 있다.
        /// </summary>
        /// <param name="entryId">회수할 항목의 식별자이다.</param>
        /// <param name="removedEntry">회수한 항목이며 실패하면 기본값이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryRecall(int entryId, out PlacementEntry removedEntry)
        {
            removedEntry = default;
            var index = IndexOf(entryId);
            if (index < 0)
            {
                return PlacementResult.UnknownEntry;
            }

            removedEntry = _entries[index];
            _entries.RemoveAt(index);
            return PlacementResult.Success;
        }

        /// <summary>식별자로 배치 항목을 찾는다.</summary>
        /// <param name="entryId">찾을 항목의 식별자이다.</param>
        /// <param name="entry">찾은 항목이며 없으면 기본값이다.</param>
        /// <returns>항목을 찾았으면 true이다.</returns>
        public bool TryGetEntry(int entryId, out PlacementEntry entry)
        {
            var index = IndexOf(entryId);
            if (index < 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[index];
            return true;
        }

        /// <summary>배치한 항목을 모두 비운다. 다음에 발급할 식별자는 이어서 증가한다.</summary>
        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>식별자에 해당하는 항목의 위치를 찾으며, 없으면 -1을 반환한다.</summary>
        private int IndexOf(int entryId)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Id == entryId)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
