using System;
using HS.Framework.UI.Windows;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 배치 단계에서 남은 배치 수와 전투 개시 버튼을 다루는 UI 창이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>수작업으로 연결하는 부분.</b> 창의 프리팹과 버튼, 라벨 배치는 사용자가 직접 만든다.
    /// 전투 개시 버튼의 클릭 이벤트에 <see cref="RequestStartBattle"/>를,
    /// 유닛 선택 버튼에 <see cref="SelectUnitDefinition"/>을 인스펙터에서 연결하면 된다.
    /// 표시 갱신이 필요한 라벨은 <see cref="StateChanged"/>를 구독하는 표시용 컴포넌트가 맡는다.
    /// 이렇게 나눠 두면 이 창이 특정 UI 라이브러리에 묶이지 않는다.
    /// </para>
    /// <para>
    /// <b>모달이 아닌 이유.</b> 창이 모달이면 <see cref="UiWindowManager"/>가 게임플레이 입력을 막는데,
    /// 배치 단계에서는 맵을 조작해 유닛을 놓아야 하므로 입력이 살아 있어야 한다.
    /// 그래서 이 창은 항상 모달이 아니며 취소 입력으로 닫히지도 않는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlacementWindow : UiWindowBase
    {
        [Header("Placement")]
        [Tooltip("이 창이 조작할 배치 컨트롤러이다.")]
        [SerializeField]
        private UnitPlacementController placementController;

        [Tooltip("창을 열 때 기본으로 선택해 둘 유닛 정의이다. 비워 두면 선택 없이 시작한다.")]
        [SerializeField]
        private UnitDefinition defaultUnitDefinition;

        /// <inheritdoc />
        /// <remarks>배치 중에는 맵 조작이 필요하므로 이 창은 모달이 될 수 없다.</remarks>
        public override bool IsModal => false;

        /// <inheritdoc />
        /// <remarks>배치 단계를 취소 입력으로 닫으면 배치를 이어갈 수 없으므로 닫히지 않게 한다.</remarks>
        public override bool CloseOnCancel => false;

        /// <summary>현재 배치하려고 선택한 유닛 정의이며 선택하지 않았으면 null이다.</summary>
        public UnitDefinition SelectedUnitDefinition { get; private set; }

        /// <summary>이 창이 조작하는 배치 컨트롤러이며 연결되지 않았으면 null이다.</summary>
        public UnitPlacementController PlacementController => placementController;

        /// <summary>
        /// 표시할 내용이 바뀌면 발생한다. 남은 배치 수나 단계 라벨을 갱신하는 표시용 컴포넌트가 구독한다.
        /// </summary>
        public event Action<PlacementWindow> StateChanged;

        /// <summary>배치할 유닛 컨트롤러를 연결하거나 교체한다.</summary>
        /// <param name="controller">연결할 배치 컨트롤러이다.</param>
        public void SetPlacementController(UnitPlacementController controller)
        {
            if (placementController == controller)
            {
                return;
            }

            UnsubscribeFromController();
            placementController = controller;
            if (IsOpen)
            {
                SubscribeToController();
            }

            StateChanged?.Invoke(this);
        }

        /// <summary>
        /// 배치할 유닛 정의를 선택한다. 유닛 선택 버튼의 클릭 이벤트에 연결한다.
        /// </summary>
        /// <param name="definition">선택할 유닛 정의이며 null이면 선택을 해제한다.</param>
        public void SelectUnitDefinition(UnitDefinition definition)
        {
            if (SelectedUnitDefinition == definition)
            {
                return;
            }

            SelectedUnitDefinition = definition;
            StateChanged?.Invoke(this);
        }

        /// <summary>
        /// 선택한 유닛을 지정한 좌표에 배치한다. 맵 조작을 처리하는 컴포넌트가 호출한다.
        /// </summary>
        /// <param name="worldPosition">배치할 월드 좌표이다.</param>
        /// <returns>처리 결과이며 컨트롤러가 없으면 <see cref="PlacementResult.NoPlacementZone"/>이다.</returns>
        public PlacementResult RequestPlaceSelectedUnit(Vector3 worldPosition)
        {
            if (placementController == null)
            {
                return PlacementResult.NoPlacementZone;
            }

            return placementController.TryPlaceUnit(SelectedUnitDefinition, worldPosition, out _);
        }

        /// <summary>
        /// 배치한 유닛을 회수한다. 회수 버튼이나 맵 조작 컴포넌트가 호출한다.
        /// </summary>
        /// <param name="entryId">회수할 배치 항목의 식별자이다.</param>
        /// <returns>처리 결과이며 컨트롤러가 없으면 <see cref="PlacementResult.UnknownEntry"/>이다.</returns>
        public PlacementResult RequestRecallUnit(int entryId)
        {
            if (placementController == null)
            {
                return PlacementResult.UnknownEntry;
            }

            return placementController.TryRecallUnit(entryId);
        }

        /// <summary>
        /// 전투 개시 버튼의 클릭 이벤트에 연결하는 진입점이다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>버튼에는 이쪽을 연결한다.</b> UnityEvent의 인스펙터 드롭다운은 값을 돌려주지 않는 메서드만
        /// 목록에 넣으므로, 결과를 돌려주는 <see cref="RequestStartBattle"/>은 <b>목록에 아예 나타나지 않는다.</b>
        /// 그러면 사용자는 안내서를 그대로 따랐는데 마지막 단계에서 고를 것이 없고,
        /// 자기가 무엇을 잘못 만들었는지부터 의심하게 된다.
        /// </para>
        /// <para>
        /// <b>실패를 삼키지 않는다.</b> 버튼을 눌렀는데 아무 일도 일어나지 않는 것 역시 침묵 실패이므로,
        /// 시작하지 못했으면 왜인지 남긴다.
        /// </para>
        /// </remarks>
        public void StartBattle()
        {
            if (RequestStartBattle())
            {
                return;
            }

            Debug.LogWarning(
                "[PlacementWindow] 전투를 시작하지 못했다. 배치 단계가 아니거나 배치한 유닛이 하나도 없다. " +
                "유닛을 배치 구역에 하나 이상 놓았는지, 그리고 이 창이 배치 컨트롤러에 연결되어 있는지 확인해야 한다.",
                this);
        }

        /// <summary>
        /// 전투 개시를 선언한다. 전투가 시작되면 배치 창을 닫는다.
        /// 버튼에 연결할 때는 <see cref="StartBattle"/>을 쓴다.
        /// </summary>
        /// <returns>전투를 시작했으면 true이다.</returns>
        public bool RequestStartBattle()
        {
            if (placementController == null || !placementController.TryStartBattle())
            {
                return false;
            }

            RequestClose();
            return true;
        }

        /// <summary>창이 열리면 기본 선택을 적용하고 컨트롤러의 변경 알림을 구독한다.</summary>
        protected override void OnOpened()
        {
            SubscribeToController();
            if (SelectedUnitDefinition == null)
            {
                SelectedUnitDefinition = defaultUnitDefinition;
            }

            StateChanged?.Invoke(this);
        }

        /// <summary>창이 닫히면 구독을 해제해 닫힌 창이 갱신 알림을 받지 않게 한다.</summary>
        protected override void OnClosed()
        {
            UnsubscribeFromController();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            UnsubscribeFromController();
            base.OnDestroy();
        }

        /// <summary>컨트롤러의 변경 알림을 구독한다. 이미 구독 중이면 중복되지 않게 먼저 해제한다.</summary>
        private void SubscribeToController()
        {
            if (placementController == null)
            {
                return;
            }

            placementController.PlacementChanged -= HandlePlacementChanged;
            placementController.PhaseChanged -= HandlePhaseChanged;
            placementController.PlacementChanged += HandlePlacementChanged;
            placementController.PhaseChanged += HandlePhaseChanged;
        }

        /// <summary>컨트롤러의 변경 알림 구독을 해제한다.</summary>
        private void UnsubscribeFromController()
        {
            if (placementController == null)
            {
                return;
            }

            placementController.PlacementChanged -= HandlePlacementChanged;
            placementController.PhaseChanged -= HandlePhaseChanged;
        }

        /// <summary>배치 내용이 바뀌면 표시 갱신을 알린다.</summary>
        private void HandlePlacementChanged(UnitPlacementController controller)
        {
            StateChanged?.Invoke(this);
        }

        /// <summary>배치 단계가 바뀌면 표시 갱신을 알린다.</summary>
        private void HandlePhaseChanged(PlacementPhase phase)
        {
            StateChanged?.Invoke(this);
        }
    }
}
