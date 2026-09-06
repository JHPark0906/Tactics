using HS.Framework.Runtime;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>일시정지 서비스의 시간 배율 저장·복원과 입력 차단 토큰 규칙을 검증한다.</summary>
    public sealed class PauseServiceTests
    {
        private float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _originalTimeScale;
        }

        [Test]
        public void PauseSavesStateBlocksInputAndPublishesEvent()
        {
            Time.timeScale = 0.5f;
            var inputController = new FakeInputStateController(true);
            var pausePublisher = new TestPublisher<PauseChangedEvent>();
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            using var pauseService = new PauseService(inputController, pausePublisher, sceneStarted);

            pauseService.Pause();

            Assert.That(pauseService.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(inputController.IsInputEnabled, Is.False);
            Assert.That(pausePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(pausePublisher.Published[0].IsPaused, Is.True);
        }

        [Test]
        public void ResumeRestoresSavedTimeScaleAndInputState()
        {
            Time.timeScale = 0.5f;
            var inputController = new FakeInputStateController(true);
            var pausePublisher = new TestPublisher<PauseChangedEvent>();
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            using var pauseService = new PauseService(inputController, pausePublisher, sceneStarted);

            pauseService.Pause();
            pauseService.Resume();

            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(pausePublisher.Published, Has.Count.EqualTo(2));
            Assert.That(pausePublisher.Published[1].IsPaused, Is.False);
        }

        [Test]
        public void ResumeKeepsInputDisabledWhenBaseStateWasDisabled()
        {
            var inputController = new FakeInputStateController(false);
            using var pauseService = new PauseService(inputController, new TestPublisher<PauseChangedEvent>(), new TestSubscriber<SceneLoadStartedEvent>());

            pauseService.Pause();
            pauseService.Resume();

            Assert.That(inputController.IsInputEnabled, Is.False);
        }

        [Test]
        public void DuplicatePauseAndResumeCallsAreIgnored()
        {
            Time.timeScale = 1f;
            var inputController = new FakeInputStateController(true);
            var pausePublisher = new TestPublisher<PauseChangedEvent>();
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            using var pauseService = new PauseService(inputController, pausePublisher, sceneStarted);

            pauseService.Resume();
            pauseService.Pause();
            pauseService.Pause();

            Assert.That(pausePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(inputController.LiveBlockCount, Is.EqualTo(1));

            pauseService.Resume();
            pauseService.Resume();

            Assert.That(pausePublisher.Published, Has.Count.EqualTo(2));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void SceneLoadStartedAutoResumesPausedState()
        {
            Time.timeScale = 0.75f;
            var inputController = new FakeInputStateController(true);
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            using var pauseService = new PauseService(inputController, new TestPublisher<PauseChangedEvent>(), sceneStarted);
            pauseService.Initialize();

            pauseService.Pause();
            sceneStarted.Publish(new SceneLoadStartedEvent(SceneReference.Create("Assets/Scenes/Level1.unity"), true));

            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(inputController.IsInputEnabled, Is.True);
        }

        [Test]
        public void DisposeRestoresSavedStateWithoutPublishingEvent()
        {
            Time.timeScale = 0.5f;
            var inputController = new FakeInputStateController(true);
            var pausePublisher = new TestPublisher<PauseChangedEvent>();
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            var pauseService = new PauseService(inputController, pausePublisher, sceneStarted);

            pauseService.Pause();
            pauseService.Dispose();

            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(inputController.LiveBlockCount, Is.Zero);
            Assert.That(pausePublisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void PauseDuringSceneTransitionKeepsInputConsistentAfterResume()
        {
            Time.timeScale = 1f;
            var inputController = new FakeInputStateController(true);
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            var sceneCompleted = new TestSubscriber<SceneLoadCompletedEvent>();
            var sceneFailed = new TestSubscriber<SceneLoadFailedEvent>();
            using var pauseService = new PauseService(inputController, new TestPublisher<PauseChangedEvent>(), sceneStarted);
            using var sceneInputGate = new SceneInputGate(sceneStarted, sceneCompleted, sceneFailed, inputController);
            pauseService.Initialize();
            sceneInputGate.Initialize();
            var destination = SceneReference.Create("Assets/Scenes/Level1.unity");

            sceneStarted.Publish(new SceneLoadStartedEvent(destination, true));
            Assert.That(inputController.IsInputEnabled, Is.False);

            pauseService.Pause();
            Assert.That(inputController.IsInputEnabled, Is.False);

            sceneCompleted.Publish(new SceneLoadCompletedEvent(destination));
            Assert.That(inputController.IsInputEnabled, Is.False, "일시정지 중에는 전환이 끝나도 입력이 차단되어야 한다.");

            pauseService.Resume();
            Assert.That(inputController.IsInputEnabled, Is.True, "일시정지 해제 후 입력이 반드시 복원되어야 한다.");
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void PausedStateBeforeTransitionResolvesRegardlessOfSubscriptionOrder()
        {
            Time.timeScale = 1f;
            var inputController = new FakeInputStateController(true);
            var sceneStarted = new TestSubscriber<SceneLoadStartedEvent>();
            var sceneCompleted = new TestSubscriber<SceneLoadCompletedEvent>();
            var sceneFailed = new TestSubscriber<SceneLoadFailedEvent>();
            using var pauseService = new PauseService(inputController, new TestPublisher<PauseChangedEvent>(), sceneStarted);
            using var sceneInputGate = new SceneInputGate(sceneStarted, sceneCompleted, sceneFailed, inputController);

            // 입력 상태를 스냅샷으로 복원하는 방식이라면 상태 역전을 일으키는 순서(게이트가 먼저 구독)로 등록한다.
            sceneInputGate.Initialize();
            pauseService.Initialize();
            var destination = SceneReference.Create("Assets/Scenes/Level1.unity");

            pauseService.Pause();
            sceneStarted.Publish(new SceneLoadStartedEvent(destination, true));

            Assert.That(pauseService.IsPaused, Is.False);
            Assert.That(inputController.IsInputEnabled, Is.False);

            sceneCompleted.Publish(new SceneLoadCompletedEvent(destination));

            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void PauseKeepsUiInputAliveSoThePlayerCanUnpause()
        {
            var inputController = new FakeInputStateController(true);
            using var pauseService = new PauseService(inputController, new TestPublisher<PauseChangedEvent>(), new TestSubscriber<SceneLoadStartedEvent>());

            pauseService.Pause();

            Assert.That(inputController.IsInputEnabled, Is.False, "일시정지 중 게임플레이는 멈춰야 한다.");
            Assert.That(
                inputController.IsUiInputEnabled,
                Is.True,
                "UI까지 막으면 일시정지 메뉴로 일시정지를 풀 수 없게 된다.");
            Assert.That(inputController.UiBlockCount, Is.Zero);
        }

    }
}
