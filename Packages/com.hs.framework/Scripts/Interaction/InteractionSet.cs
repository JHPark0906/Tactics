using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Interaction
{
    /// <summary>
    /// 하나의 월드 오브젝트에 여러 상호작용 항목을 제공한다.
    /// 목록의 위쪽 항목이 우선적으로 선택된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionSet : MonoBehaviour, IInteractable
    {
        [SerializeField] private List<InteractionOption> options = new();

        private InteractionOption _selectedOption;
        private InteractionAvailability _availability;
        private IInteractor _evaluatedInteractor;

        /// <inheritdoc />
        public InteractionPrompt Prompt => _selectedOption?.Prompt ?? default;

        /// <inheritdoc />
        public InteractionAvailability Evaluate(IInteractor interactor)
        {
            _selectedOption = null;
            _availability = new InteractionAvailability(false);
            _evaluatedInteractor = interactor;

            if (options == null)
            {
                return _availability;
            }

            foreach (var option in options)
            {
                if (option == null)
                {
                    continue;
                }

                var availability = option.Evaluate(interactor);
                if (availability.IsAvailable)
                {
                    _selectedOption = option;
                    _availability = availability;
                    return _availability;
                }

                if (string.IsNullOrWhiteSpace(_availability.UnavailableReason) &&
                    !string.IsNullOrWhiteSpace(availability.UnavailableReason))
                {
                    _availability = availability;
                }
            }

            return _availability;
        }

        /// <summary>
        /// 가장 최근에 동일한 주체로 평가한 상호작용 항목을 실행한다.
        /// 평가와 실행을 분리해 조건을 두 번 검사하지 않는다.
        /// </summary>
        public void Interact(IInteractor interactor)
        {
            if (!ReferenceEquals(_evaluatedInteractor, interactor) ||
                !_availability.IsAvailable ||
                _selectedOption == null)
            {
                return;
            }

            _selectedOption.Execute(interactor);
        }

        private void OnValidate()
        {
            if (options == null)
            {
                Debug.LogError("[InteractionSet] 상호작용 옵션 목록이 비어 있습니다.", this);
                return;
            }

            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                if (option == null)
                {
                    Debug.LogError($"[InteractionSet] {index + 1}번째 상호작용 옵션이 비어 있습니다.", this);
                    continue;
                }

                if (!option.TryValidateConfiguration(out var error))
                {
                    Debug.LogError($"[InteractionSet] {index + 1}번째 상호작용 옵션: {error}", this);
                }
            }
        }
    }
}
