using System.Collections.Generic;
using HS.Tactics.Placement;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// Level1.unity에 MainMenu의 배치를 재생하는 배선이 갖춰져 있는지 검증한다.
    /// </summary>
    /// <remarks>
    /// 재생기는 씬의 배치 컨트롤러에 항목을 넘기고 전투를 시작한다. 재생기가 없거나 다른 컨트롤러를 가리키거나, 컨트롤러가
    /// 켜질 때 배치 단계로 들어가지 않으면 항목이 <c>NotInPlacingPhase</c>로 전부 거부되어 아무것도 세워지지 않는다.
    /// 구역이 없으면 <c>NoPlacementZone</c>으로 거부된다. 그 넷을 씬에서 읽어 확인한다. 씬은 덧붙여 열고 끝나면 닫는다.
    /// </remarks>
    public sealed class Level1PartySelectionReplayWiringTests
    {
        private const string Level1ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string PlacementControllerPropertyName = "placementController";
        private const string BeginPlacementOnEnablePropertyName = "beginPlacementOnEnable";
        private const string PlacementZonePropertyName = "placementZone";

        private readonly List<Scene> _openedScenes = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var scene in _openedScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            _openedScenes.Clear();
        }

        [Test]
        public void TheReplayPointsAtTheScenesOnlyPlacementControllerWhichBeginsPlacingOnEnable()
        {
            var scene = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Additive);
            _openedScenes.Add(scene);

            var replays = FindAllInScene<PartySelectionReplay>(scene);
            var controllers = FindAllInScene<UnitPlacementController>(scene);
            Assert.That(replays, Has.Count.EqualTo(1), "재생기는 하나여야 한다. 없으면 커밋된 선택이 버려지고, 둘이면 같은 항목을 두 번 세운다.");
            Assert.That(controllers, Has.Count.EqualTo(1), "배치 컨트롤러는 하나여야 한다. 둘이면 재생기와 배치 창이 다른 것을 볼 수 있다.");

            var replay = new SerializedObject(replays[0]);
            var controllerProperty = replay.FindProperty(PlacementControllerPropertyName);
            Assert.That(controllerProperty, Is.Not.Null, $"PartySelectionReplay에 {PlacementControllerPropertyName} 필드가 있어야 한다.");
            Assert.That(controllerProperty.objectReferenceValue, Is.SameAs(controllers[0]), "재생기가 씬의 배치 컨트롤러를 가리켜야 한다.");

            var controller = new SerializedObject(controllers[0]);
            var beginOnEnable = controller.FindProperty(BeginPlacementOnEnablePropertyName);
            Assert.That(beginOnEnable, Is.Not.Null, $"UnitPlacementController에 {BeginPlacementOnEnablePropertyName} 필드가 있어야 한다.");
            Assert.That(beginOnEnable.boolValue, Is.True, "켜질 때 배치 단계로 들어가지 않으면 Start의 재생이 NotInPlacingPhase로 전부 거부된다.");

            var zone = controller.FindProperty(PlacementZonePropertyName);
            Assert.That(zone, Is.Not.Null, $"UnitPlacementController에 {PlacementZonePropertyName} 필드가 있어야 한다.");
            Assert.That(zone.objectReferenceValue, Is.Not.Null, "배치 구역이 없으면 재생이 NoPlacementZone으로 전부 거부된다.");
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found;
        }
    }
}
