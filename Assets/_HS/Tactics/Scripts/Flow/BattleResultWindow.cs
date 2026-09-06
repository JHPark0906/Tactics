using System;
using HS.Framework.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 전투 결과를 표시하고, 플레이어가 확인하면 흐름에 알리는 창이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>출구는 하나다.</b> 이기든 지든 전투가 끝나면 스테이지 선택으로 돌아가므로, 이 창에는 진행 버튼
    /// 하나뿐이다. 「다음 스테이지로」나 「다시 도전」 같은 갈래는 두지 않는다. 결과에 따라 달라지는
    /// 것은 제목과 본문 문구뿐이다.
    /// </para>
    /// <para>
    /// <b>프리팹 구성.</b> 제목·본문 텍스트와 진행 버튼을 인스펙터에서 연결한다.
    /// 프레임워크 UI 창과 마찬가지로 모든 참조는 선택 사항이며, 연결하지 않은 요소는 조용히 건너뛴다.
    /// 따라서 텍스트 없이 버튼만 있는 최소 구성으로도 동작한다.
    /// </para>
    /// <para>
    /// <b>콜백 규약.</b> 프레임워크 <see cref="ConfirmDialog"/>와 같은 불변식을 따른다.
    /// 곧 <see cref="Show"/>의 요청자는 진행 콜백을 정확히 한 번 받는다.
    /// 버튼을 누르지 않고 창이 닫혀도(ESC, 파괴 포함) 진행으로 확정하는데,
    /// 전투가 끝난 상태에서 진행 외의 선택지가 없고 여기서 멈추면 플레이어가 전투 씬에 갇히기 때문이다.
    /// 열려 있는 창에 새 결과가 도착하면 이전 요청자에게 먼저 진행을 통지한 뒤 새 요청으로 교체한다.
    /// </para>
    /// </remarks>
    public sealed class BattleResultWindow : UiWindowBase
    {
        /// <summary>결과 제목을 표시할 텍스트이다.</summary>
        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;

        /// <summary>다음 행동을 설명할 본문 텍스트이다.</summary>
        [SerializeField] private TMP_Text messageText;

        /// <summary>진행 버튼에 표시할 문구를 담을 텍스트이다.</summary>
        [SerializeField] private TMP_Text proceedButtonLabel;

        /// <summary>스테이지 선택으로 돌아가는 진행 버튼이다.</summary>
        [Header("Buttons")]
        [SerializeField] private Button proceedButton;

        [Header("Result Texts")]
        [Tooltip("승리했을 때 제목에 표시할 문구이다.")]
        [SerializeField]
        private string victoryTitle = "Victory";

        [Tooltip("패배했을 때 제목에 표시할 문구이다.")]
        [SerializeField]
        private string defeatTitle = "Defeat";

        [Tooltip("승리했을 때 본문에 표시할 문구이다.")]
        [SerializeField]
        private string victoryMessage = "스테이지를 클리어했다. 스테이지 선택으로 돌아간다.";

        [Tooltip("패배했을 때 본문에 표시할 문구이다.")]
        [SerializeField]
        private string defeatMessage = "패배했다. 스테이지 선택으로 돌아간다.";

        [Tooltip("진행 버튼에 표시할 문구이다. 결과와 무관하게 하나뿐이다.")]
        [SerializeField]
        private string proceedButtonText = "스테이지 선택";

        private Action _onProceed;

        /// <summary>이번 표시에서 진행이 확정됐는지 여부이다.</summary>
        private bool _isResolved;

        /// <summary>현재 표시 중인 전투 결과이다.</summary>
        public BattleOutcome Outcome { get; private set; }

        private void Awake()
        {
            if (proceedButton != null)
            {
                proceedButton.onClick.AddListener(OnProceedClicked);
            }
        }

        /// <summary>
        /// 파괴 시 버튼 구독을 해제하고 기반 창 정리를 수행한다.
        /// 아직 결과를 받지 못한 요청자가 있으면 진행으로 통지해, 파괴로도 요청이 방치되지 않게 한다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (proceedButton != null)
            {
                proceedButton.onClick.RemoveListener(OnProceedClicked);
            }

            if (IsOpen)
            {
                ResolveAsProceed();
            }

            base.OnDestroy();
        }

        /// <summary>
        /// 전투 결과를 표시하고 진행 선택을 기다린다.
        /// 이미 열려 있고 아직 진행이 확정되지 않았다면 이전 요청자에게 진행을 통지한 뒤 이 요청으로 교체한다.
        /// </summary>
        /// <param name="outcome">표시할 전투 결과이다.</param>
        /// <param name="onProceed">플레이어가 진행을 선택했을 때 호출할 콜백이다.</param>
        /// <param name="isRetry">씬 전환 실패 뒤 다시 표시하는 결과이면 재시도 안내를 표시한다.</param>
        public void Show(BattleOutcome outcome, Action onProceed = null, bool isRetry = false)
        {
            ShowResult(outcome, onProceed, isRetry, saveFailed: false);
        }

        /// <summary>정산 결과의 저장 실패를 표시하고, 저장에 성공한 뒤 스테이지 선택으로 돌아가도록 재시도한다.</summary>
        /// <param name="outcome">이미 정산한 전투 결과이다.</param>
        /// <param name="onRetry">저장을 다시 시도하고 성공했을 때만 이동하는 콜백이다.</param>
        public void ShowSaveFailure(BattleOutcome outcome, Action onRetry)
        {
            ShowResult(outcome, onRetry, isRetry: false, saveFailed: true);
        }

        private void ShowResult(BattleOutcome outcome, Action onProceed, bool isRetry, bool saveFailed)
        {
            // 교체당하는 이전 요청자의 진행 콜백이며, 새 요청을 모두 설치한 뒤 마지막에 통지한다.
            var replacedProceed = IsOpen && !_isResolved ? _onProceed : null;
            BeginRequest();

            Outcome = outcome;
            _onProceed = onProceed;
            _isResolved = false;

            var isVictory = outcome == BattleOutcome.Victory;
            if (titleText != null)
            {
                titleText.text = isVictory ? victoryTitle : defeatTitle;
            }

            if (messageText != null)
            {
                messageText.text = saveFailed
                    ? "진행도를 저장하지 못했다. 저장을 다시 시도한 뒤 스테이지 선택으로 돌아간다."
                    : isRetry
                    ? "스테이지 선택으로 돌아가지 못했다. 다시 시도해 주세요."
                    : isVictory ? victoryMessage : defeatMessage;
            }

            if (proceedButtonLabel != null)
            {
                proceedButtonLabel.text = saveFailed ? "저장 후 스테이지 선택" : proceedButtonText;
            }

            Open();

            replacedProceed?.Invoke();
        }

        /// <summary>버튼 선택 없이 닫혀도 진행으로 확정해 흐름이 멈추지 않게 한다.</summary>
        protected override void OnClosed()
        {
            ResolveAsProceed();
        }

        /// <summary>
        /// 진행 버튼 클릭을 처리한다.
        /// </summary>
        private void OnProceedClicked()
        {
            if (!IsOpen || _isResolved)
            {
                return;
            }

            _isResolved = true;
            var onProceed = _onProceed;
            RequestClose();
            onProceed?.Invoke();
        }

        /// <summary>
        /// 대기 중인 요청을 진행으로 확정한다.
        /// 이미 확정된 요청은 다시 통지하지 않으므로 몇 번 호출해도 통지는 한 번뿐이다.
        /// </summary>
        private void ResolveAsProceed()
        {
            var onProceed = _onProceed;
            var wasResolved = _isResolved;
            _onProceed = null;
            _isResolved = true;

            if (!wasResolved)
            {
                onProceed?.Invoke();
            }
        }
    }
}
