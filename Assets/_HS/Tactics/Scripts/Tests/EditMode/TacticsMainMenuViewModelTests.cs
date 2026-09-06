using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Runtime;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.UI;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>메인 메뉴 ViewModel이 새 게임 요청을 한 번만 보내고 그동안 시작을 잠그는지 검증한다.</summary>
    public sealed class TacticsMainMenuViewModelTests
    {
        [Test]
        public void StartGameRequestsANewGameThroughTheFlowService()
        {
            var flow = new FakeGameFlowService();
            var viewModel = CreateViewModel(flow);

            var task = viewModel.StartGameAsync();

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(flow.StartCount, Is.EqualTo(1));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.CanStartGame, Is.True);
        }

        [Test]
        public void StartIsLockedWhileTheTransitionIsPending()
        {
            var flow = new FakeGameFlowService { PendingStart = new UniTaskCompletionSource() };
            var viewModel = CreateViewModel(flow);
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

            var task = viewModel.StartGameAsync();
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(viewModel.IsBusy, Is.True);
            Assert.That(viewModel.CanStartGame, Is.False);
            Assert.That(changedProperties, Is.EqualTo(new[] { "IsBusy", "CanStartGame" }));

            viewModel.StartGameAsync().GetAwaiter().GetResult();
            Assert.That(flow.StartCount, Is.EqualTo(1), "전환 중에 다시 누른 요청은 무시되어야 한다.");

            flow.PendingStart.TrySetResult();

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.CanStartGame, Is.True);
        }

        [Test]
        public void FailedTransitionUnlocksStartAndSurfacesTheException()
        {
            var flow = new FakeGameFlowService { ThrowOnStart = true };
            var viewModel = CreateViewModel(flow);

            var task = viewModel.StartGameAsync();

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Faulted));
            Assert.That(viewModel.IsBusy, Is.False, "실패해도 버튼이 잠긴 채 남으면 안 된다.");
            Assert.That(viewModel.CanStartGame, Is.True);
            // 예외를 꺼내 관찰해야 UniTask가 관찰되지 않은 예외로 따로 기록하지 않는다.
            Assert.That(() => task.GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void MissingFlowServiceIsRejectedAtConstruction()
        {
            Assert.That(() => CreateViewModel(null), Throws.ArgumentNullException);
        }

        /// <summary>스테이지 하나짜리 카탈로그와 빈 진행 상태로 ViewModel을 만든다. 이 검사들은 목록을 보지 않는다.</summary>
        private static TacticsMainMenuViewModel CreateViewModel(IGameFlowService flow)
        {
            var catalog = new FakeSceneCatalog(1, FakeSceneCatalog.Gameplay(1));
            return new TacticsMainMenuViewModel(
                flow, catalog, new StageProgressionService(catalog).Progression, new PartySelectionService());
        }
    }
}
