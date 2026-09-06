using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HS.Framework.UI.Windows
{
    /// <summary>
    /// 제목/본문과 확인·취소 콜백을 받아 표시하는 범용 확인 팝업 창이다.
    /// 버튼을 누르지 않고 닫히면(ESC 등) 취소로 처리한다.
    /// </summary>
    /// <remarks>
    /// 재진입 규약은 "이전 요청자에게 취소를 통지한 뒤 교체한다"이다.
    /// 이미 열려 있는 상태에서 <see cref="Show"/>가 다시 호출되면 새 요청을 거부하지 않고,
    /// 아직 결과를 받지 못한 이전 요청자에게 취소 콜백을 보낸 다음 새 요청으로 교체한다.
    /// 따라서 모든 요청자는 확인 또는 취소 콜백을 정확히 한 번 받는다는 불변식이 성립하며,
    /// 어떤 요청자도 결과를 영원히 기다리는 상태에 빠지지 않는다.
    /// 이전 요청자의 취소 통지는 새 요청을 완전히 설치한 뒤에 호출하므로,
    /// 그 콜백 안에서 <see cref="Show"/>를 다시 호출해도 같은 규약이 그대로 적용된다.
    /// </remarks>
    public sealed class ConfirmDialog : UiWindowBase
    {
        /// <summary>
        /// 팝업 제목을 표시할 텍스트이다.
        /// </summary>
        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;

        /// <summary>
        /// 팝업 본문을 표시할 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text messageText;

        /// <summary>
        /// 확인 버튼이다.
        /// </summary>
        [Header("Buttons")]
        [SerializeField] private Button confirmButton;

        /// <summary>
        /// 취소 버튼이다.
        /// </summary>
        [SerializeField] private Button cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        /// <summary>
        /// 이번 표시에서 버튼 선택으로 결과가 확정됐는지 여부이다.
        /// </summary>
        private bool _isResolved;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        /// <summary>
        /// 파괴 시 버튼 구독을 해제하고 기반 창 정리를 수행한다.
        /// 아직 결과를 받지 못한 요청자가 있으면 취소로 통지해, 파괴로도 요청이 방치되지 않게 한다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            }

            if (IsOpen)
            {
                ResolveAsCancel();
            }

            base.OnDestroy();
        }

        /// <summary>
        /// 제목/본문과 콜백을 설정하고 팝업을 연다.
        /// 이미 열려 있고 아직 결과가 확정되지 않았다면 이전 요청자에게 취소를 통지한 뒤 이 요청으로 교체한다.
        /// </summary>
        /// <param name="title">표시할 제목이다.</param>
        /// <param name="message">표시할 본문이다.</param>
        /// <param name="onConfirm">확인 선택 시 호출할 콜백이다.</param>
        /// <param name="onCancel">취소 선택 시 또는 버튼 없이 닫힐 때 호출할 콜백이다.</param>
        public void Show(string title, string message, Action onConfirm = null, Action onCancel = null)
        {
            // 교체당하는 이전 요청자의 취소 콜백이며, 새 요청을 모두 설치한 뒤 마지막에 통지한다.
            var replacedCancel = IsOpen && !_isResolved ? _onCancel : null;
            BeginRequest();

            if (titleText != null)
            {
                titleText.text = title;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _isResolved = false;
            Open();

            replacedCancel?.Invoke();
        }

        /// <summary>
        /// 버튼 선택 없이 닫히면 취소 콜백을 호출하고, 콜백 참조를 정리한다.
        /// </summary>
        protected override void OnClosed()
        {
            ResolveAsCancel();
        }

        /// <summary>
        /// 대기 중인 요청을 취소로 확정한다.
        /// 이미 결과가 확정된 요청은 다시 통지하지 않으므로, 몇 번 호출해도 통지는 한 번뿐이다.
        /// </summary>
        private void ResolveAsCancel()
        {
            var onCancel = _onCancel;
            var wasResolved = _isResolved;
            _onConfirm = null;
            _onCancel = null;
            _isResolved = true;

            if (!wasResolved)
            {
                onCancel?.Invoke();
            }
        }

        /// <summary>
        /// 확인 버튼 클릭을 처리한다.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (!IsOpen || _isResolved)
            {
                return;
            }

            _isResolved = true;
            var onConfirm = _onConfirm;
            RequestClose();
            onConfirm?.Invoke();
        }

        /// <summary>
        /// 취소 버튼 클릭을 처리한다.
        /// </summary>
        private void OnCancelClicked()
        {
            if (!IsOpen || _isResolved)
            {
                return;
            }

            _isResolved = true;
            var onCancel = _onCancel;
            RequestClose();
            onCancel?.Invoke();
        }
    }
}
