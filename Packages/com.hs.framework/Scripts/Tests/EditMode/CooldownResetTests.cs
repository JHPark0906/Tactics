using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>쉬는 시각이 되돌리기에 지워지지 않는 것을 고정한다.</summary>
    /// <remarks>
    /// 끝없이 되풀이하는 자리는 아래가 끝날 때마다 아래를 되돌린다. 쉬는 자리가 되돌려질 때 쉬는
    /// 시각을 함께 지우면 그 아래에서는 쉬는 시간이 언제나 0이 된다. 쉬는 시각은 이 자리의 진행이
    /// 아니라 아래가 끝났다는 결과이므로 되돌리기의 대상이 아니다.
    /// </remarks>
    public sealed class CooldownResetTests
    {
        [Test]
        public void ACooldownUnderAnInfiniteRepeaterStillWaitsOutItsCooldown()
        {
            var now = 0f;
            var ran = 0;
            var tree = new BehaviourTreeInstance();
            var repeater = tree.SetRoot(new RepeaterBehaviour(RepeaterBehaviour.InfiniteRepeats));
            var cooldown = tree.AddChild(repeater, new CooldownBehaviour(1f, () => now));
            tree.AddChild(cooldown, new ActionBehaviour(_ =>
            {
                ran++;
                return BehaviourStatus.Success;
            }));
            var context = new BehaviourContext();

            tree.Tick(context);
            Assert.That(ran, Is.EqualTo(1));

            tree.Tick(context);
            now = 0.5f;
            tree.Tick(context);
            Assert.That(ran, Is.EqualTo(1), "되풀이 자리가 되돌려도 쉬는 시간은 그대로 흘러야 한다.");

            now = 1f;
            tree.Tick(context);
            Assert.That(ran, Is.EqualTo(2));
        }

        [Test]
        public void ResettingTheTreeDoesNotClearACooldown()
        {
            var now = 0f;
            var ran = 0;
            var tree = new BehaviourTreeInstance();
            var cooldown = tree.SetRoot(new CooldownBehaviour(1f, () => now));
            tree.AddChild(cooldown, new ActionBehaviour(_ =>
            {
                ran++;
                return BehaviourStatus.Success;
            }));
            var context = new BehaviourContext();

            tree.Tick(context);
            tree.Reset();
            now = 0.5f;

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(ran, Is.EqualTo(1), "가로채여 되돌려진 뒤에도 쉬는 중이면 아래를 다시 돌리지 않는다.");
        }

        [Test]
        public void ACooldownStillOpensOnceTheTimeHasPassed()
        {
            var now = 0f;
            var ran = 0;
            var tree = new BehaviourTreeInstance();
            var cooldown = tree.SetRoot(new CooldownBehaviour(1f, () => now));
            tree.AddChild(cooldown, new ActionBehaviour(_ =>
            {
                ran++;
                return BehaviourStatus.Success;
            }));
            var context = new BehaviourContext();

            tree.Tick(context);
            tree.Reset();
            now = 1f;

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(ran, Is.EqualTo(2));
        }
    }
}
