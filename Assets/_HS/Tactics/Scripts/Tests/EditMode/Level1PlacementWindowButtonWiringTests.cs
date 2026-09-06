using System.Collections.Generic;
using System.Text;
using HS.Tactics.Placement;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// Level1.unity의 배치 창 버튼에 인스펙터로 걸린 영속 onClick 호출이 남아 있지 않은지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 버튼의 배선은 <see cref="PlacementWindowView"/>가 코드로 한다. 씬에 영속 호출이 함께 남아 있으면 한 번 누를 때 두
    /// 배선이 모두 불리고, 영속 호출이 먼저 불린다. 그것이 엇갈려 걸려 있으면 유닛을 고르려는 누름이 전투를 시작한다.
    /// </para>
    /// <para>
    /// 뷰가 가리키는 버튼만 본다. 씬의 다른 버튼은 자기 영속 호출로 동작할 수 있어 이 규칙의 대상이 아니다. 버튼 형식은 이
    /// 검사 어셈블리가 참조하지 않는 UI 어셈블리에 있으므로, 직렬화 경로로 호출 목록을 읽는다. 씬은 덧붙여 열고 끝나면 닫는다.
    /// </para>
    /// </remarks>
    public sealed class Level1PlacementWindowButtonWiringTests
    {
        private const string Level1ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string StartBattleButtonPropertyName = "startBattleButton";
        private const string UnitSelectionButtonsPropertyName = "unitSelectionButtons";
        private const string PersistentCallsPropertyPath = "m_OnClick.m_PersistentCalls.m_Calls";

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
        public void ThePlacementWindowButtonsCarryNoInspectorOnClickCalls()
        {
            var scene = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Additive);
            _openedScenes.Add(scene);

            var view = FindInScene<PlacementWindowView>(scene);
            Assert.That(view, Is.Not.Null, $"{Level1ScenePath}에 PlacementWindowView가 있어야 한다.");

            var serializedView = new SerializedObject(view);
            var buttons = new List<(string Role, Component Button)>();

            var startBattleButton = serializedView.FindProperty(StartBattleButtonPropertyName);
            Assert.That(startBattleButton, Is.Not.Null, $"PlacementWindowView에 {StartBattleButtonPropertyName} 필드가 있어야 한다.");
            if (startBattleButton.objectReferenceValue is Component startButton)
            {
                buttons.Add((StartBattleButtonPropertyName, startButton));
            }

            var selectionButtons = serializedView.FindProperty(UnitSelectionButtonsPropertyName);
            Assert.That(selectionButtons, Is.Not.Null, $"PlacementWindowView에 {UnitSelectionButtonsPropertyName} 필드가 있어야 한다.");
            for (var index = 0; index < selectionButtons.arraySize; index++)
            {
                if (selectionButtons.GetArrayElementAtIndex(index).objectReferenceValue is Component selectionButton)
                {
                    buttons.Add(($"{UnitSelectionButtonsPropertyName}[{index}]", selectionButton));
                }
            }

            Assert.That(buttons, Is.Not.Empty, "무대 확인: 뷰가 가리키는 버튼이 하나도 없으면 이 검사가 아무것도 지키지 않는다.");

            var remaining = new StringBuilder();
            var remainingCount = 0;
            foreach (var (role, button) in buttons)
            {
                var calls = new SerializedObject(button).FindProperty(PersistentCallsPropertyPath);
                Assert.That(calls, Is.Not.Null, $"'{button.gameObject.name}'에서 {PersistentCallsPropertyPath}를 읽지 못했다.");
                for (var index = 0; index < calls.arraySize; index++)
                {
                    remainingCount++;
                    remaining.Append($"\n  {role} '{button.gameObject.name}' → {DescribeCall(calls.GetArrayElementAtIndex(index))}");
                }
            }

            Assert.That(
                remainingCount,
                Is.Zero,
                $"배치 창 버튼에 인스펙터 onClick 호출이 남아 있다. 코드가 붙이는 배선과 겹쳐 한 번 누를 때 둘 다 불린다:{remaining}");
        }

        /// <summary>영속 호출 하나를 「대상.메서드」로 적는다. 어느 버튼에 무엇이 남았는지가 실패 메시지에 보여야 한다.</summary>
        private static string DescribeCall(SerializedProperty call)
        {
            var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
            var methodName = call.FindPropertyRelative("m_MethodName")?.stringValue;
            var targetName = target != null ? target.GetType().Name : "(없음)";
            return $"{targetName}.{(string.IsNullOrEmpty(methodName) ? "(이름 없음)" : methodName)}";
        }

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
