using System.Collections.Generic;
using System.Reflection;
using HS.Tactics.Flow;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>전투 결과 창의 진행 콜백 1회 통지 규약과 재진입 처리를 검증한다.</summary>
    public sealed class BattleResultWindowTests
    {
        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ShowOpensTheWindowWithoutInvokingCallback()
        {
            var window = CreateWindow();
            var proceedCount = 0;

            window.Show(BattleOutcome.Victory, () => proceedCount++);

            Assert.That(window.IsOpen, Is.True);
            Assert.That(window.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(proceedCount, Is.Zero);
        }

        [Test]
        public void ClosingWithoutTheButtonNotifiesProceedExactlyOnce()
        {
            var window = CreateWindow();
            var proceedCount = 0;
            window.Show(BattleOutcome.Defeat, () => proceedCount++);

            window.Close();
            window.Close();

            Assert.That(proceedCount, Is.EqualTo(1));
        }

        [Test]
        public void DestroyingWhileOpenNotifiesProceedExactlyOnce()
        {
            var window = CreateWindow();
            var proceedCount = 0;
            window.Show(BattleOutcome.Defeat, () => proceedCount++);

            // 에디터는 플레이 모드가 아닐 때 OnDestroy를 호출하지 않으므로 파괴 경로를 직접 구동한다.
            MonoBehaviourLifecycle.InvokeOnDestroy(window);

            Assert.That(
                proceedCount,
                Is.EqualTo(1),
                "결과 창이 열린 채 파괴되면 흐름이 멈추지 않도록 진행을 통지해야 한다.");
            Assert.That(window.IsOpen, Is.False, "파괴 통지 뒤에는 창이 열린 상태로 남지 않아야 한다.");
        }

        [Test]
        public void DestroyingAfterTheWindowWasClosedDoesNotNotifyAgain()
        {
            var window = CreateWindow();
            var proceedCount = 0;
            window.Show(BattleOutcome.Defeat, () => proceedCount++);
            window.Close();
            Assert.That(proceedCount, Is.EqualTo(1));

            MonoBehaviourLifecycle.InvokeOnDestroy(window);

            Assert.That(
                proceedCount,
                Is.EqualTo(1),
                "이미 확정된 요청이 파괴로 다시 통지되면 스테이지 선택으로 두 번 넘어가려 한다.");
        }

        [Test]
        public void DestroyingAfterTheProceedButtonWasUsedDoesNotNotifyAgain()
        {
            var window = CreateWindow();
            var proceedCount = 0;
            window.Show(BattleOutcome.Victory, () => proceedCount++);
            InvokeProceedClicked(window);
            Assert.That(proceedCount, Is.EqualTo(1));

            MonoBehaviourLifecycle.InvokeOnDestroy(window);

            Assert.That(proceedCount, Is.EqualTo(1));
        }

        [Test]
        public void DestroyingAWindowThatWasNeverShownIsHarmless()
        {
            var window = CreateWindow();

            Assert.That(() => MonoBehaviourLifecycle.InvokeOnDestroy(window), Throws.Nothing);

            Assert.That(window.IsOpen, Is.False);
        }

        [Test]
        public void ShowWhileOpenNotifiesThePreviousRequester()
        {
            var window = CreateWindow();
            var firstProceedCount = 0;
            window.Show(BattleOutcome.Defeat, () => firstProceedCount++);

            window.Show(BattleOutcome.Victory);

            Assert.That(firstProceedCount, Is.EqualTo(1), "교체당한 요청자는 진행을 통지받아야 한다.");
            Assert.That(window.IsOpen, Is.True);
            Assert.That(window.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void ReplacedRequesterIsNotNotifiedAgainWhenTheWindowLaterCloses()
        {
            var window = CreateWindow();
            var firstProceedCount = 0;
            window.Show(BattleOutcome.Defeat, () => firstProceedCount++);
            window.Show(BattleOutcome.Victory);

            window.Close();

            Assert.That(firstProceedCount, Is.EqualTo(1));
        }

        [Test]
        public void ClosingAnUnusedWindowDoesNotThrow()
        {
            var window = CreateWindow();

            Assert.That(() => window.Close(), Throws.Nothing);
            Assert.That(window.IsOpen, Is.False);
        }

        /// <summary>진행 버튼 클릭 처리를 직접 호출한다. 버튼 없이도 클릭 경로를 검증하기 위함이다.</summary>
        /// <param name="window">클릭을 처리할 결과 창이다.</param>
        private static void InvokeProceedClicked(BattleResultWindow window)
        {
            var method = typeof(BattleResultWindow).GetMethod(
                "OnProceedClicked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "BattleResultWindow.OnProceedClicked 메서드를 찾지 못했다.");
            method.Invoke(window, null);
        }

        /// <summary>표시 요소를 연결하지 않은 최소 구성의 결과 창을 만든다.</summary>
        private BattleResultWindow CreateWindow()
        {
            return CreateWindowObject().AddComponent<BattleResultWindow>();
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        private GameObject CreateWindowObject()
        {
            var windowObject = new GameObject("BattleResultWindow");
            _createdObjects.Add(windowObject);
            return windowObject;
        }
    }
}
