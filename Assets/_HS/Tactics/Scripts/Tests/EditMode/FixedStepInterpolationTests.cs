using HS.Tactics.Foundation.Simulation;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>두 로직 스텝 사이를 메우는 비율 계산을 검증한다.</summary>
    /// <remarks>
    /// 이 비율은 화면 쪽으로만 흐른다. 값 자체가 틀리면 유닛이 앞서가거나 뒤처져 보이고,
    /// 범위를 벗어나면 스텝 밖으로 미끄러진다.
    /// </remarks>
    public sealed class FixedStepInterpolationTests
    {
        [Test]
        public void AlphaIsTheFractionOfAStepThatHasElapsed()
        {
            Assert.That(FixedStepInterpolation.ResolveAlpha(0.025f, 0.1f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(FixedStepInterpolation.ResolveAlpha(0.05f, 0.1f), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void AlphaStaysInsideTheStep()
        {
            Assert.That(
                FixedStepInterpolation.ResolveAlpha(10f, 0.1f),
                Is.EqualTo(1f),
                "1을 넘으면 아직 오지 않은 스텝 너머로 미끄러진다.");
            Assert.That(FixedStepInterpolation.ResolveAlpha(-1f, 0.1f), Is.Zero);
        }

        [Test]
        public void AlphaIsZeroRightAfterAStepBoundary()
        {
            Assert.That(FixedStepInterpolation.ResolveAlpha(0f, 0.1f), Is.Zero);
        }

        [Test]
        public void InvalidStepDurationYieldsNoInterpolation()
        {
            Assert.That(
                FixedStepInterpolation.ResolveAlpha(0.05f, 0f),
                Is.Zero,
                "스텝 크기를 모르면 메울 구간도 없다. 0으로 나누지 않는다.");
        }
    }
}
