using System.Collections.Generic;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// PlacementViewModel이 배치 컨트롤러의 상태와 조작 가능 여부를 비추는지 검증한다.
    /// UGUI 버튼·TMP 라벨 및 View의 입력 배선은 이 파일의 범위 밖이다.
    /// 배치 단계·상한은 컨트롤러 공개 API로 조절하고, 계획에 항목을 넣어 스폰 없이 배치 상태를 구성한다.
    /// </summary>
    public sealed class PlacementViewModelTests
    {
        /// <summary>검사가 만든 것들이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        /// <summary>검사가 만든 ViewModel이며 정리 대상이다.</summary>
        private readonly List<PlacementViewModel> _createdViewModels = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var viewModel in _createdViewModels)
            {
                viewModel?.Dispose();
            }

            _createdViewModels.Clear();

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
        public void TheBattleCommandFollowsThePhase()
        {
            var controller = CreatePlacementController();
            PlaceOneEntry(controller);
            var viewModel = CreateViewModel(controller);

            Assert.That(
                viewModel.CanStartBattle,
                Is.True,
                "배치 단계이고 배치한 유닛이 하나 있는데 전투를 시작할 수 없다고 한다.");

            var changedProperties = RecordPropertyChanges(viewModel);
            Assert.That(controller.TryStartBattle(), Is.True);

            Assert.That(
                viewModel.CanStartBattle,
                Is.False,
                "전투가 시작된 뒤인데 아직 전투를 시작할 수 있다고 한다.");
            Assert.That(
                changedProperties,
                Contains.Item(nameof(PlacementViewModel.CanStartBattle)),
                "값은 바뀌었는데 그 속성이 바뀌었다는 알림이 오지 않았다. 화면은 옛 상태를 그대로 보인다.");
        }

        [Test]
        public void TheSelectionIsRefusedWhenTheCapacityIsReached()
        {
            var controller = CreatePlacementController();

            // BeginPlacement가 직렬화된 상한을 다시 넣으므로 그 뒤에 상한을 좁힌다.
            controller.Plan.SetCapacity(1);
            PlaceOneEntry(controller);

            var viewModel = CreateViewModel(controller);
            var definition = CreateUnitDefinition();

            Assert.That(viewModel.RemainingCapacity, Is.EqualTo(0));
            Assert.That(
                viewModel.CanSelectUnits,
                Is.False,
                "상한에 닿았는데 유닛을 더 고를 수 있다고 한다.");
            Assert.That(
                viewModel.SelectUnitDefinition(definition),
                Is.False,
                "상한에 닿았는데 선택이 받아들여졌다.");
            Assert.That(
                viewModel.SelectedUnitDefinition,
                Is.Null,
                "거절된 선택이 그대로 남았다. 화면에는 골라진 것으로 보이는데 놓이지는 않는다.");
        }

        [Test]
        public void TheSelectionClearsWhenTheBattleStarts()
        {
            var controller = CreatePlacementController();
            PlaceOneEntry(controller);
            var viewModel = CreateViewModel(controller);
            var definition = CreateUnitDefinition();

            Assert.That(viewModel.SelectUnitDefinition(definition), Is.True);
            Assert.That(controller.TryStartBattle(), Is.True);

            Assert.That(
                viewModel.SelectedUnitDefinition,
                Is.Null,
                "전투가 시작됐는데 고른 유닛이 남아 있다. 놓을 곳이 없는 선택이 화면에 남는다.");
        }

        [Test]
        public void NoNotificationArrivesAfterDispose()
        {
            var controller = CreatePlacementController();
            PlaceOneEntry(controller);
            var viewModel = CreateViewModel(controller);

            // 먼저 알림이 오는 것을 보인다. 이것이 없으면 아래의 "안 온다"가
            // 애초에 연결된 적 없어서 조용한 것인지 구분되지 않는다.
            var changedProperties = RecordPropertyChanges(viewModel);
            Assert.That(controller.TryStartBattle(), Is.True);
            Assert.That(
                changedProperties,
                Is.Not.Empty,
                "해제하기 전인데 컨트롤러의 변화가 ViewModel에 닿지 않았다.");

            viewModel.Dispose();
            changedProperties.Clear();

            controller.BeginPlacement();

            Assert.That(
                controller.Phase,
                Is.EqualTo(PlacementPhase.Placing),
                "이 검사는 해제 뒤에 실제로 단계가 바뀌어야 뜻을 갖는다.");
            Assert.That(
                changedProperties,
                Is.Empty,
                "해제한 뒤인데 알림이 왔다. 구독이 남아 있으면 사라진 화면이 계속 갱신된다.");
        }

        /// <summary>ViewModel이 알린 속성 이름을 순서대로 모으기 시작한다.</summary>
        /// <param name="viewModel">지켜볼 ViewModel이다.</param>
        /// <returns>알림이 올 때마다 이름이 쌓이는 목록이다.</returns>
        private static List<string> RecordPropertyChanges(PlacementViewModel viewModel)
        {
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
            return changedProperties;
        }

        /// <summary>배치 단계를 열고 넉넉한 구역을 지정한 배치 컨트롤러를 만든다.</summary>
        /// <returns>만든 배치 컨트롤러이다.</returns>
        private UnitPlacementController CreatePlacementController()
        {
            var controllerObject = new GameObject("UnitPlacementController");
            _createdObjects.Add(controllerObject);

            var controller = controllerObject.AddComponent<UnitPlacementController>();
            controller.BeginPlacement();

            // 씬에 구역 오브젝트를 두지 않으므로 계획에 직접 구역을 지정한다.
            controller.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));
            return controller;
        }

        /// <summary>
        /// 계획에 배치 항목 하나를 직접 넣어 "유닛을 하나 놓은 상태"를 만든다.
        /// </summary>
        /// <remarks>
        /// 컨트롤러의 배치 경로는 프리팹을 스폰하므로 EditMode에서 태우기 어렵다. 재려는 것은 스폰이 아니라
        /// 배치 수에 따라 판단이 어떻게 갈리는지이므로, 규칙만 판정하는 계획에 직접 넣는다.
        /// </remarks>
        /// <param name="controller">항목을 넣을 배치 컨트롤러이다.</param>
        private void PlaceOneEntry(UnitPlacementController controller)
        {
            var result = controller.Plan.TryPlace(CreateUnitDefinition(), Vector3.zero, out _);
            Assert.That(
                result,
                Is.EqualTo(PlacementResult.Success),
                "검사가 기대는 준비 단계가 실패했다. 배치 항목을 하나도 넣지 못했다.");
        }

        /// <summary>검사에서 쓸 유닛 정의를 만든다.</summary>
        /// <returns>만든 유닛 정의이다.</returns>
        private UnitDefinition CreateUnitDefinition()
        {
            var definition = UnitDefinition.CreateRuntime("PlacementTester", 100);
            _createdObjects.Add(definition);
            return definition;
        }

        /// <summary>배치 컨트롤러에 연결한 ViewModel을 만든다.</summary>
        /// <param name="controller">연결할 배치 컨트롤러이다.</param>
        /// <returns>만든 ViewModel이다.</returns>
        private PlacementViewModel CreateViewModel(UnitPlacementController controller)
        {
            var viewModel = new PlacementViewModel(controller);
            _createdViewModels.Add(viewModel);
            return viewModel;
        }
    }
}
