using System;
using HS.Framework.Interaction;
using R3;
using UnityEngine;

namespace HS.Framework.UI.Interaction
{
    /// <summary>
    /// InteractionController의 포커스 스트림을 구독해 프롬프트 프레젠터의 표시를 갱신한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptBinder : MonoBehaviour
    {
        /// <summary>
        /// 포커스 상태를 발행하는 상호작용 컨트롤러이다.
        /// </summary>
        [SerializeField] private InteractionController interactionController;

        /// <summary>
        /// IInteractionPromptPresenter를 구현한 프레젠터 컴포넌트이다. 비어 있으면 같은 오브젝트에서 찾는다.
        /// </summary>
        [SerializeField] private MonoBehaviour promptPresenterBehaviour;

        private IDisposable _focusSubscription;
        private IInteractionPromptPresenter _presenter;

        private void Awake()
        {
            _presenter = ResolvePresenter();
        }

        private void OnEnable()
        {
            Resubscribe();
        }

        private void OnDisable()
        {
            _focusSubscription?.Dispose();
            _focusSubscription = null;
            _presenter?.Hide();
        }

        /// <summary>
        /// 상호작용 컨트롤러를 지정하고 활성 상태이면 구독을 다시 연결한다.
        /// </summary>
        public void Initialize(InteractionController controller)
        {
            interactionController = controller;
            if (isActiveAndEnabled)
            {
                Resubscribe();
            }
        }

        /// <summary>
        /// 기존 구독을 해제하고 현재 컨트롤러의 포커스 스트림을 다시 구독한다.
        /// </summary>
        private void Resubscribe()
        {
            _focusSubscription?.Dispose();
            _focusSubscription = null;
            if (interactionController == null)
            {
                return;
            }

            _focusSubscription = interactionController.Focus.Subscribe(OnFocusChanged);
        }

        /// <summary>
        /// 포커스 상태 변화를 프레젠터의 표시 갱신으로 변환한다.
        /// </summary>
        private void OnFocusChanged(InteractionFocusState state)
        {
            if (_presenter == null)
            {
                return;
            }

            if (InteractionPromptDisplay.ShouldShow(state))
            {
                _presenter.Show(state.Prompt, state.Availability);
            }
            else
            {
                _presenter.Hide();
            }
        }

        /// <summary>
        /// 직렬화된 프레젠터를 우선 사용하고, 없으면 같은 오브젝트에서 구현체를 찾는다.
        /// </summary>
        private IInteractionPromptPresenter ResolvePresenter()
        {
            if (promptPresenterBehaviour != null)
            {
                if (promptPresenterBehaviour is IInteractionPromptPresenter presenter)
                {
                    return presenter;
                }

                Debug.LogWarning(
                    $"{promptPresenterBehaviour.GetType().Name}은(는) IInteractionPromptPresenter를 구현하지 않습니다.",
                    this);
            }

            return GetComponent<IInteractionPromptPresenter>();
        }
    }
}
