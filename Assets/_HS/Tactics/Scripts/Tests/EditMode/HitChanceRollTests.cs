using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 확률 굴림의 양 끝을 고정한다. 검사들은 명중률과 흡수 확률을 1이나 0으로 두어 결과를 정하므로,
    /// 그 두 값이 굴림과 무관하게 언제나 같은 답을 내야 어떤 검사도 가끔 붉어지지 않는다.
    /// </summary>
    /// <remarks>
    /// Unity 의 <c>Random.value</c> 는 1.0을 포함하므로 「1이면 항상 성공」은 굴림 비교만으로는 성립하지 않는다.
    /// 여러 번 굴려 보는 것은 그 끝값 처리가 굴림에 앞서 있음을 드러내기 위해서다.
    /// </remarks>
    public sealed class HitChanceRollTests
    {
        private const int Trials = 256;

        [Test]
        public void AChanceOfOneAlwaysSucceeds()
        {
            for (var trial = 0; trial < Trials; trial++)
            {
                Assert.That(HitChanceCalculator.Roll(1f), Is.True, "확률 1은 굴림과 무관하게 성공이어야 한다.");
            }
        }

        [Test]
        public void AChanceOfZeroNeverSucceeds()
        {
            for (var trial = 0; trial < Trials; trial++)
            {
                Assert.That(HitChanceCalculator.Roll(0f), Is.False, "확률 0은 굴림과 무관하게 실패여야 한다.");
            }
        }

        [Test]
        public void ChancesOutsideTheRangeBehaveLikeTheEnds()
        {
            Assert.That(HitChanceCalculator.Roll(1.5f), Is.True);
            Assert.That(HitChanceCalculator.Roll(-0.5f), Is.False);
        }
    }
}
