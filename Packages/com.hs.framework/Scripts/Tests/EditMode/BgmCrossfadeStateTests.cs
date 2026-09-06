using HS.Framework.Audio;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>배경음 크로스페이드 상태의 슬롯 교대와 가중치 진행 규칙을 검증한다.</summary>
    public sealed class BgmCrossfadeStateTests
    {
        [Test]
        public void InitialStateIsSilent()
        {
            var state = new BgmCrossfadeState();

            Assert.That(state.IsSilent, Is.True);
            Assert.That(state.GetWeight(0), Is.EqualTo(0f));
            Assert.That(state.GetWeight(1), Is.EqualTo(0f));
        }

        [Test]
        public void BeginPlayReturnsSlotZeroFirstAndAlternates()
        {
            var state = new BgmCrossfadeState();

            Assert.That(state.BeginPlay(1f), Is.EqualTo(0));
            Assert.That(state.BeginPlay(1f), Is.EqualTo(1));
            Assert.That(state.BeginPlay(1f), Is.EqualTo(0));
        }

        [Test]
        public void ZeroFadeBeginPlaySetsWeightsInstantly()
        {
            var state = new BgmCrossfadeState();

            var slot = state.BeginPlay(0f);

            Assert.That(state.GetWeight(slot), Is.EqualTo(1f));
            Assert.That(state.GetWeight(1 - slot), Is.EqualTo(0f));
            Assert.That(state.IsSilent, Is.False);
        }

        [Test]
        public void TickProgressesFadeInLinearly()
        {
            var state = new BgmCrossfadeState();
            var slot = state.BeginPlay(2f);

            state.Tick(0.5f);

            Assert.That(state.GetWeight(slot), Is.EqualTo(0.25f).Within(1e-5f));

            state.Tick(2f);

            Assert.That(state.GetWeight(slot), Is.EqualTo(1f));
        }

        [Test]
        public void MidFadeSwapContinuesOutgoingWeightFromCurrentValue()
        {
            var state = new BgmCrossfadeState();
            var firstSlot = state.BeginPlay(1f);
            state.Tick(0.4f);

            var secondSlot = state.BeginPlay(1f);

            Assert.That(secondSlot, Is.EqualTo(1 - firstSlot));
            Assert.That(state.GetWeight(secondSlot), Is.EqualTo(0f));
            Assert.That(state.GetWeight(firstSlot), Is.EqualTo(0.4f).Within(1e-5f));

            state.Tick(0.5f);

            Assert.That(state.GetWeight(secondSlot), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(state.GetWeight(firstSlot), Is.EqualTo(0f));
        }

        [Test]
        public void BeginStopFadesActiveSlotToSilence()
        {
            var state = new BgmCrossfadeState();
            var slot = state.BeginPlay(0f);

            state.BeginStop(2f);
            state.Tick(1f);

            Assert.That(state.IsStopping, Is.True);
            Assert.That(state.GetWeight(slot), Is.EqualTo(0.5f).Within(1e-5f));

            state.Tick(1f);

            Assert.That(state.IsSilent, Is.True);
        }

        [Test]
        public void ZeroFadeBeginStopSilencesImmediately()
        {
            var state = new BgmCrossfadeState();
            state.BeginPlay(0f);

            state.BeginStop(0f);

            Assert.That(state.IsSilent, Is.True);
        }

        [Test]
        public void BeginPlayAfterStopResumesFadeIn()
        {
            var state = new BgmCrossfadeState();
            state.BeginPlay(0f);
            state.BeginStop(0f);

            var slot = state.BeginPlay(1f);
            state.Tick(1f);

            Assert.That(state.IsStopping, Is.False);
            Assert.That(state.GetWeight(slot), Is.EqualTo(1f));
        }

        [Test]
        public void NonPositiveDeltaTimeIsIgnored()
        {
            var state = new BgmCrossfadeState();
            var slot = state.BeginPlay(1f);

            state.Tick(0f);
            state.Tick(-1f);

            Assert.That(state.GetWeight(slot), Is.EqualTo(0f));
        }
    }
}
