using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>문맥에 두는 쉬는 시각을 여러 자리가 나눠 쓰는 것을 고정한다.</summary>
    public sealed class ContextCooldownBehaviourTests
    {
        private const string Key = "cooldown.shout";

        [Test]
        public void TwoNodesSharingAKeyShareTheCooldown()
        {
            var now = 0f;
            var firstRuns = 0;
            var secondRuns = 0;
            var tree = new BehaviourTreeInstance();
            var sequence = tree.SetRoot(new SequenceBehaviour());
            var first = tree.AddChild(sequence, new ContextCooldownBehaviour(Key, 1f, timeProvider: () => now));
            tree.AddChild(first, new ActionBehaviour(_ =>
            {
                firstRuns++;
                return BehaviourStatus.Success;
            }));
            var second = tree.AddChild(sequence, new ContextCooldownBehaviour(Key, 1f, timeProvider: () => now));
            tree.AddChild(second, new ActionBehaviour(_ =>
            {
                secondRuns++;
                return BehaviourStatus.Success;
            }));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(firstRuns, Is.EqualTo(1));
            Assert.That(secondRuns, Is.EqualTo(0), "앞엣 자리가 끝나며 둔 쉬는 시각이 같은 키를 쓰는 뒤엣 자리를 막는다.");
        }

        [Test]
        public void TheGateOpensAgainOnceTheTimeHasPassed()
        {
            var now = 0f;
            var context = new BehaviourContext();
            var tree = new BehaviourTreeInstance();
            var cooldown = tree.SetRoot(new ContextCooldownBehaviour(Key, 1f, timeProvider: () => now));
            tree.AddChild(cooldown, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            now = 0.5f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            now = 1f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void AddingToTheExistingDurationStartsFromTheReadyTimeAlreadyInTheContext()
        {
            var now = 0f;
            var context = new BehaviourContext();
            var tree = new BehaviourTreeInstance();
            var cooldown = tree.SetRoot(new ContextCooldownBehaviour(Key, 1f, addsToExistingDuration: true, timeProvider: () => now));
            // 아래가 도는 사이 같은 키를 쓰는 다른 자리가 쉬는 시각을 두었다.
            tree.AddChild(cooldown, new ActionBehaviour(values =>
            {
                values.SetValue(Key, 5f);
                return BehaviourStatus.Success;
            }));

            tree.Tick(context);

            Assert.That(context.TryGetValue<float>(Key, out var readyTime), Is.True);
            Assert.That(readyTime, Is.EqualTo(6f), "남은 시간에 더하면 이미 있는 시각 뒤로 미룬다.");
        }

        [Test]
        public void WithoutAddingTheReadyTimeIsCountedFromNow()
        {
            var now = 0f;
            var context = new BehaviourContext();
            var tree = new BehaviourTreeInstance();
            var cooldown = tree.SetRoot(new ContextCooldownBehaviour(Key, 1f, addsToExistingDuration: false, timeProvider: () => now));
            tree.AddChild(cooldown, new ActionBehaviour(values =>
            {
                values.SetValue(Key, 5f);
                return BehaviourStatus.Success;
            }));

            tree.Tick(context);

            Assert.That(context.TryGetValue<float>(Key, out var readyTime), Is.True);
            Assert.That(readyTime, Is.EqualTo(1f), "더하지 않으면 지금부터 다시 센다.");
        }

        [Test]
        public void ResettingTheTreeLeavesTheSharedCooldownInTheContext()
        {
            var now = 0f;
            var context = new BehaviourContext();
            var tree = new BehaviourTreeInstance();
            var cooldown = tree.SetRoot(new ContextCooldownBehaviour(Key, 1f, timeProvider: () => now));
            tree.AddChild(cooldown, new ActionBehaviour(_ => BehaviourStatus.Success));

            tree.Tick(context);
            tree.Reset();
            now = 0.5f;

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "쉬는 시각은 문맥에 있으므로 되돌리기가 지우지 못한다.");
        }

        [Test]
        public void TheDefinitionNeedsAKeyButNotAnOwner()
        {
            var build = new BehaviourBuildContext(null, new BehaviourContext());

            Assert.That(new ContextCooldownDefinition().CreateBehaviour(build), Is.Null);

            var definition = new ContextCooldownDefinition();
            var info = typeof(ContextCooldownDefinition).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "ContextCooldownDefinition에 'key' 필드가 없다.");
            info.SetValue(definition, Key);

            Assert.That(definition.CreateBehaviour(build), Is.TypeOf<ContextCooldownBehaviour>());
            Assert.That(definition.DisplayName, Is.Not.Empty);
        }
    }
}
