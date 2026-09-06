using HS.Tactics.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// MainMenu에 영웅 배치 패널이 연결되어 있는지 확인한 뒤 직접 게임을 시작하는 버튼을 비활성화한다.
    /// 버튼 오브젝트는 삭제하지 않으며, 버튼을 실제로 끈 경우에만 씬을 저장한다.
    /// </summary>
    public static class MainMenuEntryPathSceneWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/MainMenu.unity";
        private const string StartGameButtonPropertyName = "startGameButton";
        private const string HeroPlacementPanelPropertyName = "heroPlacementPanel";

        /// <summary>MainMenu.unity를 열어 곧장 시작하는 버튼의 GameObject를 끄고 저장한다. 바꿀 것이 없으면 저장하지 않는다.</summary>
        [MenuItem("Tools/HS Tactics/Main Menu/Disable Direct Game Start Button")]
        public static void DisableDirectGameStartButton()
        {
            // 에디터에서 손으로 실행했을 때 열려 있던 씬의 미저장 변경을 묻지 않고 버리지 않도록 먼저 묻는다.
            // 배치모드에서는 물을 사람이 없어 그대로 지나간다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[MainMenuEntryPathSceneWiring] 열려 있던 씬의 저장을 취소해 MainMenu.unity는 손대지 않는다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = FindInScene<TacticsMainMenuView>(scene);
            if (view == null)
            {
                Debug.LogError($"[MainMenuEntryPathSceneWiring] {ScenePath}에서 TacticsMainMenuView를 찾지 못해 손대지 않는다.");
                return;
            }

            var serializedView = new SerializedObject(view);
            var panelProperty = serializedView.FindProperty(HeroPlacementPanelPropertyName);
            if (panelProperty == null || panelProperty.objectReferenceValue == null)
            {
                Debug.LogError(
                    $"[MainMenuEntryPathSceneWiring] {ScenePath}의 TacticsMainMenuView에 배치 패널이 연결되어 있지 않다. " +
                    "이 상태에서 곧장 시작하는 버튼을 끄면 게임플레이 씬으로 가는 길이 없어지므로 손대지 않는다.");
                return;
            }

            var buttonProperty = serializedView.FindProperty(StartGameButtonPropertyName);
            var button = buttonProperty != null ? buttonProperty.objectReferenceValue as Button : null;
            if (button == null)
            {
                Debug.Log($"[MainMenuEntryPathSceneWiring] {ScenePath}의 곧장 시작하는 버튼 연결이 비어 있어 끌 것이 없다.");
                return;
            }

            if (!button.gameObject.activeSelf)
            {
                Debug.Log($"[MainMenuEntryPathSceneWiring] '{button.gameObject.name}'은 이미 꺼져 있어 저장하지 않는다.");
                return;
            }

            button.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"[MainMenuEntryPathSceneWiring] '{button.gameObject.name}'을 끄고 {ScenePath}를 저장했다. " +
                "게임플레이 씬으로 가는 길은 스테이지 버튼→배치 패널→\"게임 시작\" 하나다.");
        }

        /// <summary>씬의 뿌리 오브젝트들을 훑어 컴포넌트를 찾는다. 비활성 오브젝트도 포함한다.</summary>
        /// <param name="scene">찾을 씬이다.</param>
        /// <typeparam name="T">찾을 컴포넌트 형식이다.</typeparam>
        /// <returns>찾은 컴포넌트이며 없으면 null이다.</returns>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
