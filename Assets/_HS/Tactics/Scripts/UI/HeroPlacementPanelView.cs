using System;
using System.Collections.Generic;
using System.ComponentModel;
using HS.Framework.Foundation.MVVM;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HS.Tactics.UI
{
    /// <summary>
    /// MainMenu 위에서 나타나는 영웅 배치 패널의 View이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>씬 전환을 모른다.</b> 이 View가 아는 것은 격자와 선택, "게임 시작"·"뒤로" 두 결정뿐이다.
    /// 그 결정을 <see cref="Confirmed"/>·<see cref="Cancelled"/>로 밖에 알리기만 하고, 실제로 무엇을
    /// 할지(<see cref="PartySelectionService"/>에 커밋하고 씬을 넘기는 것)는 이 패널을 여닫는
    /// <c>TacticsMainMenuView</c>가 정한다. <see cref="HS.Tactics.Placement.PlacementWindowView"/>와 같은
    /// 게임플레이 씬의 배치 창은 이 클래스와 전혀 관계가 없다 — 실물 스폰이 없는 MainMenu 전용 View다.
    /// </para>
    /// <para>
    /// <b>ViewModel은 열 때 만든다.</b> 패널은 평소 꺼져 있다가 스테이지를 고르면 열리므로,
    /// <see cref="Open"/>에서 처음 한 번 <see cref="HeroPlacementViewModel"/>을 만들고 그 뒤로는
    /// 재사용하며 <see cref="HeroPlacementViewModel.ResetSelection"/>으로 지난 선택만 지운다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HeroPlacementPanelView : ViewBase<HeroPlacementViewModel>
    {
        [Header("Placement")]
        [Tooltip("이 패널에서 고를 수 있는 유닛 정의이며, 아래 선택 버튼·초상 이미지와 같은 순서로 넣는다.")]
        [SerializeField]
        private List<UnitDefinition> selectableUnitDefinitions = new();

        [Header("Widgets")]
        [Tooltip("유닛 선택 버튼이며 위 정의 목록과 같은 순서로 넣는다.")]
        [SerializeField]
        private List<Button> unitSelectionButtons = new();

        [Tooltip("정지 3D 초상을 그릴 자리이며 위 정의 목록과 같은 순서로 넣는다.")]
        [SerializeField]
        private List<RawImage> unitPortraitImages = new();

        [Tooltip("초상을 굽는 컴포넌트이다.")]
        [SerializeField]
        private HeroPortraitRenderer portraitRenderer;

        [Tooltip("배치 격자의 칸 버튼 아홉이다. 왼쪽 아래가 0번이고 오른쪽으로 세다가 줄이 끝나면 위로 올라간다.")]
        [SerializeField]
        private List<Button> gridCellButtons = new();

        [Tooltip("\"게임 시작\" 버튼이다. 하나 이상 세워야 눌린다.")]
        [SerializeField]
        private Button confirmButton;

        [Tooltip("배치를 취소하고 패널을 닫는 버튼이다. 없어도 된다.")]
        [SerializeField]
        private Button backButton;

        [Tooltip("남은 배치 수를 보여 줄 라벨이다.")]
        [SerializeField]
        private TMP_Text remainingCapacityLabel;

        [Tooltip("고른 유닛의 이름을 보여 줄 라벨이다.")]
        [SerializeField]
        private TMP_Text selectedUnitLabel;

        private HeroPlacementViewModel _ownedViewModel;
        private bool _buttonsAreBound;

        /// <summary>
        /// "게임 시작"이 눌렸을 때 발생하며, 그 순간 세운 배치를 실어 나른다.
        /// 구독하는 쪽이 <see cref="PartySelectionService"/>에 커밋하고 씬을 넘긴다.
        /// </summary>
        public event Action<IReadOnlyList<PartySelectionEntry>> Confirmed;

        /// <summary>"뒤로"가 눌렸을 때 발생한다. 아무것도 커밋하지 않는다.</summary>
        public event Action Cancelled;

        /// <summary>
        /// 패널을 연다. 처음 열면 ViewModel을 만들고, 다시 열면 지난 선택을 지운다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);

            if (_ownedViewModel == null)
            {
                _ownedViewModel = new HeroPlacementViewModel(selectableUnitDefinitions);
                Initialize(_ownedViewModel);
                BindButtons();
            }
            else
            {
                _ownedViewModel.ResetSelection();
            }

            RefreshPortraits();
            Refresh();
        }

        /// <summary>패널을 닫는다. 커밋 여부와 무관하게 그냥 감춘다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <inheritdoc />
        protected override void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            Refresh();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            UnbindButtons();
            base.OnDestroy();
            _ownedViewModel?.Dispose();
            _ownedViewModel = null;
        }

        /// <summary>ViewModel의 지금 상태를 버튼과 라벨에 반영한다.</summary>
        public override void Refresh()
        {
            var viewModel = ViewModel;

            if (confirmButton != null)
            {
                confirmButton.interactable = viewModel != null && viewModel.CanConfirm;
            }

            RefreshUnitSelectionButtons(viewModel);
            RefreshGridCellButtons(viewModel);

            if (remainingCapacityLabel != null)
            {
                remainingCapacityLabel.text = viewModel != null
                    ? $"{viewModel.PlacedUnitCount} / {viewModel.PlacementCapacity}"
                    : string.Empty;
            }

            if (selectedUnitLabel != null)
            {
                var selected = viewModel?.SelectedUnitDefinition;
                selectedUnitLabel.text = selected != null ? selected.DisplayName : string.Empty;
            }
        }

        /// <summary>선택 버튼의 눌림 가능 여부를 지금 상태에 맞춘다. 정지 초상은 여기서 다시 굽지 않는다.</summary>
        private void RefreshUnitSelectionButtons(HeroPlacementViewModel viewModel)
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
        /// 칸 버튼을 지금 칸 상태에 맞춘다. 빈 칸은 유닛을 골랐을 때만, 찬 칸은 물리기 위해 언제나 눌린다.
        /// </summary>
        private void RefreshGridCellButtons(HeroPlacementViewModel viewModel)
        {
            var cellStates = viewModel?.CellStates;
            for (var cellIndex = 0; cellIndex < gridCellButtons.Count; cellIndex++)
            {
                var button = gridCellButtons[cellIndex];
                if (button == null)
                {
                    continue;
                }

                if (cellStates == null || cellIndex >= cellStates.Count)
                {
                    button.interactable = false;
                    continue;
                }

                var state = cellStates[cellIndex];
                button.interactable = state == PlacementCellState.Occupied
                    || (state == PlacementCellState.Empty && viewModel.SelectedUnitDefinition != null);
            }
        }

        /// <summary>정의 목록 순서대로 정지 초상을 굽거나 캐시에서 읽어 이미지에 채운다.</summary>
        private void RefreshPortraits()
        {
            if (portraitRenderer == null)
            {
                return;
            }

            for (var index = 0; index < unitPortraitImages.Count; index++)
            {
                var image = unitPortraitImages[index];
                if (image == null)
                {
                    continue;
                }

                var hasDefinition = index < selectableUnitDefinitions.Count && selectableUnitDefinitions[index] != null;
                if (!hasDefinition)
                {
                    image.gameObject.SetActive(false);
                    continue;
                }

                var texture = portraitRenderer.GetOrBakePortrait(selectableUnitDefinitions[index]);
                image.texture = texture;
                image.gameObject.SetActive(texture != null);
            }
        }

        /// <summary>버튼의 클릭을 ViewModel의 명령과 이 패널의 알림에 잇는다. 한 번만 건다.</summary>
        private void BindButtons()
        {
            if (_buttonsAreBound)
            {
                return;
            }

            _buttonsAreBound = true;

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

            for (var index = 0; index < unitSelectionButtons.Count; index++)
            {
                var button = unitSelectionButtons[index];
                if (button == null)
                {
                    continue;
                }

                var definitionIndex = index;
                button.onClick.AddListener(() => HandleUnitSelectionClicked(definitionIndex));
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(HandleBackClicked);
            }
        }

        /// <summary>버튼에 건 클릭 연결을 모두 푼다.</summary>
        private void UnbindButtons()
        {
            if (!_buttonsAreBound)
            {
                return;
            }

            _buttonsAreBound = false;

            foreach (var button in gridCellButtons)
            {
                button?.onClick.RemoveAllListeners();
            }

            foreach (var button in unitSelectionButtons)
            {
                button?.onClick.RemoveAllListeners();
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
            }
        }

        /// <summary>격자의 칸을 눌렀을 때 세우거나 물린다. 거절당하면 왜인지 남긴다.</summary>
        private void HandleGridCellClicked(int cellIndex)
        {
            if (ViewModel == null)
            {
                return;
            }

            var result = ViewModel.PlaceOrRecallAtCell(cellIndex);
            if (result != PlacementResult.Success)
            {
                Debug.LogWarning($"[HeroPlacementPanelView] {cellIndex}번 칸을 처리하지 못했다: {result}.", this);
            }
        }

        /// <summary>유닛 선택 버튼을 눌렀을 때 해당 자리의 정의를 고른다.</summary>
        private void HandleUnitSelectionClicked(int definitionIndex)
        {
            var definitions = ViewModel?.SelectableUnitDefinitions;
            if (definitions == null || definitionIndex >= definitions.Count)
            {
                return;
            }

            ViewModel.SelectUnitDefinition(definitions[definitionIndex]);
        }

        /// <summary>"게임 시작"을 눌렀을 때 지금 세운 배치를 실어 알린다.</summary>
        private void HandleConfirmClicked()
        {
            if (ViewModel == null || !ViewModel.CanConfirm)
            {
                Debug.LogWarning("[HeroPlacementPanelView] 세운 유닛이 하나도 없어 게임을 시작할 수 없다.", this);
                return;
            }

            Confirmed?.Invoke(ViewModel.BuildSelectionEntries());
        }

        /// <summary>"뒤로"를 눌렀을 때 취소를 알린다.</summary>
        private void HandleBackClicked()
        {
            Cancelled?.Invoke();
        }
    }
}
