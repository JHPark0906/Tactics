using Cysharp.Threading.Tasks;
using HS.Framework.Foundation.MVVM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 단일 입력 바인딩 ViewModel을 UGUI 요소에 표시하고 편집 입력을 전달한다.
    /// </summary>
    public sealed class SettingsInputBindingItemView : ViewBase<InputBindingItemViewModel>
    {
        /// <summary>
        /// 액션 이름을 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text actionNameText;

        /// <summary>
        /// 바인딩 이름을 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text bindingNameText;

        /// <summary>
        /// 바인딩 경로를 직접 입력하는 입력 필드이다.
        /// </summary>
        [SerializeField] private TMP_InputField bindingPathInput;

        /// <summary>
        /// 다음 입력을 받아 바인딩을 변경하는 버튼이다.
        /// </summary>
        [SerializeField] private Button rebindButton;

        /// <summary>
        /// 현재 바인딩 또는 입력 대기 상태를 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text bindingDisplayText;

        /// <summary>
        /// 바인딩을 기본값으로 되돌리는 버튼이다.
        /// </summary>
        [SerializeField] private Button resetButton;

        /// <summary>
        /// 입력 바인딩 ViewModel과 UI 요소를 연결한다.
        /// </summary>
        protected override void OnViewModelBound()
        {
            if (bindingPathInput != null)
            {
                bindingPathInput.onValueChanged.RemoveListener(OnBindingPathChanged);
                bindingPathInput.onValueChanged.AddListener(OnBindingPathChanged);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(OnResetClicked);
                resetButton.onClick.AddListener(OnResetClicked);
            }

            if (rebindButton != null)
            {
                rebindButton.onClick.RemoveListener(OnRebindClicked);
                rebindButton.onClick.AddListener(OnRebindClicked);
            }
        }

        /// <summary>
        /// UI 표시 값을 ViewModel 상태와 맞춘다.
        /// </summary>
        public override void Refresh()
        {
            if (ViewModel == null)
            {
                return;
            }

            if (actionNameText != null)
            {
                actionNameText.text = string.IsNullOrWhiteSpace(ViewModel.ActionMapName)
                    ? ViewModel.ActionName
                    : $"{ViewModel.ActionMapName}/{ViewModel.ActionName}";
            }

            if (bindingNameText != null)
            {
                bindingNameText.text = ViewModel.BindingName;
            }

            if (bindingPathInput != null && bindingPathInput.text != ViewModel.PendingPath)
            {
                bindingPathInput.SetTextWithoutNotify(ViewModel.PendingPath);
            }

            if (bindingDisplayText != null)
            {
                bindingDisplayText.text = ViewModel.IsRebinding ? "입력 대기 중... (Esc: 취소)" : ViewModel.DisplayName;
            }

            if (rebindButton != null)
            {
                rebindButton.interactable = !ViewModel.IsRebinding;
            }
        }

        /// <summary>
        /// 오브젝트가 제거될 때 UI 이벤트 구독을 정리한다.
        /// </summary>
        protected override void OnDestroy()
        {
            ViewModel?.CancelRebinding();

            if (bindingPathInput != null)
            {
                bindingPathInput.onValueChanged.RemoveListener(OnBindingPathChanged);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(OnResetClicked);
            }

            if (rebindButton != null)
            {
                rebindButton.onClick.RemoveListener(OnRebindClicked);
            }

            base.OnDestroy();
        }

        /// <summary>
        /// 설정창이 닫혀 UI가 비활성화되면 진행 중인 재지정을 취소한다.
        /// </summary>
        private void OnDisable()
        {
            ViewModel?.CancelRebinding();
        }

        /// <summary>
        /// 입력 필드 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="path">새 입력 바인딩 경로이다.</param>
        private void OnBindingPathChanged(string path)
        {
            if (ViewModel != null)
            {
                ViewModel.PendingPath = path;
            }
        }

        /// <summary>
        /// 기본 바인딩으로 되돌리고 UI 표시를 갱신한다.
        /// </summary>
        private void OnResetClicked()
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.ResetToDefaults();
            Refresh();
        }

        /// <summary>
        /// 대화형 입력 재지정을 시작한다.
        /// </summary>
        private void OnRebindClicked()
        {
            RebindAsync().Forget();
        }

        /// <summary>
        /// 입력 수집이 끝날 때까지 UI 상태를 갱신한다.
        /// </summary>
        /// <returns>입력 수집 종료 시 완료된다.</returns>
        private async UniTask RebindAsync()
        {
            if (ViewModel == null)
            {
                return;
            }

            Refresh();
            try
            {
                if (!await ViewModel.StartInteractiveRebindingAsync())
                {
                    return;
                }
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (this != null && isActiveAndEnabled)
                {
                    Refresh();
                }
            }
        }
    }
}
