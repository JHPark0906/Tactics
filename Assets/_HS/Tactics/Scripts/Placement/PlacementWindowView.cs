using System.Collections.Generic;
using System.ComponentModel;
using HS.Framework.Foundation.MVVM;
using HS.Framework.UI.Windows;
using HS.Tactics.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 배치 창의 ViewModel을 UGUI 요소에 연결하는 View이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>창을 상속하지 않고 옆에 붙인다.</b> <see cref="UiWindowBase"/>와
    /// <see cref="ViewBase{TViewModel}"/>은 둘 다 <see cref="MonoBehaviour"/>라 한 클래스가 둘 다 될 수 없다.
    /// 그래서 여닫기는 같은 GameObject의 <see cref="PlacementWindow"/>가 그대로 맡고, 이 컴포넌트는
    /// 그 창의 수명주기에 편승해 표시만 맡는다. <c>SettingsWindowView</c>가 쓰는 것과 같은 형태이며,
    /// <b>이렇게 해야 UiWindowManager가 이 창을 다른 창과 똑같이 열고 닫을 수 있다.</b>
    /// </para>
    /// <para>
    /// <b>고른 유닛을 창에도 넘긴다.</b> 맵을 눌러 유닛을 놓는 경로(<see cref="PlacementInputController"/>)는
    /// <see cref="PlacementWindow.SelectedUnitDefinition"/>을 보고 무엇을 놓을지 정한다. 그래서 ViewModel에서
    /// 선택이 바뀌면 창에도 옮겨 준다. <b>옮기지 않으면 버튼으로 고른 유닛과 맵에 놓이는 유닛이 달라진다.</b>
    /// </para>
    /// <para>
    /// <b>버튼과 라벨은 인스펙터에서 연결한다.</b> 유닛 선택 버튼은 목록의 같은 자리와 짝지어지므로,
    /// 버튼을 정의와 같은 순서로 넣어야 한다. 자리가 모자라면 남는 정의는 고를 수 없고,
    /// 버튼이 더 많으면 남는 버튼은 꺼진다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiWindowBase))]
    public sealed class PlacementWindowView : ViewBase<PlacementViewModel>
    {
        [Header("Window")]
        [Tooltip("여닫기를 맡는 창이다. 비워 두면 같은 GameObject에서 찾는다.")]
        [SerializeField]
        private UiWindowBase window;

        [Tooltip("고른 유닛을 넘겨 줄 배치 창이다. 비워 두면 같은 GameObject에서 찾는다.")]
        [SerializeField]
        private PlacementWindow placementWindow;

        [Header("Placement")]
        [Tooltip("상태를 읽어 올 배치 컨트롤러이다. 비워 두면 배치 창에 연결된 것을 쓴다.")]
        [SerializeField]
        private UnitPlacementController placementController;

        [Tooltip("이 스테이지에서 고를 수 있는 유닛 정의이며, 아래 선택 버튼과 같은 순서로 넣는다.")]
        [SerializeField]
        private List<UnitDefinition> selectableUnitDefinitions = new();

        [Header("Widgets")]
        [Tooltip("유닛 선택 버튼이며 위 정의 목록과 같은 순서로 넣는다.")]
        [SerializeField]
        private List<Button> unitSelectionButtons = new();

        [Tooltip("배치 격자의 칸 버튼 아홉이다. 왼쪽 아래가 0번이고 오른쪽으로 세다가 줄이 끝나면 위로 올라간다.\n" +
                 "이 순서가 어긋나면 화면에서 누른 자리와 세계에서 서는 자리가 달라진다.")]
        [SerializeField]
        private List<Button> gridCellButtons = new();

        [Tooltip("전투 개시 버튼이다.")]
        [SerializeField]
        private Button startBattleButton;

        [Tooltip("남은 배치 수를 보여 줄 라벨이다.")]
        [SerializeField]
        private TMP_Text remainingCapacityLabel;

        [Tooltip("고른 유닛의 이름을 보여 줄 라벨이다.")]
        [SerializeField]
        private TMP_Text selectedUnitLabel;

        private PlacementViewModel _ownedViewModel;

        /// <summary>이 View가 편승한 창이며 연결되지 않았으면 null이다.</summary>
        public UiWindowBase Window => window;

        /// <summary>
        /// 조합된 창을 찾아 열림 알림을 구독하고, 기본 ViewModel을 만들어 연결한다.
        /// </summary>
        private void Awake()
        {
            window = window != null ? window : GetComponent<UiWindowBase>();
            placementWindow = placementWindow != null ? placementWindow : GetComponent<PlacementWindow>();

            if (placementController == null && placementWindow != null)
            {
                placementController = placementWindow.PlacementController;
            }

            if (window != null)
            {
                window.Opened += HandleWindowOpened;
            }

            if (ViewModel == null)
            {
                _ownedViewModel = new PlacementViewModel(placementController, selectableUnitDefinitions);
                Initialize(_ownedViewModel);
            }
        }

        /// <inheritdoc />
        protected override void OnViewModelBound()
        {
            BindButtons();
        }

        /// <inheritdoc />
        protected override void OnViewModelUnbinding()
        {
            UnbindButtons();
            DisposeOwnedViewModel();
        }

        /// <inheritdoc />
        protected override void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            Refresh();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            if (window != null)
            {
                window.Opened -= HandleWindowOpened;
            }

            UnbindButtons();
            base.OnDestroy();
            DisposeOwnedViewModel();
        }

        /// <summary>ViewModel의 지금 상태를 버튼과 라벨에 반영한다.</summary>
        public override void Refresh()
        {
            var viewModel = ViewModel;

            if (startBattleButton != null)
            {
                startBattleButton.interactable = viewModel != null && viewModel.CanStartBattle;
            }

            RefreshUnitSelectionButtons(viewModel);
            RefreshGridCellButtons(viewModel);

            if (remainingCapacityLabel != null)
            {
                remainingCapacityLabel.text = viewModel != null
                    ? $"{viewModel.PlacedUnitCount} / {viewModel.PlacementCapacity}"
                    : string.Empty;
            }

            var selected = viewModel?.SelectedUnitDefinition;
            if (selectedUnitLabel != null)
            {
                selectedUnitLabel.text = selected != null ? selected.DisplayName : string.Empty;
            }

            if (placementWindow != null)
            {
                placementWindow.SelectUnitDefinition(selected);
            }
        }

        /// <summary>선택 버튼의 켜짐과 눌림 가능 여부를 지금 상태에 맞춘다.</summary>
        /// <param name="viewModel">읽을 ViewModel이며 null이면 모든 버튼을 끈다.</param>
        private void RefreshUnitSelectionButtons(PlacementViewModel viewModel)
        {
            var definitions = viewModel?.SelectableUnitDefinitions;
            for (var index = 0; index < unitSelectionButtons.Count; index++)
            {
                var button = unitSelectionButtons[index];
                if (button == null)
                {
                    continue;
                }

                var hasDefinition = definitions != null && index < definitions.Count;
                button.gameObject.SetActive(hasDefinition);
                button.interactable = hasDefinition && viewModel.CanSelectUnits;
            }
        }

        /// <summary>
        /// 칸 버튼을 지금 칸 상태에 맞춘다.
        /// </summary>
        /// <remarks>
        /// 찬 칸도 눌리지 않게 둔다. 눌러서 거절당하는 것보다 <b>누를 수 없다는 것이 먼저 보이는 편</b>이
        /// 플레이어에게 규칙을 알려 준다. 회수는 맵에서 유닛을 직접 가리켜 한다.
        /// </remarks>
        /// <param name="viewModel">읽을 ViewModel이며 null이면 모든 칸을 끈다.</param>
        private void RefreshGridCellButtons(PlacementViewModel viewModel)
        {
            var cellStates = viewModel?.CellStates;
            for (var cellIndex = 0; cellIndex < gridCellButtons.Count; cellIndex++)
            {
                var button = gridCellButtons[cellIndex];
                if (button == null)
                {
                    continue;
                }

                var hasCell = cellStates != null && cellIndex < cellStates.Count;
                button.interactable = hasCell
                                      && cellStates[cellIndex] == PlacementCellState.Empty
                                      && viewModel.SelectedUnitDefinition != null;
            }
        }

        /// <summary>버튼의 클릭을 ViewModel의 명령에 잇는다.</summary>
        private void BindButtons()
        {
            for (var cellIndex = 0; cellIndex < gridCellButtons.Count; cellIndex++)
            {
                var button = gridCellButtons[cellIndex];
                if (button == null)
                {
                    continue;
                }

                // 반복 변수를 그대로 담으면 모든 버튼이 마지막 칸을 가리킨다.
                var boundCellIndex = cellIndex;
                button.onClick.AddListener(() => HandleGridCellClicked(boundCellIndex));
            }

            if (startBattleButton != null)
            {
                startBattleButton.onClick.AddListener(HandleStartBattleClicked);
            }

            for (var index = 0; index < unitSelectionButtons.Count; index++)
            {
                var button = unitSelectionButtons[index];
                if (button == null)
                {
                    continue;
                }

                // 인덱스를 지역 변수로 붙든다. 반복 변수를 그대로 담으면 모든 버튼이 마지막 값을 쓴다.
                var definitionIndex = index;
                button.onClick.AddListener(() => HandleUnitSelectionClicked(definitionIndex));
            }
        }

        /// <summary>버튼에 건 클릭 연결을 모두 푼다.</summary>
        private void UnbindButtons()
        {
            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveAllListeners();
            }

            for (var index = 0; index < unitSelectionButtons.Count; index++)
            {
                unitSelectionButtons[index]?.onClick.RemoveAllListeners();
            }

            for (var cellIndex = 0; cellIndex < gridCellButtons.Count; cellIndex++)
            {
                gridCellButtons[cellIndex]?.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// 격자의 칸을 눌렀을 때 그 칸에 고른 유닛을 세운다.
        /// </summary>
        /// <remarks>거절당하면 왜인지 남긴다. 눌렀는데 아무 일도 없으면 그것도 침묵 실패다.</remarks>
        /// <param name="cellIndex">누른 칸의 번호이다.</param>
        private void HandleGridCellClicked(int cellIndex)
        {
            if (ViewModel == null)
            {
                return;
            }

            var result = ViewModel.TryPlaceSelectedUnitAtCell(cellIndex);
            if (result != PlacementResult.Success)
            {
                Debug.LogWarning($"[PlacementWindowView] {cellIndex}번 칸에 유닛을 세우지 못했다: {result}.", this);
            }
        }

        /// <summary>
        /// 전투 개시 버튼을 눌렀을 때 처리한다. 시작하면 창을 닫는다.
        /// </summary>
        /// <remarks>
        /// 버튼이 눌렸는데 아무 일도 일어나지 않으면 그것도 침묵 실패이므로 왜인지 남긴다.
        /// </remarks>
        private void HandleStartBattleClicked()
        {
            if (ViewModel == null)
            {
                return;
            }

            if (!ViewModel.TryStartBattle())
            {
                Debug.LogWarning(
                    "[PlacementWindowView] 전투를 시작하지 못했다. 배치 단계가 아니거나 배치한 유닛이 하나도 없다.",
                    this);
                return;
            }

            if (window != null)
            {
                window.RequestClose();
            }
        }

        /// <summary>유닛 선택 버튼을 눌렀을 때 해당 자리의 정의를 고른다.</summary>
        /// <param name="definitionIndex">누른 버튼과 짝지어진 정의의 자리이다.</param>
        private void HandleUnitSelectionClicked(int definitionIndex)
        {
            var definitions = ViewModel?.SelectableUnitDefinitions;
            if (definitions == null || definitionIndex >= definitions.Count)
            {
                return;
            }

            ViewModel.SelectUnitDefinition(definitions[definitionIndex]);
        }

        /// <summary>창이 열리면 놓쳤을 수 있는 변화를 다시 읽는다.</summary>
        /// <param name="openedWindow">열린 창이며 상태는 보관한 참조에서 읽는다.</param>
        private void HandleWindowOpened(UiWindowBase openedWindow)
        {
            ViewModel?.Refresh();
            Refresh();
        }

        /// <summary>스스로 만들어 소유한 ViewModel을 해제한다. 밖에서 받은 것은 해제하지 않는다.</summary>
        private void DisposeOwnedViewModel()
        {
            if (_ownedViewModel == null)
            {
                return;
            }

            _ownedViewModel.Dispose();
            _ownedViewModel = null;
        }
    }
}
