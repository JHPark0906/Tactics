using HS.Tactics.Combat;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 기본 명중률과 굴림에 따른 명중 판정을 검증한다.
    /// 엄폐는 명중률에 영향을 주지 않으며 피해 흡수는 별도 어빌리티가 담당한다.
    /// </summary>
    public sealed class HitChanceCalculatorTests
    {
        [Test]
        public void RollBelowChanceHitsAndRollAtOrAboveChanceMisses()
        {
            Assert.That(HitChanceCalculator.IsHit(0.5f, 0.49f), Is.True);
            Assert.That(HitChanceCalculator.IsHit(0.5f, 0.5f), Is.False, "경계값은 빗나감으로 처리한다.");
            Assert.That(HitChanceCalculator.IsHit(0.5f, 0.51f), Is.False);
        }

        [Test]
        public void FullHitChanceAlwaysHits()
        {
            Assert.That(HitChanceCalculator.IsHit(1f, 0f), Is.True);
            Assert.That(HitChanceCalculator.IsHit(1f, 0.999f), Is.True);
        }

        [Test]
        public void TheHitChanceIsClampedIntoTheValidRange()
        {
            Assert.That(HitChanceCalculator.IsHit(1.5f, 0.999f), Is.True, "1을 넘겨도 언제나 명중한다.");
            Assert.That(HitChanceCalculator.IsHit(-0.5f, 0f), Is.False, "0 아래로 내려가도 언제나 빗나간다.");
        }
    }
}
