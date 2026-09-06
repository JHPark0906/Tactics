using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.ProjectManagement;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.UI;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 메인 메뉴 ViewModel의 스테이지 목록과 스테이지 선택을 검증한다.
    /// 목록은 카탈로그의 게임플레이 씬을 번호 순으로 세우고, 클리어 표시는 진행 상태에서 읽으며,
    /// 스테이지 선택은 새 게임과 같은 잠금을 지나 흐름 서비스에 그 번호로 닿는다.
    /// </summary>
    public sealed class TacticsMainMenuStageSelectionTests
    {
        [Test]
        public void OnlyGameplayScenesAreListedInIdOrder()
        {
            var catalog = new FakeSceneCatalog(
                1,
                FakeSceneCatalog.MainMenu(),
                FakeSceneCatalog.Gameplay(2),
                FakeSceneCatalog.Gameplay(1));
            var viewModel = CreateViewModel(new FakeGameFlowService(), catalog);

            viewModel.Load();

            Assert.That(viewModel.Stages.Count, Is.EqualTo(2));
            Assert.That(viewModel.Stages[0].StageId, Is.EqualTo(1));
            Assert.That(viewModel.Stages[1].StageId, Is.EqualTo(2));
            Assert.That(viewModel.ClearedStageCount, Is.EqualTo(0));
        }

        [Test]
        public void ClearedMarksAreReadFromTheProgression()
        {
            var catalog = CreateCatalog(stageCount: 3);
            var progression = new StageProgressionService(catalog).Progression;
            progression.SetHighestClearedStage(2);
            var viewModel = new TacticsMainMenuViewModel(
                new FakeGameFlowService(), catalog, progression, new PartySelectionService());

            viewModel.Load();

            Assert.That(viewModel.Stages[0].IsCleared, Is.True);
            Assert.That(viewModel.Stages[1].IsCleared, Is.True);
            Assert.That(viewModel.Stages[2].IsCleared, Is.False);
            Assert.That(viewModel.ClearedStageCount, Is.EqualTo(2));
        }

        [Test]
        public void LoadAnnouncesTheListAndTheClearedCount()
        {
            var viewModel = CreateViewModel(new FakeGameFlowService(), CreateCatalog(stageCount: 1));
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

            viewModel.Load();

            Assert.That(changedProperties, Is.EqualTo(new[] { "Stages", "ClearedStageCount" }));
        }

        [Test]
        public void StartStageRequestsThatLevelFromTheFlowService()
        {
            var flow = new FakeGameFlowService();
            var viewModel = CreateViewModel(flow, CreateCatalog(stageCount: 2));

            var task = viewModel.StartStageAsync(2);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(flow.LoadedLevelIds, Is.EqualTo(new[] { 2 }));
            Assert.That(flow.StartCount, Is.EqualTo(0), "스테이지 선택은 새 게임 요청이 아니다.");
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.CanStartGame, Is.True);
        }

        [Test]
        public void OtherRequestsAreIgnoredWhileAStageTransitionIsPending()
        {
            var flow = new FakeGameFlowService { PendingLoad = new UniTaskCompletionSource() };
            var viewModel = CreateViewModel(flow, CreateCatalog(stageCount: 2));

            var task = viewModel.StartStageAsync(1);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(viewModel.IsBusy, Is.True);
            Assert.That(viewModel.CanStartGame, Is.False);

            viewModel.StartStageAsync(2).GetAwaiter().GetResult();
            viewModel.StartGameAsync().GetAwaiter().GetResult();
            Assert.That(flow.LoadedLevelIds, Is.EqualTo(new[] { 1 }), "전환 중의 다른 스테이지 요청은 무시되어야 한다.");
            Assert.That(flow.StartCount, Is.EqualTo(0), "전환 중의 새 게임 요청은 무시되어야 한다.");

            flow.PendingLoad.TrySetResult();

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.CanStartGame, Is.True);
        }

        [Test]
        public void StageRequestIsIgnoredWhileANewGameIsPending()
        {
            var flow = new FakeGameFlowService { PendingStart = new UniTaskCompletionSource() };
            var viewModel = CreateViewModel(flow, CreateCatalog(stageCount: 2));

            var task = viewModel.StartGameAsync();
            viewModel.StartStageAsync(2).GetAwaiter().GetResult();

            Assert.That(flow.LoadedLevelIds, Is.Empty, "새 게임 전환 중의 스테이지 요청은 무시되어야 한다.");

            flow.PendingStart.TrySetResult();

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(viewModel.IsBusy, Is.False);
        }

        [Test]
        public void FailedStageTransitionUnlocksAndSurfacesTheException()
        {
            var flow = new FakeGameFlowService { ThrowOnLoad = true };
            var viewModel = CreateViewModel(flow, CreateCatalog(stageCount: 1));

            var task = viewModel.StartStageAsync(1);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Faulted));
            Assert.That(viewModel.IsBusy, Is.False, "실패해도 버튼이 잠긴 채 남으면 안 된다.");
            Assert.That(viewModel.CanStartGame, Is.True);
            // 예외를 꺼내 관찰해야 UniTask가 관찰되지 않은 예외로 따로 기록하지 않는다.
            Assert.That(() => task.GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void MissingCatalogOrProgressionIsRejectedAtConstruction()
        {
            var catalog = CreateCatalog(stageCount: 1);
            var progression = new StageProgressionService(catalog).Progression;

            Assert.That(
                () => new TacticsMainMenuViewModel(
                    new FakeGameFlowService(), null, progression, new PartySelectionService()),
                Throws.ArgumentNullException);
            Assert.That(
                () => new TacticsMainMenuViewModel(
                    new FakeGameFlowService(), catalog, null, new PartySelectionService()),
                Throws.ArgumentNullException);
            Assert.That(
                () => new TacticsMainMenuViewModel(new FakeGameFlowService(), catalog, progression, null),
                Throws.ArgumentNullException);
        }

        /// <summary>1번부터 지정한 개수까지의 게임플레이 씬만 가진 카탈로그를 만든다.</summary>
        private static FakeSceneCatalog CreateCatalog(int stageCount)
        {
            var scenes = new ProjectSceneDefinition[stageCount];
            for (var index = 0; index < stageCount; index++)
            {
                scenes[index] = FakeSceneCatalog.Gameplay(index + 1);
            }

            return new FakeSceneCatalog(1, scenes);
        }

        /// <summary>카탈로그로 만든 빈 진행 상태와 함께 ViewModel을 만든다.</summary>
        private static TacticsMainMenuViewModel CreateViewModel(FakeGameFlowService flow, FakeSceneCatalog catalog)
        {
            return new TacticsMainMenuViewModel(
                flow, catalog, new StageProgressionService(catalog).Progression, new PartySelectionService());
        }
    }
}
