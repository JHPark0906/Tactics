using System;
using UnityEngine;

namespace HS.Framework.Interaction
{
    /// <summary>
    /// 상호작용을 실행할 수 있는 주체를 나타낸다.
    /// </summary>
    public interface IInteractor
    {
        /// <summary>
        /// 상호작용을 실행하는 GameObject를 가져온다.
        /// </summary>
        GameObject GameObject { get; }
    }

    /// <summary>
    /// 플레이어가 사용할 수 있는 월드 오브젝트를 나타낸다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 상호작용 UI에 표시할 정보를 가져온다.
        /// </summary>
        InteractionPrompt Prompt { get; }

        /// <summary>
        /// 지정한 주체가 현재 오브젝트와 상호작용할 수 있는지 검사한다.
        /// </summary>
        InteractionAvailability Evaluate(IInteractor interactor);

        /// <summary>
        /// 지정한 주체의 상호작용 요청을 처리한다.
        /// </summary>
        void Interact(IInteractor interactor);
    }

    /// <summary>
    /// 상호작용 가능 여부와 실패 이유를 전달한다.
    /// </summary>
    public readonly struct InteractionAvailability
    {
        /// <summary>
        /// 상호작용 가능 여부를 가져온다.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// 상호작용할 수 없을 때 UI에 표시할 이유를 가져온다.
        /// </summary>
        public string UnavailableReason { get; }

        /// <summary>
        /// 상호작용 가능 여부를 생성한다.
        /// </summary>
        public InteractionAvailability(bool isAvailable, string unavailableReason = null)
        {
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason;
        }
    }

    /// <summary>
    /// 상호작용 UI에 표시할 텍스트 데이터를 나타낸다.
    /// </summary>
    [Serializable]
    public readonly struct InteractionPrompt
    {
        /// <summary>
        /// 상호작용 동작의 이름을 가져온다.
        /// </summary>
        public string ActionText { get; }

        /// <summary>
        /// 상호작용 대상의 이름을 가져온다.
        /// </summary>
        public string TargetText { get; }

        /// <summary>
        /// UI에 표시할 상호작용 정보를 생성한다.
        /// </summary>
        public InteractionPrompt(string actionText, string targetText)
        {
            ActionText = actionText;
            TargetText = targetText;
        }
    }

    /// <summary>현재 상호작용 대상과 표시 정보를 함께 보관하는 읽기 전용 상태이다.</summary>
    public readonly struct InteractionFocusState : IEquatable<InteractionFocusState>
    {
        /// <summary>현재 주시 중인 상호작용 대상이다.</summary>
        public IInteractable Interactable { get; }

        /// <summary>현재 대상의 상호작용 가능 상태이다.</summary>
        public InteractionAvailability Availability { get; }

        /// <summary>현재 대상의 표시 정보이다.</summary>
        public InteractionPrompt Prompt { get; }

        /// <summary>상호작용 포커스 상태를 생성한다.</summary>
        public InteractionFocusState(IInteractable interactable, InteractionAvailability availability)
        {
            Interactable = interactable;
            Availability = availability;
            Prompt = interactable?.Prompt ?? default;
        }

        /// <inheritdoc />
        public bool Equals(InteractionFocusState other)
        {
            return ReferenceEquals(Interactable, other.Interactable) &&
                   Availability.IsAvailable == other.Availability.IsAvailable &&
                   string.Equals(Availability.UnavailableReason, other.Availability.UnavailableReason,
                       StringComparison.Ordinal) &&
                   string.Equals(Prompt.ActionText, other.Prompt.ActionText, StringComparison.Ordinal) &&
                   string.Equals(Prompt.TargetText, other.Prompt.TargetText, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is InteractionFocusState other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(
            Interactable,
            Availability.IsAvailable,
            Availability.UnavailableReason,
            Prompt.ActionText,
            Prompt.TargetText);
    }

    /// <summary>
    /// 상호작용 가능 여부를 판정하는 조건을 정의한다.
    /// </summary>
    public interface IInteractionRequirement
    {
        /// <summary>
        /// 지정한 주체가 이 조건을 충족하는지 검사한다.
        /// </summary>
        InteractionAvailability Evaluate(IInteractor interactor);
    }

    /// <summary>
    /// 상호작용이 실행될 때 수행할 동작을 정의한다.
    /// </summary>
    public interface IInteractionAction
    {
        /// <summary>
        /// 지정한 상호작용 주체를 기준으로 동작을 수행한다.
        /// </summary>
        void Execute(IInteractor interactor);
    }

    /// <summary>
    /// 상호작용 프롬프트 UI의 표시를 담당한다.
    /// </summary>
    public interface IInteractionPromptPresenter
    {
        /// <summary>
        /// 상호작용 프롬프트를 표시한다.
        /// </summary>
        void Show(InteractionPrompt prompt, InteractionAvailability availability);

        /// <summary>
        /// 현재 표시 중인 상호작용 프롬프트를 숨긴다.
        /// </summary>
        void Hide();
    }

}
