using System.Collections.Generic;
using HS.Framework.Foundation.MVVM;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>배치 격자의 칸 하나가 화면에서 어떻게 보여야 하는지이다.</summary>
    public enum PlacementCellState
    {
        /// <summary>지금 유닛을 세울 수 없는 칸이다. 배치 단계가 아니거나 상한에 닿았을 때이다.</summary>
        Unavailable = 0,

        /// <summary>비어 있어 유닛을 세울 수 있는 칸이다.</summary>
        Empty = 1,

        /// <summary>이미 유닛이 서 있는 칸이다.</summary>
        Occupied = 2
    }

    /// <summary>
    /// 배치 창이 표시하고 조작하는 상태를 담는 ViewModel이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UI 타입을 모른다.</b> 버튼도 라벨도 캔버스도 여기서는 다루지 않는다. 그래서 이 클래스는
    /// 씬 없이 EditMode에서 그대로 검사할 수 있고, 화면 구성이 바뀌어도 고칠 일이 없다.
    /// 표시로 옮기는 일은 <see cref="PlacementWindowView"/>가 맡는다.
    /// </para>
    /// <para>
    /// <b>상태의 출처는 하나다.</b> 단계와 배치 수는 <see cref="UnitPlacementController"/>가 쥐고 있고
    /// 이 ViewModel은 그것을 읽어 속성으로 비출 뿐이다. 값을 따로 세어 두면 두 벌이 어긋날 수 있고,
    /// 어긋난 쪽이 화면이면 <b>플레이어는 배치가 되는 줄 알고 계속 누르게 된다.</b>
    /// </para>
    /// <para>
    /// <b>고른 유닛은 여기서만 바뀐다.</b> 상한에 닿았거나 배치 단계가 아니면 선택을 받아들이지 않는다.
    /// 받아들인 뒤 배치에서 거절하면 화면에는 골라진 것으로 보이는데 놓이지는 않아,
    /// <b>플레이어가 자기 조작을 의심하게 된다.</b>
    /// </para>
    /// </remarks>
    public sealed class PlacementViewModel : ViewModelBase
    {
        private readonly UnitPlacementController _controller;
        private readonly List<UnitDefinition> _selectableUnitDefinitions = new();
        private readonly PlacementCellState[] _cellStates = new PlacementCellState[PartyRules.GridCellCount];

        private UnitDefinition _selectedUnitDefinition;
        private PlacementPhase _phase;
        private int _placedUnitCount;
        private int _placementCapacity;

        /// <summary>배치 컨트롤러와 고를 수 있는 유닛 정의를 받아 ViewModel을 만든다.</summary>
        /// <param name="controller">상태를 읽고 명령을 넘길 배치 컨트롤러이며 null이면 모든 명령이 거절된다.</param>
        /// <param name="selectableUnitDefinitions">고를 수 있는 유닛 정의 목록이며 null인 항목은 걸러진다.</param>
        public PlacementViewModel(
            UnitPlacementController controller,
            IReadOnlyList<UnitDefinition> selectableUnitDefinitions = null)
        {
            _controller = controller;

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

            if (_controller != null)
            {
                _controller.PhaseChanged += HandlePhaseChanged;
                _controller.PlacementChanged += HandlePlacementChanged;
            }

            ReadFromController();
        }

        /// <summary>고를 수 있는 유닛 정의 목록이며 만든 뒤에는 바뀌지 않는다.</summary>
        public IReadOnlyList<UnitDefinition> SelectableUnitDefinitions => _selectableUnitDefinitions;

        /// <summary>지금 고른 유닛 정의이며 고르지 않았으면 null이다.</summary>
        public UnitDefinition SelectedUnitDefinition => _selectedUnitDefinition;

        /// <summary>지금의 배치 단계이다.</summary>
        public PlacementPhase Phase => _phase;

        /// <summary>지금까지 배치한 유닛 수이다.</summary>
        public int PlacedUnitCount => _placedUnitCount;

        /// <summary>배치할 수 있는 유닛 수의 상한이다.</summary>
        public int PlacementCapacity => _placementCapacity;

        /// <summary>더 배치할 수 있는 남은 수이며 음수가 되지 않는다.</summary>
        public int RemainingCapacity => Mathf.Max(0, _placementCapacity - _placedUnitCount);

        /// <summary>지금이 유닛을 놓고 회수할 수 있는 배치 단계인지 여부이다.</summary>
        public bool IsPlacing => _phase == PlacementPhase.Placing;

        /// <summary>유닛을 더 고를 수 있는지 여부이며, 상한에 닿으면 거짓이 된다.</summary>
        public bool CanSelectUnits => IsPlacing && RemainingCapacity > 0;

        /// <summary>전투를 시작할 수 있는지 여부이며, 배치한 유닛이 하나도 없으면 거짓이다.</summary>
        public bool CanStartBattle => IsPlacing && _placedUnitCount > 0;

        /// <summary>배치한 유닛을 회수할 수 있는지 여부이다.</summary>
        public bool CanRecallUnits => IsPlacing && _placedUnitCount > 0;

        /// <summary>
        /// 격자 칸 아홉의 상태이며 칸 번호 순서대로 담긴다.
        /// </summary>
        /// <remarks>
        /// <b>전투가 시작되면 전부 <see cref="PlacementCellState.Unavailable"/>이 된다.</b>
        /// 격자는 처음 서는 자리를 정할 뿐이므로 전투 중에는 칸을 묻지도 보여 주지도 않는다.
        /// </remarks>
        public IReadOnlyList<PlacementCellState> CellStates => _cellStates;

        /// <summary>격자의 칸 수이며 <see cref="PartyRules.GridCellCount"/>를 따른다.</summary>
        public int CellCount => _cellStates.Length;

        /// <summary>
        /// 고른 유닛을 그 칸에 세운다. UI의 칸 버튼과 맵 클릭이 함께 지나는 자리이다.
        /// </summary>
        /// <param name="cellIndex">유닛을 세울 칸의 번호이다.</param>
        /// <returns>처리 결과이며 컨트롤러가 없으면 <see cref="PlacementResult.NoPlacementZone"/>이다.</returns>
        public PlacementResult TryPlaceSelectedUnitAtCell(int cellIndex)
        {
            if (_controller == null)
            {
                return PlacementResult.NoPlacementZone;
            }

            return _controller.TryPlaceUnitAtCell(_selectedUnitDefinition, cellIndex, out _);
        }

        /// <summary>
        /// 배치할 유닛 정의를 고른다.
        /// </summary>
        /// <remarks>
        /// 상한에 닿았거나 배치 단계가 아니면 고르지 않고 거짓을 돌려준다. null을 넘기는 것은 선택 해제이며
        /// 단계와 무관하게 언제나 받아들인다.
        /// </remarks>
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

        /// <summary>전투 개시를 컨트롤러에 요청한다.</summary>
        /// <returns>전투가 시작됐으면 true이다.</returns>
        public bool TryStartBattle()
        {
            return _controller != null && _controller.TryStartBattle();
        }

        /// <summary>배치한 유닛 하나를 회수한다.</summary>
        /// <param name="entryId">회수할 배치 항목의 식별자이다.</param>
        /// <returns>처리 결과이며 컨트롤러가 없으면 <see cref="PlacementResult.UnknownEntry"/>이다.</returns>
        public PlacementResult TryRecallUnit(int entryId)
        {
            return _controller != null ? _controller.TryRecallUnit(entryId) : PlacementResult.UnknownEntry;
        }

        /// <summary>
        /// 컨트롤러의 지금 상태를 다시 읽어 속성에 반영한다.
        /// </summary>
        /// <remarks>
        /// 창을 열 때처럼 알림을 놓쳤을 수 있는 시점에 부른다. 알림이 없는 사이에 상한이나 구역이 바뀌었어도
        /// 이것으로 화면이 실제와 다시 맞는다.
        /// </remarks>
        public void Refresh()
        {
            ReadFromController();
        }

        /// <inheritdoc />
        protected override void OnDispose()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.PhaseChanged -= HandlePhaseChanged;
            _controller.PlacementChanged -= HandlePlacementChanged;
        }

        /// <summary>배치 단계가 바뀌면 상태를 다시 읽는다.</summary>
        /// <param name="phase">컨트롤러가 알려 온 새 단계이며 상태는 컨트롤러에서 다시 읽는다.</param>
        private void HandlePhaseChanged(PlacementPhase phase)
        {
            ReadFromController();
        }

        /// <summary>배치 내용이 바뀌면 상태를 다시 읽는다.</summary>
        /// <param name="controller">알림을 보낸 컨트롤러이며 상태는 보관한 참조에서 읽는다.</param>
        private void HandlePlacementChanged(UnitPlacementController controller)
        {
            ReadFromController();
        }

        /// <summary>
        /// 컨트롤러에서 값을 읽어 바뀐 것만 알린다.
        /// </summary>
        /// <remarks>
        /// 저장하는 값은 셋뿐이고 나머지는 그 셋에서 계산한다. 그래서 셋 중 하나라도 바뀌면 계산되는 속성도
        /// 함께 알린다 — 계산되는 속성에는 필드가 없어 <see cref="ViewModelBase.SetProperty{T}"/>가
        /// 알려 줄 수 없기 때문이다.
        /// </remarks>
        private void ReadFromController()
        {
            var phase = _controller != null ? _controller.Phase : PlacementPhase.Preparing;
            var placedUnitCount = _controller != null ? _controller.PlacedUnitCount : 0;
            var placementCapacity = _controller != null ? _controller.PlacementCapacity : 0;

            var changed = SetProperty(ref _phase, phase, nameof(Phase));
            changed |= SetProperty(ref _placedUnitCount, placedUnitCount, nameof(PlacedUnitCount));
            changed |= SetProperty(ref _placementCapacity, placementCapacity, nameof(PlacementCapacity));

            // 셈이 그대로여도 칸은 바뀔 수 있다. 유닛을 다른 칸으로 옮기면 수는 같고 자리만 달라진다.
            // 그래서 칸 상태는 위의 셋과 따로 다시 읽는다.
            if (RefreshCellStates())
            {
                OnPropertyChanged(nameof(CellStates));
            }

            if (!changed)
            {
                return;
            }

            OnPropertiesChanged(
                nameof(RemainingCapacity),
                nameof(IsPlacing),
                nameof(CanSelectUnits),
                nameof(CanStartBattle),
                nameof(CanRecallUnits));

            // 배치 단계를 벗어나면 고른 유닛을 놓을 곳이 없다. 그대로 두면 전투 중에도 골라진 것으로 보인다.
            if (!IsPlacing)
            {
                SelectUnitDefinition(null);
            }
        }

        /// <summary>
        /// 칸 아홉의 상태를 다시 읽고 하나라도 달라졌는지 알려 준다.
        /// </summary>
        /// <remarks>
        /// <b>배치 단계가 아니면 계획도 격자도 묻지 않는다.</b> 전투가 시작된 뒤에는 칸이 무엇을 담고
        /// 있든 화면에 쓸 일이 없으므로, 그때는 조회 자체를 하지 않고 전부 「고를 수 없음」으로 둔다.
        /// 격자를 초기 배치 밖으로 끌고 나가지 않겠다는 것이 여기서 지켜진다.
        /// </remarks>
        /// <returns>칸 상태가 하나라도 달라졌으면 true이다.</returns>
        private bool RefreshCellStates()
        {
            var changed = false;
            var placing = IsPlacing && _controller != null;
            var plan = placing ? _controller.Plan : null;
            var canPlaceMore = placing && RemainingCapacity > 0;

            for (var cellIndex = 0; cellIndex < _cellStates.Length; cellIndex++)
            {
                PlacementCellState state;
                if (plan == null)
                {
                    state = PlacementCellState.Unavailable;
                }
                else if (plan.IsCellOccupied(cellIndex))
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
