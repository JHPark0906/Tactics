using System.Collections.Generic;
using HS.Framework.UI.Windows;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>확인 팝업의 재진입 규약과 콜백 통지 불변식을 검증한다.</summary>
    public sealed class ConfirmDialogTests
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
        public void ShowOpensTheDialogWithoutInvokingCallbacks()
        {
            var dialog = CreateDialog();
            var confirmCount = 0;
            var cancelCount = 0;

            dialog.Show("제목", "본문", () => confirmCount++, () => cancelCount++);

            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(confirmCount, Is.Zero);
            Assert.That(cancelCount, Is.Zero);
        }

        [Test]
        public void ClosingWithoutAButtonNotifiesCancelExactlyOnce()
        {
            var dialog = CreateDialog();
            var cancelCount = 0;
            dialog.Show("제목", "본문", onCancel: () => cancelCount++);

            dialog.Close();
            dialog.Close();

            Assert.That(cancelCount, Is.EqualTo(1));
        }

        [Test]
        public void ShowWhileOpenNotifiesThePreviousRequesterWithCancel()
        {
            var dialog = CreateDialog();
            var firstCancelCount = 0;
            var firstConfirmCount = 0;
            dialog.Show("첫 요청", "본문", () => firstConfirmCount++, () => firstCancelCount++);

            dialog.Show("두 번째 요청", "본문");

            Assert.That(firstCancelCount, Is.EqualTo(1), "교체당한 요청자는 취소를 통지받아야 한다.");
            Assert.That(firstConfirmCount, Is.Zero);
            Assert.That(dialog.IsOpen, Is.True);
        }

        [Test]
        public void ReplacedRequesterIsNotNotifiedAgainWhenTheDialogLaterCloses()
        {
            var dialog = CreateDialog();
            var firstCancelCount = 0;
            var secondCancelCount = 0;
            dialog.Show("첫 요청", "본문", onCancel: () => firstCancelCount++);

            dialog.Show("두 번째 요청", "본문", onCancel: () => secondCancelCount++);
            dialog.Close();

            Assert.That(firstCancelCount, Is.EqualTo(1), "이전 요청자는 교체 시점에만 통지받아야 한다.");
            Assert.That(secondCancelCount, Is.EqualTo(1), "새 요청자는 닫힘 시점에 통지받아야 한다.");
        }

        [Test]
        public void ShowAfterAResolvedCloseDoesNotNotifyTheEarlierRequesterAgain()
        {
            var dialog = CreateDialog();
            var firstCancelCount = 0;
            dialog.Show("첫 요청", "본문", onCancel: () => firstCancelCount++);
            dialog.Close();

            dialog.Show("두 번째 요청", "본문");

            Assert.That(firstCancelCount, Is.EqualTo(1));
        }

        [Test]
        public void ShowFromInsideAReplacedCancelCallbackKeepsTheOneNotificationRule()
        {
            var dialog = CreateDialog();
            var firstCancelCount = 0;
            var secondCancelCount = 0;
            var thirdCancelCount = 0;

            dialog.Show(
                "첫 요청",
                "본문",
                onCancel: () =>
                {
                    firstCancelCount++;

                    // 교체 통지를 받은 요청자가 곧바로 새 요청을 올리는 재진입 상황이다.
                    dialog.Show("세 번째 요청", "본문", onCancel: () => thirdCancelCount++);
                });

            dialog.Show("두 번째 요청", "본문", onCancel: () => secondCancelCount++);

            Assert.That(firstCancelCount, Is.EqualTo(1));
            Assert.That(secondCancelCount, Is.EqualTo(1), "재진입으로 교체당한 요청자도 취소를 통지받아야 한다.");
            Assert.That(thirdCancelCount, Is.Zero, "가장 마지막 요청은 아직 결과를 기다려야 한다.");

            dialog.Close();

            Assert.That(firstCancelCount, Is.EqualTo(1));
            Assert.That(secondCancelCount, Is.EqualTo(1));
            Assert.That(thirdCancelCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelCallbackCanOpenAnotherRequestWithoutLosingVisibilityOrStackMembership()
        {
            var dialog = CreateDialog();
            var managerObject = new GameObject(nameof(UiWindowManager));
            _createdGameObjects.Add(managerObject);
            var manager = managerObject.AddComponent<UiWindowManager>();
            manager.Register(dialog);
            var firstCancelCount = 0;
            var secondCancelCount = 0;
            dialog.Show("첫 요청", "본문", onCancel: () =>
            {
                firstCancelCount++;
                dialog.Show("두 번째 요청", "본문", onCancel: () => secondCancelCount++);
            });

            manager.CloseTopWindow();

            Assert.That(firstCancelCount, Is.EqualTo(1));
            Assert.That(secondCancelCount, Is.Zero);
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(dialog.gameObject.activeSelf, Is.True);
            Assert.That(manager.TopWindow, Is.SameAs(dialog));
            Assert.That(manager.OpenWindowCount, Is.EqualTo(1));

            manager.CloseTopWindow();

            Assert.That(secondCancelCount, Is.EqualTo(1));
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(manager.OpenWindowCount, Is.Zero);
        }

        [Test]
        public void CloseAllKeepsTheSameDialogReopenedByItsCancelCallback()
        {
            var input = new FakeInputStateController(true);
            var manager = CreateManager(input);
            var dialog = CreateModalDialog();
            manager.Register(dialog);
            var secondCancelCount = 0;
            dialog.Show("첫 요청", "본문", onCancel: () =>
                dialog.Show("새 요청", "본문", onCancel: () => secondCancelCount++));

            manager.CloseAllWindows();

            AssertManagedModalIsOpen(manager, dialog, input);
            Assert.That(secondCancelCount, Is.Zero, "진입 뒤 생긴 요청은 전체 닫기의 대상이 아니다.");

            manager.CloseTopWindow();
            Assert.That(secondCancelCount, Is.EqualTo(1));
            Assert.That(manager.OpenWindowCount, Is.Zero);
            Assert.That(input.LiveBlockCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CloseAllSkipsANewRequestOnAnotherDialogFromTheOriginalSnapshot(bool closeBeforeShow)
        {
            var input = new FakeInputStateController(true);
            var manager = CreateManager(input);
            var first = CreateModalDialog();
            var second = CreateDialog();
            manager.Register(first);
            manager.Register(second);
            var firstCancelCount = 0;
            var replacementCancelCount = 0;
            first.Show("아래 요청", "본문", onCancel: () => firstCancelCount++);
            second.Show("위 요청", "본문", onCancel: () =>
            {
                if (closeBeforeShow)
                {
                    first.Close();
                }

                first.Show("교체 요청", "본문", onCancel: () => replacementCancelCount++);
            });

            manager.CloseAllWindows();

            Assert.That(firstCancelCount, Is.EqualTo(1));
            Assert.That(replacementCancelCount, Is.Zero, "같은 인스턴스라도 새 요청이면 이전 스냅샷에서 닫으면 안 된다.");
            Assert.That(second.IsOpen, Is.False);
            AssertManagedModalIsOpen(manager, first, input);

            manager.CloseTopWindow();
            Assert.That(replacementCancelCount, Is.EqualTo(1));
            Assert.That(input.LiveBlockCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnregisterKeepsReopenedWindowsConsistentWithWhetherTheyRegisterAgain(bool registerAgain)
        {
            var input = new FakeInputStateController(true);
            var manager = CreateManager(input);
            var dialog = CreateModalDialog();
            manager.Register(dialog);
            var replacementCancelCount = 0;
            dialog.Show("첫 요청", "본문", onCancel: () =>
            {
                dialog.Show("새 요청", "본문", onCancel: () => replacementCancelCount++);
                if (registerAgain)
                {
                    manager.Register(dialog);
                }
            });

            manager.Unregister(dialog);

            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(dialog.gameObject.activeSelf, Is.True);
            Assert.That(manager.OpenWindowCount, Is.EqualTo(registerAgain ? 1 : 0));
            Assert.That(input.LiveBlockCount, Is.EqualTo(registerAgain ? 1 : 0));
            Assert.That(replacementCancelCount, Is.Zero);

            manager.Register(dialog);
            AssertManagedModalIsOpen(manager, dialog, input);
            manager.CloseTopWindow();
            Assert.That(replacementCancelCount, Is.EqualTo(1), "재등록 뒤에도 콜백과 스택 구독은 한 번씩만 동작한다.");
            Assert.That(manager.OpenWindowCount, Is.Zero);
            Assert.That(input.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void CloseAllClosesTheOriginalWindowsFromTopToBottom()
        {
            var input = new FakeInputStateController(true);
            var manager = CreateManager(input);
            var first = CreateModalDialog();
            var second = CreateDialog();
            manager.Register(first);
            manager.Register(second);
            var closed = new List<int>();
            first.Show("아래 요청", "본문", onCancel: () => closed.Add(1));
            second.Show("위 요청", "본문", onCancel: () => closed.Add(2));

            manager.CloseAllWindows();

            Assert.That(closed, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(first.IsOpen, Is.False);
            Assert.That(second.IsOpen, Is.False);
            Assert.That(manager.OpenWindowCount, Is.Zero);
            Assert.That(input.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void DestroyingTheManagerCannotRegisterAReopenedRequestBackIntoItsStack()
        {
            var input = new FakeInputStateController(true);
            var manager = CreateManager(input);
            var dialog = CreateModalDialog();
            manager.Register(dialog);
            dialog.Show("첫 요청", "본문", onCancel: () =>
            {
                dialog.Show("독립된 새 요청", "본문");
                manager.Register(dialog);
            });

            MonoBehaviourLifecycle.InvokeOnDestroy(manager);

            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(manager.OpenWindowCount, Is.Zero);
            Assert.That(input.LiveBlockCount, Is.Zero);
        }

        [Test]
        public void DestroyingAnOpenDialogNotifiesTheWaitingRequesterWithCancel()
        {
            var dialog = CreateDialog();
            var cancelCount = 0;
            dialog.Show("제목", "본문", onCancel: () => cancelCount++);

            MonoBehaviourLifecycle.InvokeOnDestroy(dialog);

            Assert.That(cancelCount, Is.EqualTo(1), "파괴로도 요청이 방치되어서는 안 된다.");
        }

        [Test]
        public void DestroyingAClosedDialogDoesNotNotifyAgain()
        {
            var dialog = CreateDialog();
            var cancelCount = 0;
            dialog.Show("제목", "본문", onCancel: () => cancelCount++);
            dialog.Close();

            MonoBehaviourLifecycle.InvokeOnDestroy(dialog);

            Assert.That(cancelCount, Is.EqualTo(1));
        }

        private ConfirmDialog CreateDialog()
        {
            var gameObject = new GameObject(nameof(ConfirmDialog));
            _createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<ConfirmDialog>();
        }

        private ConfirmDialog CreateModalDialog()
        {
            var dialog = CreateDialog();
            var serialized = new SerializedObject(dialog);
            serialized.FindProperty("isModal").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return dialog;
        }

        private UiWindowManager CreateManager(FakeInputStateController input)
        {
            var created = new GameObject(nameof(UiWindowManager));
            _createdGameObjects.Add(created);
            var manager = created.AddComponent<UiWindowManager>();
            manager.InjectInputStateController(input);
            return manager;
        }

        private static void AssertManagedModalIsOpen(UiWindowManager manager, ConfirmDialog dialog, FakeInputStateController input)
        {
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(dialog.gameObject.activeSelf, Is.True);
            Assert.That(manager.TopWindow, Is.SameAs(dialog));
            Assert.That(manager.OpenWindowCount, Is.EqualTo(1));
            Assert.That(input.LiveBlockCount, Is.EqualTo(1));
            Assert.That(input.IsInputEnabled, Is.False);
            Assert.That(input.IsUiInputEnabled, Is.True);
        }
    }
}
