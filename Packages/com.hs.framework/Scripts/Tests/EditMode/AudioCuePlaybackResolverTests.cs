using HS.Framework.Audio;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>사운드 큐의 클립 선택과 변주 계산 규칙을 검증한다.</summary>
    public sealed class AudioCuePlaybackResolverTests
    {
        [Test]
        public void SelectClipIndexReturnsMinusOneWhenNoClips()
        {
            var index = AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 0, -1, 0.5f);

            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void SelectClipIndexReturnsZeroWhenSingleClipRegardlessOfMode()
        {
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 1, 0, 0.9f), Is.EqualTo(0));
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Sequential, 1, 0, 0.9f), Is.EqualTo(0));
        }

        [Test]
        public void SequentialSelectionStartsAtZeroAndWrapsAround()
        {
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Sequential, 3, -1, 0f), Is.EqualTo(0));
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Sequential, 3, 0, 0f), Is.EqualTo(1));
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Sequential, 3, 2, 0f), Is.EqualTo(0));
        }

        [Test]
        public void RandomSelectionMapsRandomValueToIndex()
        {
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 4, -1, 0f), Is.EqualTo(0));
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 4, -1, 0.6f), Is.EqualTo(2));
        }

        [Test]
        public void RandomSelectionClampsRandomValueAtUpperBound()
        {
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 4, -1, 1f), Is.EqualTo(3));
            Assert.That(AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 4, -1, 2f), Is.EqualTo(3));
        }

        [Test]
        public void RandomSelectionAvoidsImmediateRepeat()
        {
            var index = AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 3, 1, 0.5f);

            Assert.That(index, Is.EqualTo(2));
        }

        [Test]
        public void RandomSelectionRepeatAvoidanceWrapsToFirstIndex()
        {
            var index = AudioCuePlaybackResolver.SelectClipIndex(
                AudioCueClipSelectionMode.Random, 3, 2, 1f);

            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void ResolveInRangeInterpolatesBetweenBounds()
        {
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.2f, 0.8f, 0f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.2f, 0.8f, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.2f, 0.8f, 1f), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void ResolveInRangeSwapsInvertedBounds()
        {
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.8f, 0.2f, 0f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.8f, 0.2f, 1f), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void ResolveInRangeClampsRandomValueOutsideUnitRange()
        {
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.2f, 0.8f, -1f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(AudioCuePlaybackResolver.ResolveInRange(0.2f, 0.8f, 2f), Is.EqualTo(0.8f).Within(1e-5f));
        }
    }
}
