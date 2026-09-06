using System.Collections.Generic;
using System.Reflection;
using HS.Framework.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>상호작용 옵션의 컴포넌트 구성 검증을 확인한다.</summary>
    public sealed class InteractionOptionTests
    {
        [Test]
        public void ValidConfigurationEvaluatesAndExecutes()
        {
            var gameObject = new GameObject("InteractionOptionTest");
            try
            {
                var requirement = gameObject.AddComponent<TestRequirement>();
                var action = gameObject.AddComponent<TestAction>();
                var option = new InteractionOption();
                SetBehaviours(option, "requirements", requirement);
                SetBehaviours(option, "actions", action);

                Assert.That(option.TryValidateConfiguration(out var error), Is.True, error);
                Assert.That(option.Evaluate(new TestInteractor(gameObject)).IsAvailable, Is.True);

                option.Execute(new TestInteractor(gameObject));

                Assert.That(action.ExecutionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InvalidRequirementComponentIsRejectedBeforeInteraction()
        {
            var gameObject = new GameObject("InteractionOptionTest");
            try
            {
                var option = new InteractionOption();
                SetBehaviours(option, "requirements", gameObject.AddComponent<InvalidBehaviour>());

                Assert.That(option.TryValidateConfiguration(out var error), Is.False);
                Assert.That(error, Does.Contain("IInteractionRequirement"));
                Assert.That(option.Evaluate(new TestInteractor(gameObject)).IsAvailable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InvalidActionComponentIsRejectedBeforeExecution()
        {
            var gameObject = new GameObject("InteractionOptionTest");
            try
            {
                var option = new InteractionOption();
                SetBehaviours(option, "actions", gameObject.AddComponent<InvalidBehaviour>());

                Assert.That(option.TryValidateConfiguration(out var error), Is.False);
                Assert.That(error, Does.Contain("IInteractionAction"));

                Assert.DoesNotThrow(() => option.Execute(new TestInteractor(gameObject)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetBehaviours(InteractionOption option, string fieldName, params MonoBehaviour[] behaviours)
        {
            var field = typeof(InteractionOption).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(option, new List<MonoBehaviour>(behaviours));
        }

        private sealed class TestInteractor : IInteractor
        {
            public TestInteractor(GameObject gameObject)
            {
                GameObject = gameObject;
            }

            public GameObject GameObject { get; }
        }

        private sealed class TestRequirement : MonoBehaviour, IInteractionRequirement
        {
            public InteractionAvailability Evaluate(IInteractor interactor)
            {
                return new InteractionAvailability(true);
            }
        }

        private sealed class TestAction : MonoBehaviour, IInteractionAction
        {
            public int ExecutionCount { get; private set; }

            public void Execute(IInteractor interactor)
            {
                ExecutionCount++;
            }
        }

        private sealed class InvalidBehaviour : MonoBehaviour
        {
        }
    }
}
