using System.Collections.Generic;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>태그 컨테이너의 참조 계수와 계층 질의, 변화 알림을 검증한다.</summary>
    public sealed class GameplayTagContainerTests
    {
        /// <summary>테스트에서 반복해 쓰는 태그 이름이다.</summary>
        private const string SlowedName = "State.Slowed";

        [Test]
        public void AddingATagGrantsIt()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);

            Assert.That(container.AddTag(slowed), Is.EqualTo(1));
            Assert.That(container.HasTagExact(slowed), Is.True);
            Assert.That(container.GetCount(slowed), Is.EqualTo(1));
            Assert.That(container.DistinctTagCount, Is.EqualTo(1));
        }

        [Test]
        public void TagSurvivesUntilEveryGrantIsTakenBack()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);
            container.AddTag(slowed);
            container.AddTag(slowed);

            Assert.That(container.GetCount(slowed), Is.EqualTo(2));

            Assert.That(container.RemoveTag(slowed), Is.EqualTo(1));
            Assert.That(
                container.HasTagExact(slowed),
                Is.True,
                "두 번 부여된 태그는 한 번 회수해도 남아 있어야 한다.");

            Assert.That(container.RemoveTag(slowed), Is.Zero);
            Assert.That(container.HasTagExact(slowed), Is.False);
        }

        [Test]
        public void GrantCountCanBeAddedAndRemovedInBulk()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);

            Assert.That(container.AddTag(slowed, 3), Is.EqualTo(3));
            Assert.That(container.RemoveTag(slowed, 2), Is.EqualTo(1));
            Assert.That(container.HasTagExact(slowed), Is.True);
            Assert.That(container.RemoveTag(slowed, 5), Is.Zero, "남은 계수보다 많이 회수해도 0에서 멈춘다.");
            Assert.That(container.HasTagExact(slowed), Is.False);
        }

        [Test]
        public void RemovingAnAbsentTagDoesNothing()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);
            var changeCount = 0;
            using var subscription = container.Changed.Subscribe(_ => changeCount++);

            Assert.That(container.RemoveTag(slowed), Is.Zero);
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void InvalidTagsAndNonPositiveCountsAreIgnored()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);

            Assert.That(container.AddTag(GameplayTag.None), Is.Zero);
            Assert.That(container.AddTag(slowed, 0), Is.Zero);
            Assert.That(container.AddTag(slowed, -1), Is.Zero);
            Assert.That(container.DistinctTagCount, Is.Zero);

            container.AddTag(slowed);
            Assert.That(container.RemoveTag(slowed, 0), Is.EqualTo(1));
            Assert.That(container.HasTagExact(slowed), Is.True);
        }

        [Test]
        public void RemoveCompletelyDropsEveryGrantAtOnce()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);
            container.AddTag(slowed, 3);

            Assert.That(container.RemoveTagCompletely(slowed), Is.True);
            Assert.That(container.HasTagExact(slowed), Is.False);
            Assert.That(container.RemoveTagCompletely(slowed), Is.False);
        }

        [Test]
        public void HierarchicalQueryFindsChildrenThroughTheParentName()
        {
            var container = new GameplayTagContainer();
            container.AddTag(GameplayTag.Parse("Cooldown.Attack"));

            Assert.That(container.HasTag(GameplayTag.Parse("Cooldown")), Is.True);
            Assert.That(container.HasTag(GameplayTag.Parse("Cooldown.Attack")), Is.True);
            Assert.That(
                container.HasTagExact(GameplayTag.Parse("Cooldown")),
                Is.False,
                "정확한 질의는 계층을 타고 올라가지 않는다.");
        }

        [Test]
        public void HierarchicalQueryDoesNotMatchDownwards()
        {
            var container = new GameplayTagContainer();
            container.AddTag(GameplayTag.Parse("Cooldown"));

            Assert.That(
                container.HasTag(GameplayTag.Parse("Cooldown.Attack")),
                Is.False,
                "상위 태그만 가진 컨테이너가 하위 태그를 가졌다고 답해서는 안 된다.");
        }

        [Test]
        public void HasAnyAndHasAllFollowHierarchy()
        {
            var container = new GameplayTagContainer();
            container.AddTag(GameplayTag.Parse("State.Slowed"));
            container.AddTag(GameplayTag.Parse("Cooldown.Attack"));
            var queried = new List<GameplayTag>
            {
                GameplayTag.Parse("State"),
                GameplayTag.Parse("Cooldown")
            };

            Assert.That(container.HasAny(queried), Is.True);
            Assert.That(container.HasAll(queried), Is.True);

            queried.Add(GameplayTag.Parse("Ability.Dash"));
            Assert.That(container.HasAny(queried), Is.True);
            Assert.That(container.HasAll(queried), Is.False);
        }

        [Test]
        public void HasAllOnAnEmptyQueryIsTrueAndHasAnyIsFalse()
        {
            var container = new GameplayTagContainer();
            var empty = new List<GameplayTag>();

            Assert.That(container.HasAll(empty), Is.True);
            Assert.That(container.HasAny(empty), Is.False);
            Assert.That(container.HasAll(null), Is.False);
            Assert.That(container.HasAny(null), Is.False);
        }

        [Test]
        public void ChangeIsAnnouncedOnlyWhenTheTagIsGainedOrLost()
        {
            var container = new GameplayTagContainer();
            var slowed = GameplayTag.Parse(SlowedName);
            var changes = new List<GameplayTagChange>();
            using var subscription = container.Changed.Subscribe(changes.Add);

            container.AddTag(slowed);
            container.AddTag(slowed);
            container.RemoveTag(slowed);
            container.RemoveTag(slowed);

            Assert.That(changes.Count, Is.EqualTo(2), "두 번째 부여와 첫 회수는 태그 보유 상태를 바꾸지 않는다.");
            Assert.That(changes[0].ChangeKind, Is.EqualTo(GameplayTagChangeKind.Gained));
            Assert.That(changes[0].Tag, Is.EqualTo(slowed));
            Assert.That(changes[0].Count, Is.EqualTo(1));
            Assert.That(changes[1].ChangeKind, Is.EqualTo(GameplayTagChangeKind.Lost));
            Assert.That(changes[1].Count, Is.Zero);
        }

        [Test]
        public void ClearDropsEveryTagAndAnnouncesEachLoss()
        {
            var container = new GameplayTagContainer();
            container.AddTag(GameplayTag.Parse("State.Slowed"), 2);
            container.AddTag(GameplayTag.Parse("Cooldown.Attack"));
            var lostCount = 0;
            using var subscription = container.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayTagChangeKind.Lost)
                {
                    lostCount++;
                }
            });

            container.Clear();

            Assert.That(container.DistinctTagCount, Is.Zero);
            Assert.That(lostCount, Is.EqualTo(2));
        }

        [Test]
        public void ClearingAnEmptyContainerAnnouncesNothing()
        {
            var container = new GameplayTagContainer();
            var changeCount = 0;
            using var subscription = container.Changed.Subscribe(_ => changeCount++);

            container.Clear();

            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void ChangedDoesNotExposeTheInternalSubject()
        {
            var container = new GameplayTagContainer();

            Assert.That(container.Changed, Is.Not.InstanceOf<R3.Subject<GameplayTagChange>>());
            Assert.That(container.Changed, Is.SameAs(container.Changed));
        }

        [Test]
        public void TagsWrittenInDifferentCaseShareOneGrantCount()
        {
            var container = new GameplayTagContainer();
            container.AddTag(GameplayTag.Parse("State.Slowed"));
            container.AddTag(GameplayTag.Parse("state.slowed"));

            Assert.That(container.DistinctTagCount, Is.EqualTo(1));
            Assert.That(container.GetCount(GameplayTag.Parse("STATE.SLOWED")), Is.EqualTo(2));
        }
    }
}
