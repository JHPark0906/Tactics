using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>문맥의 값을 견주는 두 조건이 문을 어떻게 여닫고, 값이 바뀌면 어떻게 끊는지 고정한다.</summary>
    public sealed class ContextComparisonConditionTests
    {
        [Test]
        public void EqualValuesOpenTheGateAndDifferentValuesCloseIt()
        {
            var context = new BehaviourContext();
            context.SetValue("a", 1);
            context.SetValue("b", 1);

            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b"), context), Is.EqualTo(BehaviourStatus.Success));

            context.SetValue("b", 2);
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b"), context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.NotEqual), context),
                Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void NumbersOfDifferentTypesCompareByValue()
        {
            var context = new BehaviourContext();
            context.SetValue("a", 1);
            context.SetValue("b", 1f);

            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b"), context), Is.EqualTo(BehaviourStatus.Success),
                "정수와 실수라도 수는 값으로 견준다.");
        }

        [Test]
        public void OrderingComparesNumbers()
        {
            var context = new BehaviourContext();
            context.SetValue("a", 1);
            context.SetValue("b", 2);

            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.Less), context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.Greater), context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.LessOrEqual), context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.GreaterOrEqual), context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void OrderingOnValuesThatAreNotNumbersIsNeverTrue()
        {
            var context = new BehaviourContext();
            context.SetValue("a", "x");
            context.SetValue("b", "y");

            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.Less), context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b", ContextComparison.NotEqual), context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void TwoMissingValuesCountAsEqual()
        {
            Assert.That(Gate(new CompareContextValuesBehaviour("a", "b"), new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void AChangeToEitherKeyStopsTheRunningBranch()
        {
            foreach (var changedKey in new[] { "a", "b" })
            {
                var context = new BehaviourContext();
                context.SetValue("a", 1);
                context.SetValue("b", 1);
                var tree = new BehaviourTreeInstance();
                var root = tree.SetRoot(new SelectorBehaviour());
                var guard = tree.AddChild(root,
                    new CompareContextValuesBehaviour("a", "b", ContextComparison.Equal, BehaviourAbortScope.Self));
                var running = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));

                tree.Tick(context);
                Assert.That(tree.IsRunning(running), Is.True);

                context.SetValue(changedKey, 2);
                tree.Tick(context);

                Assert.That(tree.IsRunning(running), Is.False, $"'{changedKey}'가 바뀌면 그 아래가 끊겨야 한다.");
            }
        }

        [Test]
        public void ANumberConditionComparesAgainstItsConstant()
        {
            var context = new BehaviourContext();
            context.SetValue("hp", 5);

            Assert.That(Gate(new ContextNumberConditionBehaviour("hp", ContextComparison.Greater, 3f), context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(Gate(new ContextNumberConditionBehaviour("hp", ContextComparison.Less, 3f), context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(Gate(new ContextNumberConditionBehaviour("hp", ContextComparison.Equal, 5f), context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void ANumberConditionIsClosedWhenTheValueIsMissingOrNotANumber()
        {
            var context = new BehaviourContext();

            Assert.That(Gate(new ContextNumberConditionBehaviour("hp", ContextComparison.Less, 3f), context), Is.EqualTo(BehaviourStatus.Failure));

            context.SetValue("hp", "many");
            Assert.That(Gate(new ContextNumberConditionBehaviour("hp", ContextComparison.Less, 3f), context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void ANumberConditionWithLowerPriorityTakesTheTurnBack()
        {
            var context = new BehaviourContext();
            context.SetValue("hp", 100);
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var guard = tree.AddChild(root,
                new ContextNumberConditionBehaviour("hp", ContextComparison.Less, 30f, BehaviourAbortScope.LowerPriority));
            var guarded = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));
            var fallback = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            Assert.That(tree.IsRunning(fallback), Is.True);

            context.SetValue("hp", 10);
            tree.Tick(context);

            Assert.That(tree.IsRunning(guarded), Is.True, "값이 조건에 들어오면 앞엣 자리가 차례를 되찾는다.");
        }

        [Test]
        public void TheDefinitionsNeedKeysButNotAnOwner()
        {
            var build = new BehaviourBuildContext(null, new BehaviourContext());

            Assert.That(new CompareContextValuesDefinition().CreateBehaviour(build), Is.Null);
            Assert.That(new ContextNumberConditionDefinition().CreateBehaviour(build), Is.Null);

            var compare = Configure(new CompareContextValuesDefinition(), ("leftKey", "a"), ("rightKey", "b"));
            Assert.That(compare.CreateBehaviour(build), Is.TypeOf<CompareContextValuesBehaviour>());
            Assert.That(compare.DisplayName, Is.Not.Empty);

            var number = Configure(new ContextNumberConditionDefinition(), ("key", "hp"));
            Assert.That(number.CreateBehaviour(build), Is.TypeOf<ContextNumberConditionBehaviour>());
            Assert.That(number.DisplayName, Is.Not.Empty);
        }

        /// <summary>조건을 뿌리로, 성공하는 잎을 아래에 두고 한 번 돌려 문이 열렸는지 본다.</summary>
        private static BehaviourStatus Gate(IBehaviour condition, IBehaviourContext context)
        {
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(condition);
            tree.AddChild(gate, new ActionBehaviour(_ => BehaviourStatus.Success));
            return tree.Tick(context);
        }

        private static TDefinition Configure<TDefinition>(TDefinition definition, params (string Field, object Value)[] values)
            where TDefinition : BehaviourNodeDefinition
        {
            foreach (var (field, value) in values)
            {
                var info = typeof(TDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(info, Is.Not.Null, $"{typeof(TDefinition).Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
                info.SetValue(definition, value);
            }

            return definition;
        }
    }
}
