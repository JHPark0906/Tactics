using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Tests.Support;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>문맥에 적힌 시간만큼 기다리는 잎을 고정한다.</summary>
    public sealed class WaitContextTimeBehaviourTests
    {
        private const string Key = "wait.seconds";

        [Test]
        public void WaitsForTheSecondsStoredInTheContext()
        {
            var now = 0f;
            var context = new BehaviourContext();
            context.SetValue(Key, 1.5f);
            var wait = new WaitContextTimeBehaviour(Key, () => now);

            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            now = 1f;
            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            now = 1.5f;
            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void AnIntegerIsReadAsSecondsToo()
        {
            var now = 0f;
            var context = new BehaviourContext();
            context.SetValue(Key, 2);
            var wait = new WaitContextTimeBehaviour(Key, () => now);

            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            now = 2f;
            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void FailsWhenTheContextHasNoNumber()
        {
            var context = new BehaviourContext();

            Assert.That(new WaitContextTimeBehaviour(Key, () => 0f).Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            context.SetValue(Key, "soon");
            Assert.That(new WaitContextTimeBehaviour(Key, () => 0f).Tick(context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void TheDurationIsReadOnceWhenTheWaitStarts()
        {
            var now = 0f;
            var context = new BehaviourContext();
            context.SetValue(Key, 1f);
            var wait = new WaitContextTimeBehaviour(Key, () => now);

            wait.Tick(context);
            context.SetValue(Key, 10f);
            now = 1f;

            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Success), "기다리는 중에 값이 바뀌어도 처음 읽은 시간만큼만 기다린다.");
        }

        [Test]
        public void ResettingStartsTheWaitOver()
        {
            var now = 0f;
            var context = new BehaviourContext();
            context.SetValue(Key, 1f);
            var wait = new WaitContextTimeBehaviour(Key, () => now);

            wait.Tick(context);
            wait.Reset();
            now = 1f;

            Assert.That(wait.Tick(context), Is.EqualTo(BehaviourStatus.Running));
        }

        [Test]
        public void TheDefinitionNeedsAKeyButNotAnOwner()
        {
            var build = new BehaviourBuildContext(null, new BehaviourContext());

            Assert.That(new WaitContextTimeDefinition().CreateBehaviour(build), Is.Null);

            var definition = new WaitContextTimeDefinition();
            var info = typeof(WaitContextTimeDefinition).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "WaitContextTimeDefinition에 'key' 필드가 없다.");
            info.SetValue(definition, Key);

            Assert.That(definition.CreateBehaviour(build), Is.TypeOf<WaitContextTimeBehaviour>());
            Assert.That(definition.DisplayName, Is.Not.Empty);
        }
    }
}
