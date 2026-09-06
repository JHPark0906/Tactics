using System.Collections.Generic;
using HS.Tactics.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// PlacementWindowView가 참조하는 버튼에서 인스펙터의 영속 onClick 호출을 제거한다.
    /// 이 버튼들은 View가 런타임 리스너로 연결하므로 영속 호출이 함께 있으면 클릭이 중복 처리된다.
    /// RemoveAllListeners는 영속 호출을 지우지 않으므로 직렬화된 호출 목록을 비운다.
    /// 다른 버튼은 변경하지 않으며, 반복 실행 시 필요한 변경만 저장한다.
    /// </summary>
    public static class PlacementWindowButtonWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";

        /// <summary>전투 개시 버튼을 가리키는 뷰의 직렬화 필드 이름이다.</summary>
        private const string StartBattleButtonPropertyName = "startBattleButton";

        /// <summary>유닛 선택 버튼 목록을 가리키는 뷰의 직렬화 필드 이름이다.</summary>
        private const string UnitSelectionButtonsPropertyName = "unitSelectionButtons";

        /// <summary>버튼의 영속 onClick 호출 목록이 있는 자리이다. 형식을 몰라도 이 경로로 읽고 쓸 수 있다.</summary>
        private const string PersistentCallsPropertyPath = "m_OnClick.m_PersistentCalls.m_Calls";

        /// <summary>Level1.unity를 열어 배치 창 버튼들의 영속 onClick 호출을 비우고 저장한다. 비울 것이 없으면 저장하지 않는다.</summary>
        [MenuItem("Tools/HS Tactics/Placement/Clear Placement Window Button OnClick Calls")]
        public static void ClearPlacementWindowButtonCalls()
        {
            // 에디터에서 손으로 실행했을 때 열려 있던 씬의 미저장 변경을 묻지 않고 버리지 않도록 먼저 묻는다.
            // 배치모드에서는 물을 사람이 없어 그대로 지나간다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[PlacementWindowButtonWiring] 열려 있던 씬의 저장을 취소해 Level1.unity는 손대지 않는다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = FindInScene<PlacementWindowView>(scene);
            if (view == null)
            {
                Debug.LogError($"[PlacementWindowButtonWiring] {ScenePath}에서 PlacementWindowView를 찾지 못해 손대지 않는다.");
                return;
            }

            var cleared = 0;
            foreach (var button in CollectViewButtons(view))
            {
                cleared += ClearPersistentCalls(button);
            }

            if (cleared == 0)
            {
                Debug.Log($"[PlacementWindowButtonWiring] {ScenePath}의 배치 창 버튼에 비울 영속 호출이 없어 저장하지 않는다.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PlacementWindowButtonWiring] 영속 호출 {cleared}개를 비우고 {ScenePath}를 저장했다. 버튼의 배선은 이제 코드 한 곳에서만 온다.");
        }

        /// <summary>뷰의 직렬화 필드가 가리키는 버튼을 모은다. 비어 있는 칸은 건너뛴다.</summary>
        /// <param name="view">배치 창 뷰이다.</param>
        /// <returns>뷰가 가리키는 버튼 구성요소이다.</returns>
        private static List<Component> CollectViewButtons(Component view)
        {
            var buttons = new List<Component>();
            var serializedView = new SerializedObject(view);

            if (serializedView.FindProperty(StartBattleButtonPropertyName)?.objectReferenceValue is Component startBattleButton)
            {
                buttons.Add(startBattleButton);
            }

            var selectionButtons = serializedView.FindProperty(UnitSelectionButtonsPropertyName);
            if (selectionButtons == null || !selectionButtons.isArray)
            {
                return buttons;
            }

            for (var index = 0; index < selectionButtons.arraySize; index++)
            {
                if (selectionButtons.GetArrayElementAtIndex(index).objectReferenceValue is Component selectionButton)
                {
                    buttons.Add(selectionButton);
                }
            }

            return buttons;
        }

        /// <summary>버튼 하나의 영속 onClick 호출을 비운다.</summary>
        /// <param name="button">비울 버튼 구성요소이다.</param>
        /// <returns>비운 호출 수이며, 없었으면 0이다.</returns>
        private static int ClearPersistentCalls(Component button)
        {
            var serializedButton = new SerializedObject(button);
            var calls = serializedButton.FindProperty(PersistentCallsPropertyPath);
            if (calls == null || !calls.isArray)
            {
                Debug.LogWarning(
                    $"[PlacementWindowButtonWiring] '{button.gameObject.name}'에서 {PersistentCallsPropertyPath}를 찾지 못해 손대지 않는다.",
                    button);
                return 0;
            }

            var count = calls.arraySize;
            if (count == 0)
            {
                return 0;
            }

            for (var index = 0; index < count; index++)
            {
                Debug.Log($"[PlacementWindowButtonWiring] '{button.gameObject.name}'의 영속 호출 {DescribeCall(calls.GetArrayElementAtIndex(index))}을 비운다.");
            }

            calls.ClearArray();
            serializedButton.ApplyModifiedPropertiesWithoutUndo();
            return count;
        }

        /// <summary>영속 호출 하나를 「대상.메서드」로 적는다. 어느 배선이 사라지는지가 로그에 남아야 한다.</summary>
        /// <param name="call">적을 영속 호출이다.</param>
        /// <returns>사람이 읽을 표현이다.</returns>
        private static string DescribeCall(SerializedProperty call)
        {
            var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
            var methodName = call.FindPropertyRelative("m_MethodName")?.stringValue;
            var targetName = target != null ? target.GetType().Name : "(없음)";
            return $"{targetName}.{(string.IsNullOrEmpty(methodName) ? "(이름 없음)" : methodName)}";
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
