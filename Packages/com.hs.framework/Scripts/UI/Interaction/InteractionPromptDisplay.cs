using HS.Framework.Interaction;

namespace HS.Framework.UI.Interaction
{
    /// <summary>
    /// 상호작용 포커스 상태를 프롬프트 표시 결정으로 변환하는 순수 로직을 제공한다.
    /// </summary>
    public static class InteractionPromptDisplay
    {
        /// <summary>
        /// 지정한 포커스 상태에서 프롬프트를 표시해야 하는지 판정한다.
        /// </summary>
        public static bool ShouldShow(InteractionFocusState state)
        {
            return state.Interactable != null;
        }

        /// <summary>
        /// UI에 표시할 상호작용 불가 이유를 가져온다. 표시할 이유가 없으면 null을 반환한다.
        /// </summary>
        public static string ResolveUnavailableReason(InteractionAvailability availability)
        {
            if (availability.IsAvailable || string.IsNullOrEmpty(availability.UnavailableReason))
            {
                return null;
            }

            return availability.UnavailableReason;
        }
    }
}
