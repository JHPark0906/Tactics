using HS.Framework.Interaction;
using HS.Framework.UI.Interaction;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>상호작용 포커스 상태의 프롬프트 표시 결정 로직을 확인한다.</summary>
    public sealed class InteractionPromptDisplayTests
    {
        [Test]
        public void EmptyFocusStateHidesPrompt()
        {
            Assert.That(InteractionPromptDisplay.ShouldShow(default), Is.False);
        }

        [Test]
        public void FocusedInteractableShowsPrompt()
        {
            var state = new InteractionFocusState(
                new FakeInteractable(),
                new InteractionAvailability(true));

            Assert.That(InteractionPromptDisplay.ShouldShow(state), Is.True);
        }

        [Test]
        public void UnavailableFocusStillShowsPrompt()
        {
            var state = new InteractionFocusState(
                new FakeInteractable(),
                new InteractionAvailability(false, "열쇠가 필요합니다."));

            Assert.That(InteractionPromptDisplay.ShouldShow(state), Is.True);
        }

        [Test]
        public void AvailableStateHasNoUnavailableReason()
        {
            var availability = new InteractionAvailability(true, "무시되어야 하는 이유");

            Assert.That(InteractionPromptDisplay.ResolveUnavailableReason(availability), Is.Null);
        }

        [Test]
        public void UnavailableStateExposesReason()
        {
            var availability = new InteractionAvailability(false, "열쇠가 필요합니다.");

            Assert.That(
                InteractionPromptDisplay.ResolveUnavailableReason(availability),
                Is.EqualTo("열쇠가 필요합니다."));
        }

        [TestCase(null)]
        [TestCase("")]
        public void UnavailableStateWithoutReasonReturnsNull(string reason)
        {
            var availability = new InteractionAvailability(false, reason);

            Assert.That(InteractionPromptDisplay.ResolveUnavailableReason(availability), Is.Null);
        }

        /// <summary>테스트용 상호작용 대상 구현이다.</summary>
        private sealed class FakeInteractable : IInteractable
        {
            /// <inheritdoc />
            public InteractionPrompt Prompt => new("열기", "문");

            /// <inheritdoc />
            public InteractionAvailability Evaluate(IInteractor interactor) => new(true);

            /// <inheritdoc />
            public void Interact(IInteractor interactor)
            {
            }
        }
    }
}
