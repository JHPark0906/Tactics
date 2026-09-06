using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>조건이 자식 실행을 허용하거나 차단하는 데코레이터 규칙을 검증한다.</summary>
    public sealed class ConditionDecoratorTests
    {
        [Test]
        public void APassingConditionLetsTheChildRunAndPassesItsResultUp()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new ConditionBehaviour(_ => true));
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void ABlockingConditionDoesNotRunTheChildAtAll()
        {
            var tree = new BehaviourTreeInstance();
            var ran = new List<string>();
            var root = tree.SetRoot(new ConditionBehaviour(_ => false));
            tree.AddChild(root, new ActionBehaviour(_ =>
            {
                ran.Add("child");
                return BehaviourStatus.Success;
            }));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(ran, Is.Empty,
                "막는다는 것은 아래를 시작조차 않는다는 뜻이다. 실행한 뒤 결과만 바꾸는 것과 다르다.");
        }

        [Test]
        public void ADecoratorWithNothingUnderItFails()
        {
            var tree = new BehaviourTreeInstance();
            tree.SetRoot(new ConditionBehaviour(_ => true));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure),
                "감쌀 것이 없는 상태가 조용히 성공으로 읽히면 트리가 왜 그렇게 도는지 알 수 없다.");
        }

        [Test]
        public void AContextValueConditionPassesOnlyWhileTheKeyHasAValue()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new ContextValueConditionBehaviour("target"));
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));
            var context = new BehaviourContext();

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            context.SetValue("target", 1);

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void AContextValueConditionCanAlsoRequireThatTheKeyIsEmpty()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new ContextValueConditionBehaviour("target", requiresValue: false));
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));
            var context = new BehaviourContext();

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));

            context.SetValue("target", 1);

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void AConditionSitsAboveWhatItGuardsInsteadOfBesideIt()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            var guarded = tree.AddChild(root, new ConditionBehaviour(_ => false));
            tree.AddChild(guarded, new ActionBehaviour(_ => BehaviourStatus.Success));
            var after = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(root.Children.Count, Is.EqualTo(2));
            Assert.That(guarded.Children.Count, Is.EqualTo(1),
                "무엇을 막는지가 트리 모양에 드러난다.");
            Assert.That(after, Is.Not.Null);
        }
    }
}
