using System;
using System.Collections.Generic;
using HS.Framework.Foundation.Input;
using HS.Framework.Runtime;
using HS.Framework.UI.Windows;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>모달 창이 열린 동안 게임플레이 입력이 차단되고, 닫히면 복원되는지 검증한다.</summary>
    public sealed class UiWindowModalInputBlockTests
    {
        private readonly List<GameObject> _createdGameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _createdGameObjects)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            _createdGameObjects.Clear();
        }

        [Test]
        public void ModalWindowBlocksInputWhileOpenAndRestoresItOnClose()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var modal = CreateWindow(isModal: true);

            manager.OpenWindow(modal);

            Assert.That(inputController.IsInputEnabled, Is.False);
            Assert.That(inputController.LiveBlockCount, Is.EqualTo(1));

            manager.CloseWindow(modal);

            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void NonModalWindowDoesNotBlockInput()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var window = CreateWindow(isModal: false);

            manager.OpenWindow(window);

            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void OverlappingModalWindowsHoldASingleBlockUntilAllAreClosed()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var firstModal = CreateWindow(isModal: true);
            var secondModal = CreateWindow(isModal: true);

            manager.OpenWindow(firstModal);
            manager.OpenWindow(secondModal);

            Assert.That(inputController.LiveBlockCount, Is.EqualTo(1), "모달이 겹쳐도 토큰은 하나만 보유해야 한다.");

            manager.CloseWindow(secondModal);
            Assert.That(inputController.IsInputEnabled, Is.False, "아직 남은 모달이 있으면 차단이 유지되어야 한다.");

            manager.CloseWindow(firstModal);
            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void InjectingControllerWhileModalIsOpenAcquiresBlockImmediately()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(null);
            var modal = CreateWindow(isModal: true);

            manager.OpenWindow(modal);
            Assert.That(inputController.LiveBlockCount, Is.Zero);

            manager.InjectInputStateController(inputController);

            Assert.That(inputController.IsInputEnabled, Is.False);
            Assert.That(inputController.LiveBlockCount, Is.EqualTo(1));
        }

        [Test]
        public void DestroyingManagerReleasesInputBlock()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var modal = CreateWindow(isModal: true);
            manager.OpenWindow(modal);
            Assert.That(inputController.IsInputEnabled, Is.False);

            MonoBehaviourLifecycle.InvokeOnDestroy(manager);

            Assert.That(inputController.IsInputEnabled, Is.True);
            Assert.That(inputController.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void DisablingManagerReleasesBlockAndReenablingRestoresIt()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var modal = CreateWindow(isModal: true);
            manager.OpenWindow(modal);

            MonoBehaviourLifecycle.InvokeOnDisable(manager);
            Assert.That(inputController.IsInputEnabled, Is.True, "관리자가 멈추면 되돌릴 주체가 없으므로 차단을 해제해야 한다.");

            MonoBehaviourLifecycle.InvokeOnEnable(manager);
            Assert.That(inputController.IsInputEnabled, Is.False, "다시 활성화되면 열린 모달에 맞춰 차단을 재획득해야 한다.");
        }

        [Test]
        public void BlockedInputStaysBlockedWhileAnotherOwnerHoldsItsOwnToken()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var modal = CreateWindow(isModal: true);

            using var externalBlock = inputController.AcquireInputBlock();
            manager.OpenWindow(modal);
            manager.CloseWindow(modal);

            Assert.That(inputController.IsInputEnabled, Is.False, "다른 주체의 토큰이 살아 있으면 차단이 유지되어야 한다.");
            Assert.That(inputController.LiveBlockCount, Is.EqualTo(1));
        }

        [Test]
        public void ModalWindowKeepsItsOwnUiInputAlive()
        {
            var inputController = new FakeInputStateController(true);
            var manager = CreateManager(inputController);
            var modal = CreateWindow(isModal: true);

            manager.OpenWindow(modal);

            Assert.That(inputController.IsInputEnabled, Is.False, "모달 아래 게임플레이는 멈춰야 한다.");
            Assert.That(
                inputController.IsUiInputEnabled,
                Is.True,
                "모달이 자기 클릭과 취소 입력을 죽이면 확인 대화상자를 누를 수 없게 된다.");
            Assert.That(inputController.UiBlockCount, Is.Zero, "모달은 UI 범위를 막아서는 안 된다.");
            Assert.That(inputController.GameplayBlockCount, Is.EqualTo(1));
        }

        private UiWindowManager CreateManager(IInputStateController inputStateController)
        {
            var gameObject = new GameObject(nameof(UiWindowManager));
            _createdGameObjects.Add(gameObject);
            var manager = gameObject.AddComponent<UiWindowManager>();
            if (inputStateController != null)
            {
                manager.InjectInputStateController(inputStateController);
            }

            return manager;
        }

        private TestWindow CreateWindow(bool isModal)
        {
            var gameObject = new GameObject(nameof(TestWindow));
            _createdGameObjects.Add(gameObject);
            var window = gameObject.AddComponent<TestWindow>();
            window.ModalOverride = isModal;
            return window;
        }

        /// <summary>모달 여부를 코드로 지정하는 테스트용 창이다.</summary>
        private sealed class TestWindow : UiWindowBase
        {
            public bool ModalOverride { get; set; }

            public override bool IsModal => ModalOverride;
        }

    }
}
