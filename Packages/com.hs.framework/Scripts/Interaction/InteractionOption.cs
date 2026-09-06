using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Interaction
{
    /// <summary>
    /// 인스펙터에서 조건과 동작을 조합해 구성하는 하나의 상호작용 항목이다.
    /// </summary>
    [Serializable]
    public sealed class InteractionOption
    {
        [SerializeField] private string actionText = "Interact";
        [SerializeField] private string targetText;
        [SerializeField] private List<MonoBehaviour> requirements = new();
        [SerializeField] private List<MonoBehaviour> actions = new();

        /// <summary>상호작용 UI에 표시할 정보다.</summary>
        public InteractionPrompt Prompt => new(actionText, targetText);

        /// <summary>인스펙터에 연결한 조건과 동작 컴포넌트 구성을 검증한다.</summary>
        public bool TryValidateConfiguration(out string error)
        {
            if (!TryValidateRequirements(out error))
            {
                return false;
            }

            return TryValidateActions(out error);
        }

        /// <summary>
        /// 지정한 주체가 이 상호작용 항목을 실행할 수 있는지 검사한다.
        /// </summary>
        public InteractionAvailability Evaluate(IInteractor interactor)
        {
            if (interactor == null)
            {
                return new InteractionAvailability(false);
            }

            if (!TryValidateConfiguration(out var error))
            {
                return new InteractionAvailability(false, error);
            }

            if (requirements == null)
            {
                return new InteractionAvailability(true);
            }

            foreach (var behaviour in requirements)
            {
                var requirement = (IInteractionRequirement)behaviour;
                var availability = requirement.Evaluate(interactor);
                if (!availability.IsAvailable)
                {
                    return availability;
                }
            }

            return new InteractionAvailability(true);
        }

        /// <summary>
        /// 등록된 동작을 순서대로 실행한다.
        /// </summary>
        public void Execute(IInteractor interactor)
        {
            if (!TryValidateConfiguration(out _) || actions == null)
            {
                return;
            }

            foreach (var behaviour in actions)
            {
                ((IInteractionAction)behaviour).Execute(interactor);
            }
        }

        private bool TryValidateRequirements(out string error)
        {
            if (requirements != null)
            {
                for (var index = 0; index < requirements.Count; index++)
                {
                    if (requirements[index] is not IInteractionRequirement)
                    {
                        error = $"조건 목록의 {index + 1}번째 요소는 IInteractionRequirement를 구현한 컴포넌트여야 합니다.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private bool TryValidateActions(out string error)
        {
            if (actions != null)
            {
                for (var index = 0; index < actions.Count; index++)
                {
                    if (actions[index] is not IInteractionAction)
                    {
                        error = $"동작 목록의 {index + 1}번째 요소는 IInteractionAction을 구현한 컴포넌트여야 합니다.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
