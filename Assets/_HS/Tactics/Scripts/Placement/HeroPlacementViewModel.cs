using System.Collections.Generic;
using HS.Framework.Foundation.MVVM;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// MainMenu의 영웅 배치 패널이 다루는 상태를 담는 ViewModel이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="UnitPlacementController"/>를 모른다.</b> <see cref="PlacementViewModel"/>과 달리 이것은
    /// 컨트롤러도 MonoBehaviour도 참조하지 않고 순수 <see cref="UnitPlacementPlan"/> 하나만 감싼다.
    /// MainMenu 위에서는 실제로 유닛을 스폰하지 않으므로 컨트롤러가 할 일 자체가 없다 — 확정한 선택은
    /// <see cref="BuildSelectionEntries"/>로 꺼내 <see cref="PartySelectionService"/>에 커밋하고,
    /// 실제 스폰은 게임플레이 씬에 진입한 뒤 그 씬의 진짜 <see cref="UnitPlacementController"/>가 한다.
    /// </para>
    /// <para>
    /// <b>가짜 배치 구역을 쓴다.</b> 이 계획이 계산하는 칸 중심 좌표는 버려진다 — 게임플레이 씬에서
    /// <see cref="UnitPlacementController.TryPlaceUnitAtCell"/>이 그 씬의 진짜 구역으로 다시 계산하기
    /// 때문이다. 그래서 이 ViewModel은 격자 판정(칸 점유·상한)만 통과하면 되는 크기만 있는 구역이면 충분하다.
    /// </para>
    /// <para>
    /// <b>계획이 알림을 내지 않으므로 매 조작 뒤에 스스로 다시 읽는다.</b> <see cref="UnitPlacementPlan"/>은
    /// 이벤트가 없는 순수 클래스다. 이 ViewModel이 유일한 조작자이므로, 조작 메서드가 끝날 때마다
    /// <see cref="RefreshFromPlan"/>을 불러 속성을 다시 채우고 바뀐 것만 알린다.
    /// </para>
    /// </remarks>
    public sealed class HeroPlacementViewModel : ViewModelBase
    {
        /// <summary>
        /// 계획을 어디에 놓을지는 뜻이 없으므로 격자 판정만 통과할 만큼 넉넉한 고정 구역을 쓴다.
        /// </summary>
        private static readonly PlacementArea DummyArea = new(Vector3.zero, new Vector3(20f, 10f, 20f));

        private readonly UnitPlacementPlan _plan;
        private readonly List<UnitDefinition> _selectableUnitDefinitions = new();
        private readonly PlacementCellState[] _cellStates = new PlacementCellState[PartyRules.GridCellCount];

        private UnitDefinition _selectedUnitDefinition;
        private int _placedUnitCount;
        private int _placementCapacity;

        /// <summary>고를 수 있는 유닛 정의 목록을 받아 ViewModel을 만든다.</summary>
        /// <param name="selectableUnitDefinitions">고를 수 있는 유닛 정의 목록이며 null인 항목은 걸러진다.</param>
        public HeroPlacementViewModel(IReadOnlyList<UnitDefinition> selectableUnitDefinitions)
        {
            _plan = new UnitPlacementPlan(DummyArea);

            if (selectableUnitDefinitions != null)
            {
                for (var index = 0; index < selectableUnitDefinitions.Count; index++)
                {
                    var definition = selectableUnitDefinitions[index];
                    if (definition != null)
                    {
                        _selectableUnitDefinitions.Add(definition);
                    }
                }
            }

            RefreshFromPlan();
        }

        /// <summary>고를 수 있는 유닛 정의 목록이며 만든 뒤에는 바뀌지 않는다.</summary>
        public IReadOnlyList<UnitDefinition> SelectableUnitDefinitions => _selectableUnitDefinitions;

        /// <summary>지금 고른 유닛 정의이며 고르지 않았으면 null이다.</summary>
        public UnitDefinition SelectedUnitDefinition => _selectedUnitDefinition;

        /// <summary>지금까지 세운 유닛 수이다.</summary>
        public int PlacedUnitCount => _placedUnitCount;

        /// <summary>세울 수 있는 유닛 수의 상한이다.</summary>
        public int PlacementCapacity => _placementCapacity;

        /// <summary>더 세울 수 있는 남은 수이며 음수가 되지 않는다.</summary>
        public int RemainingCapacity => Mathf.Max(0, _placementCapacity - _placedUnitCount);

        /// <summary>유닛을 더 고를 수 있는지 여부이며, 상한에 닿으면 거짓이 된다.</summary>
        public bool CanSelectUnits => RemainingCapacity > 0;

        /// <summary>지금 배치로 게임을 시작할 수 있는지 여부이며, 하나도 세우지 않았으면 거짓이다.</summary>
        public bool CanConfirm => _placedUnitCount > 0;

        /// <summary>
        /// 격자 칸 아홉의 상태이며 칸 번호 순서대로 담긴다.
        /// </summary>
        public IReadOnlyList<PlacementCellState> CellStates => _cellStates;

        /// <summary>격자의 칸 수이며 <see cref="PartyRules.GridCellCount"/>를 따른다.</summary>
        public int CellCount => _cellStates.Length;

        /// <summary>
        /// 고른 유닛을 그 칸에 세운다. 비어 있으면 세우고, 이미 유닛이 있으면 그 유닛을 물린다.
        /// </summary>
        /// <remarks>
        /// 맵을 눌러 배치하는 게임플레이 씬과 달리 이 패널에는 회수를 위한 별도 조작이 없다.
        /// 그래서 찬 칸을 다시 누르면 <b>물리는 것</b>으로 다룬다 — 그러지 않으면 잘못 세운 유닛을
        /// 되돌릴 방법이 없다.
        /// </remarks>
        /// <param name="cellIndex">누른 칸의 번호이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult PlaceOrRecallAtCell(int cellIndex)
        {
            PlacementResult result;
            if (_plan.TryGetEntryAtCell(cellIndex, out var occupant))
            {
                result = _plan.TryRecall(occupant.Id, out _);
            }
            else
            {
                result = _plan.TryPlaceAtCell(_selectedUnitDefinition, cellIndex, out _);
            }

            RefreshFromPlan();
            return result;
        }

        /// <summary>
        /// 배치할 유닛 정의를 고른다. 상한에 닿았으면 고르지 않고 거짓을 돌려준다.
        /// null을 넘기는 것은 선택 해제이며 언제나 받아들인다.
        /// </summary>
        /// <param name="definition">고를 유닛 정의이며 null이면 선택을 해제한다.</param>
        /// <returns>선택이 받아들여졌으면 true이다.</returns>
        public bool SelectUnitDefinition(UnitDefinition definition)
        {
            if (definition != null && !CanSelectUnits)
            {
                return false;
            }

            if (_selectedUnitDefinition != definition)
            {
                _selectedUnitDefinition = definition;
                OnPropertyChanged(nameof(SelectedUnitDefinition));
            }

            return true;
        }

        /// <summary>
        /// 지금까지 세운 것을 모두 지운다. 패널을 다시 열 때 지난 판의 선택이 남지 않게 부른다.
        /// </summary>
        public void ResetSelection()
        {
            _plan.Clear();
            SelectUnitDefinition(null);
            RefreshFromPlan();
        }

        /// <summary>
        /// 지금 세운 것을 씬 경계를 넘길 수 있는 값으로 옮긴다.
        /// </summary>
        /// <returns>칸 번호와 유닛 정의만 담은 목록이며, 세운 순서대로이다.</returns>
        public IReadOnlyList<PartySelectionEntry> BuildSelectionEntries()
        {
            var plan = _plan.Entries;
            var entries = new List<PartySelectionEntry>(plan.Count);
            for (var index = 0; index < plan.Count; index++)
            {
                entries.Add(new PartySelectionEntry(plan[index].CellIndex, plan[index].Definition));
            }

            return entries;
        }

        /// <summary>
        /// 계획에서 값을 읽어 바뀐 것만 알린다.
        /// </summary>
        private void RefreshFromPlan()
        {
            var changed = SetProperty(ref _placedUnitCount, _plan.Count, nameof(PlacedUnitCount));
            changed |= SetProperty(ref _placementCapacity, _plan.Capacity, nameof(PlacementCapacity));

            if (RefreshCellStates())
            {
                OnPropertyChanged(nameof(CellStates));
            }

            if (changed)
            {
                OnPropertiesChanged(nameof(RemainingCapacity), nameof(CanSelectUnits), nameof(CanConfirm));
            }

            // 상한에 닿으면 고른 유닛을 더 놓을 곳이 없다. 그대로 두면 더 고를 수 없는데도 골라진 것으로 보인다.
            if (!CanSelectUnits && _selectedUnitDefinition != null)
            {
                SelectUnitDefinition(null);
            }
        }

        /// <summary>칸 아홉의 상태를 다시 읽고 하나라도 달라졌는지 알려 준다.</summary>
        /// <returns>칸 상태가 하나라도 달라졌으면 true이다.</returns>
        private bool RefreshCellStates()
        {
            var changed = false;
            var canPlaceMore = CanSelectUnits;

            for (var cellIndex = 0; cellIndex < _cellStates.Length; cellIndex++)
            {
                PlacementCellState state;
                if (_plan.IsCellOccupied(cellIndex))
                {
                    state = PlacementCellState.Occupied;
                }
                else
                {
                    state = canPlaceMore ? PlacementCellState.Empty : PlacementCellState.Unavailable;
                }

                if (_cellStates[cellIndex] != state)
                {
                    _cellStates[cellIndex] = state;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
