using System.Collections.Generic;
using HS.Tactics.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// MainMenu.unity에서 게임플레이 씬으로 가는 활성 경로가 스테이지 버튼→배치 패널 하나뿐인지 검증한다.
    /// </summary>
    /// <remarks>
    /// 배치 패널의 "게임 시작"만 선택을 커밋한다. 커밋 없이 씬을 바꾸는 버튼이 켜져 있으면 게임플레이 씬의 재생기가 세울 것을
    /// 찾지 못하고 손 배치 창으로 떨어진다. 그래서 곧장 시작하는 버튼은 연결이 비어 있거나 GameObject가 꺼져 있어야 하고,
    /// 배치 패널과 스테이지 버튼은 연결되어 있어야 한다. 씬은 덧붙여 열고 끝나면 닫는다.
    /// </remarks>
    public sealed class MainMenuEntryPathTests
    {
        private const string MainMenuScenePath = "Assets/_HS/Tactics/Scenes/MainMenu.unity";
        private const string StartGameButtonPropertyName = "startGameButton";
        private const string StageButtonsPropertyName = "stageButtons";
        private const string HeroPlacementPanelPropertyName = "heroPlacementPanel";

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
        public void TheOnlyActivePathIntoGameplayIsTheStagePlacementPanel()
        {
            var serializedView = OpenMainMenuView();

            var panel = serializedView.FindProperty(HeroPlacementPanelPropertyName);
            Assert.That(panel, Is.Not.Null, $"TacticsMainMenuView에 {HeroPlacementPanelPropertyName} 필드가 있어야 한다.");
            Assert.That(panel.objectReferenceValue, Is.Not.Null, "배치 패널이 연결되어 있지 않으면 커밋하는 길이 없다.");

            var stageButtons = serializedView.FindProperty(StageButtonsPropertyName);
            Assert.That(stageButtons, Is.Not.Null, $"TacticsMainMenuView에 {StageButtonsPropertyName} 필드가 있어야 한다.");
            Assert.That(stageButtons.arraySize, Is.GreaterThan(0), "스테이지 버튼이 없으면 패널을 열 길이 없다.");
            for (var index = 0; index < stageButtons.arraySize; index++)
            {
                Assert.That(stageButtons.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null, $"스테이지 버튼 {index}번이 비어 있다.");
            }

            var startGameButton = serializedView.FindProperty(StartGameButtonPropertyName);
            Assert.That(startGameButton, Is.Not.Null, $"TacticsMainMenuView에 {StartGameButtonPropertyName} 필드가 있어야 한다.");
            // 버튼 형식은 이 검사 어셈블리가 참조하지 않는 UI 어셈블리에 있다. 필요한 것은 그 오브젝트가 꺼져 있는지뿐이므로 컴포넌트로 본다.
            if (startGameButton.objectReferenceValue is Component button)
            {
                Assert.That(
                    button.gameObject.activeSelf,
                    Is.False,
                    $"'{button.gameObject.name}'이 켜져 있다. 커밋 없이 게임플레이 씬으로 가는 길이 열려 있으면 재생기가 세울 것을 찾지 못하고 손 배치 창으로 떨어진다.");
            }
        }

        private SerializedObject OpenMainMenuView()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            _openedScenes.Add(scene);

            TacticsMainMenuView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<TacticsMainMenuView>(true);
                if (view != null)
                {
                    break;
                }
            }

            Assert.That(view, Is.Not.Null, $"{MainMenuScenePath}에 TacticsMainMenuView가 있어야 한다.");
            return new SerializedObject(view);
        }
    }
}
