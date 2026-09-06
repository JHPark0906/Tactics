using UnityEngine;
using VContainer;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 게임플레이 씬 진입 시 MainMenu에서 커밋된 영웅 배치를 재생하고 전투를 시작한다.
    /// 각 항목은 UnitPlacementController.TryPlaceUnitAtCell로 전달해 실제 배치 구역에서 좌표와 스폰 규칙을 적용한다.
    /// 커밋된 선택이 없으면 아무것도 하지 않아, 씬을 직접 연 경우 해당 씬의 배치 UI를 사용할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartySelectionReplay : MonoBehaviour
    {
        [Tooltip("재생할 배치 컨트롤러이다. 비워 두면 씬에서 찾는다.")]
        [SerializeField]
        private UnitPlacementController placementController;

        private PartySelectionService _partySelectionService;

        /// <summary>MainMenu에서 커밋된 선택을 들고 있는 서비스를 주입받는다.</summary>
        /// <param name="partySelectionService">커밋된 선택을 보관하는 서비스이다.</param>
        [Inject]
        public void InjectPartySelectionService(PartySelectionService partySelectionService)
        {
            _partySelectionService = partySelectionService;
        }

        private void Start()
        {
            if (_partySelectionService == null || !_partySelectionService.HasSelection)
            {
                return;
            }

            var controller = ResolveController();
            if (controller == null)
            {
                Debug.LogWarning(
                    "[PartySelectionReplay] 배치 컨트롤러를 찾지 못해 MainMenu에서 고른 배치를 재생하지 못했다. " +
                    "커밋된 선택은 그대로 버려진다.",
                    this);
                return;
            }

            Replay(controller, _partySelectionService.TakeAndClear());
        }

        /// <summary>커밋된 항목을 순서대로 재생하고, 전부 시도한 뒤 전투를 시작한다.</summary>
        /// <param name="controller">재생에 쓸 배치 컨트롤러이다.</param>
        /// <param name="entries">재생할 항목이다.</param>
        private void Replay(UnitPlacementController controller, System.Collections.Generic.IReadOnlyList<PartySelectionEntry> entries)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var result = controller.TryPlaceUnitAtCell(entry.Definition, entry.CellIndex, out _);
                if (result != PlacementResult.Success)
                {
                    var definitionName = entry.Definition != null ? entry.Definition.DisplayName : "(없음)";
                    Debug.LogWarning(
                        $"[PartySelectionReplay] MainMenu에서 고른 {definitionName}을 {entry.CellIndex}번 칸에 다시 세우지 못했다: {result}.",
                        this);
                }
            }

            if (!controller.TryStartBattle())
            {
                Debug.LogWarning(
                    "[PartySelectionReplay] 재생을 마쳤지만 전투를 시작하지 못했다. 세운 유닛이 하나도 없다.",
                    this);
            }
        }

        /// <summary>연결된 컨트롤러를 쓰거나, 없으면 씬에서 찾는다.</summary>
        /// <returns>재생에 쓸 배치 컨트롤러이며 없으면 null이다.</returns>
        private UnitPlacementController ResolveController()
        {
            return placementController != null
                ? placementController
                : FindFirstObjectByType<UnitPlacementController>(FindObjectsInactive.Include);
        }
    }
}
