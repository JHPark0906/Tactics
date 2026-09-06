using System;
using HS.Framework.Runtime;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using MessagePipe;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>씬 전환 중 입력 차단 토큰의 획득과 해제를 검증한다.</summary>
    public sealed class SceneInputGateTests
    {
        [Test]
        public void SceneLoadKeepsInputDisabledAfterFailureWhenBaseStateWasDisabled()
        {
            var started = new TestSubscriber<SceneLoadStartedEvent>();
            var completed = new TestSubscriber<SceneLoadCompletedEvent>();
            var failed = new TestSubscriber<SceneLoadFailedEvent>();
            var inputState = new FakeInputStateController(false);
            var gate = new SceneInputGate(started, completed, failed, inputState);
            var destination = SceneReference.Create("Assets/Scenes/Test.unity");
            gate.Initialize();

            started.Publish(new SceneLoadStartedEvent(destination, true));
            Assert.That(inputState.IsInputEnabled, Is.False);

            failed.Publish(new SceneLoadFailedEvent(destination, new InvalidOperationException()));
            Assert.That(inputState.IsInputEnabled, Is.False);
            Assert.That(inputState.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void SceneLoadRestoresEnabledInputAfterCompletion()
        {
            var started = new TestSubscriber<SceneLoadStartedEvent>();
            var completed = new TestSubscriber<SceneLoadCompletedEvent>();
            var failed = new TestSubscriber<SceneLoadFailedEvent>();
            var inputState = new FakeInputStateController(true);
            var gate = new SceneInputGate(started, completed, failed, inputState);
            var destination = SceneReference.Create("Assets/Scenes/Test.unity");
            gate.Initialize();

            started.Publish(new SceneLoadStartedEvent(destination, false));
            Assert.That(inputState.IsInputEnabled, Is.False);

            completed.Publish(new SceneLoadCompletedEvent(destination));
            Assert.That(inputState.IsInputEnabled, Is.True);
            Assert.That(inputState.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void DuplicateStartedEventsAcquireOnlyOneBlockToken()
        {
            var started = new TestSubscriber<SceneLoadStartedEvent>();
            var completed = new TestSubscriber<SceneLoadCompletedEvent>();
            var failed = new TestSubscriber<SceneLoadFailedEvent>();
            var inputState = new FakeInputStateController(true);
            var gate = new SceneInputGate(started, completed, failed, inputState);
            var destination = SceneReference.Create("Assets/Scenes/Test.unity");
            gate.Initialize();

            started.Publish(new SceneLoadStartedEvent(destination, true));
            started.Publish(new SceneLoadStartedEvent(destination, true));
            Assert.That(inputState.LiveBlockCount, Is.EqualTo(1));

            completed.Publish(new SceneLoadCompletedEvent(destination));
            Assert.That(inputState.IsInputEnabled, Is.True);
            Assert.That(inputState.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void DisposeDuringTransitionReleasesHeldBlockToken()
        {
            var started = new TestSubscriber<SceneLoadStartedEvent>();
            var completed = new TestSubscriber<SceneLoadCompletedEvent>();
            var failed = new TestSubscriber<SceneLoadFailedEvent>();
            var inputState = new FakeInputStateController(true);
            var gate = new SceneInputGate(started, completed, failed, inputState);
            var destination = SceneReference.Create("Assets/Scenes/Test.unity");
            gate.Initialize();

            started.Publish(new SceneLoadStartedEvent(destination, true));
            Assert.That(inputState.IsInputEnabled, Is.False);

            gate.Dispose();
            Assert.That(inputState.IsInputEnabled, Is.True);
            Assert.That(inputState.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void SceneTransitionBlocksUiInputAsWell()
        {
            var started = new TestSubscriber<SceneLoadStartedEvent>();
            var completed = new TestSubscriber<SceneLoadCompletedEvent>();
            var failed = new TestSubscriber<SceneLoadFailedEvent>();
            var inputState = new FakeInputStateController(true);
            var gate = new SceneInputGate(started, completed, failed, inputState);
            var destination = SceneReference.Create("Assets/Scenes/Test.unity");
            gate.Initialize();

            started.Publish(new SceneLoadStartedEvent(destination, true));

            Assert.That(inputState.IsInputEnabled, Is.False);
            Assert.That(
                inputState.IsUiInputEnabled,
                Is.False,
                "사라질 씬의 UI는 눌러도 유효하지 않으므로 전환 중에는 UI까지 막는다.");

            completed.Publish(new SceneLoadCompletedEvent(destination));

            Assert.That(inputState.IsInputEnabled, Is.True);
            Assert.That(inputState.IsUiInputEnabled, Is.True, "전환이 끝나면 UI가 반드시 살아나야 한다.");
        }

    }
}
