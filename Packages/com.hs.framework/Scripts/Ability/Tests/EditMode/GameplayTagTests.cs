using System;
using HS.Framework.Ability.Tags;
using NUnit.Framework;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>게임플레이 태그의 형식 검증과 계층 일치 규칙을 검증한다.</summary>
    public sealed class GameplayTagTests
    {
        [Test]
        public void ValidNameIsParsed()
        {
            Assert.That(GameplayTag.TryParse("State.Slowed", out var tag), Is.True);
            Assert.That(tag.IsValid, Is.True);
            Assert.That(tag.Name, Is.EqualTo("State.Slowed"));
            Assert.That(tag.Depth, Is.EqualTo(2));
        }

        [Test]
        public void SurroundingWhitespaceIsTrimmed()
        {
            Assert.That(GameplayTag.TryParse("  State.Slowed  ", out var tag), Is.True);
            Assert.That(tag.Name, Is.EqualTo("State.Slowed"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(".State")]
        [TestCase("State.")]
        [TestCase("State..Slowed")]
        [TestCase(".")]
        [TestCase("State Slowed")]
        [TestCase("State. Slowed")]
        public void MalformedNameIsRejectedRatherThanRepaired(string malformedName)
        {
            Assert.That(GameplayTag.TryParse(malformedName, out var tag), Is.False);
            Assert.That(tag.IsValid, Is.False);
        }

        [Test]
        public void NullNameIsRejected()
        {
            Assert.That(GameplayTag.TryParse(null, out _), Is.False);
        }

        [Test]
        public void ParseThrowsOnMalformedNameSoTyposAreLoud()
        {
            Assert.Throws<ArgumentException>(() => GameplayTag.Parse("State..Slowed"));
        }

        [Test]
        public void NoneIsInvalidAndMatchesNothing()
        {
            var none = GameplayTag.None;
            var tag = GameplayTag.Parse("State");

            Assert.That(none.IsValid, Is.False);
            Assert.That(none.Depth, Is.Zero);
            Assert.That(none.Matches(tag), Is.False);
            Assert.That(tag.Matches(none), Is.False);
        }

        [Test]
        public void ChildMatchesItsAncestorsButNotTheOtherWayAround()
        {
            var child = GameplayTag.Parse("State.Movement.Slowed");
            var parent = GameplayTag.Parse("State.Movement");
            var root = GameplayTag.Parse("State");

            Assert.That(child.Matches(parent), Is.True);
            Assert.That(child.Matches(root), Is.True);
            Assert.That(child.Matches(child), Is.True);

            Assert.That(parent.Matches(child), Is.False, "상위 태그는 하위 태그에 일치하지 않아야 한다.");
            Assert.That(root.Matches(child), Is.False);
        }

        [Test]
        public void PrefixOverlapDoesNotCountAsHierarchyMatch()
        {
            var slowed = GameplayTag.Parse("State.Slowed");
            var slow = GameplayTag.Parse("State.Slow");

            Assert.That(slowed.Matches(slow), Is.False, "단계 경계에서 끊기지 않는 접두사는 일치가 아니다.");
        }

        [Test]
        public void SiblingsDoNotMatch()
        {
            var stunned = GameplayTag.Parse("State.Stunned");
            var slowed = GameplayTag.Parse("State.Slowed");

            Assert.That(stunned.Matches(slowed), Is.False);
            Assert.That(slowed.Matches(stunned), Is.False);
        }

        [Test]
        public void MatchesExactIgnoresHierarchy()
        {
            var child = GameplayTag.Parse("State.Slowed");
            var root = GameplayTag.Parse("State");

            Assert.That(child.MatchesExact(root), Is.False);
            Assert.That(child.MatchesExact(GameplayTag.Parse("State.Slowed")), Is.True);
        }

        [Test]
        public void ComparisonIgnoresCaseSoOneTagIsNotSpelledTwoWays()
        {
            var upper = GameplayTag.Parse("State.Slowed");
            var lower = GameplayTag.Parse("state.slowed");

            Assert.That(upper, Is.EqualTo(lower));
            Assert.That(upper == lower, Is.True);
            Assert.That(upper.GetHashCode(), Is.EqualTo(lower.GetHashCode()));
            Assert.That(upper.Matches(GameplayTag.Parse("STATE")), Is.True);
        }

        [Test]
        public void AuthoredCaseIsPreservedForDisplay()
        {
            var tag = GameplayTag.Parse("State.Slowed");

            Assert.That(tag.Name, Is.EqualTo("State.Slowed"));
            Assert.That(tag.ToString(), Is.EqualTo("State.Slowed"));
        }

        [Test]
        public void ParentIsResolvedOneLevelUp()
        {
            var tag = GameplayTag.Parse("A.B.C");

            Assert.That(tag.TryGetParent(out var parent), Is.True);
            Assert.That(parent.Name, Is.EqualTo("A.B"));
            Assert.That(parent.TryGetParent(out var grandParent), Is.True);
            Assert.That(grandParent.Name, Is.EqualTo("A"));
            Assert.That(grandParent.TryGetParent(out _), Is.False, "최상위 태그에는 상위가 없다.");
        }

        [Test]
        public void EqualTagsShareHashCodeSoTheyWorkAsDictionaryKeys()
        {
            var lookup = new System.Collections.Generic.Dictionary<GameplayTag, int>
            {
                [GameplayTag.Parse("Cooldown.Attack")] = 3
            };

            Assert.That(lookup.TryGetValue(GameplayTag.Parse("Cooldown.Attack"), out var value), Is.True);
            Assert.That(value, Is.EqualTo(3));
        }
    }
}
