using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using HS.Tactics.Placement;
using HS.Tactics.UI;
using HS.Tactics.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// 플레이어가 세울 수 있는 유닛 목록에서 적 유닛을 걷어 내는 메뉴이다. Level1.unity의 배치 창과 MainMenu.unity의 영웅 배치
    /// 패널, 두 씬에 직렬화된 목록을 손본다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>아군인지는 프리팹의 행동 트리로 가른다.</b> 유닛 정의의 기본 진영은 두 정의 모두 비어 있고 다른 표지도 없다. 아군과
    /// 적을 실제로 다르게 움직이게 하는 것은 프리팹 뿌리의 <see cref="TacticalUnit"/>이 든 행동 트리 하나이므로, 「정의의
    /// 프리팹이 아군 트리(<c>UnitBehaviourTree.asset</c>)를 들고 있는가」를 기준으로 삼는다. 정의에 따로 표지를 두면 트리와
    /// 어긋날 수 있는 두 번째 진실이 생긴다.
    /// </para>
    /// <para>
    /// <b>판정할 수 없는 정의는 건드리지 않는다.</b> 프리팹이 없거나 유닛 컴포넌트가 없거나 트리 필드를 읽지 못하면 오류를 남기고
    /// 그대로 둔다. 「모르면 지운다」로 기울면 필드 이름이 바뀌었을 때 목록이 통째로 비고 버튼이 다 지워진다.
    /// </para>
    /// <para>
    /// <b>목록은 인덱스로 짝지어져 있다.</b> 두 뷰 모두 정의 목록과 선택 버튼(그리고 초상 이미지)을 같은 순서로 들고 인덱스로
    /// 맞춘다. 그래서 정의 하나를 빼면 같은 인덱스의 위젯도 함께 빼고 그 GameObject를 지운다. 위젯만 남기면 뒤 인덱스가
    /// 밀려 다른 정의의 버튼이 되거나, 빈 버튼이 씬에 남는다.
    /// </para>
    /// <para>
    /// <b>몇 번을 돌려도 같다.</b> 뺄 것이 없으면 씬을 저장하지 않는다. 판정은 fileID가 아니라 내용(정의가 든 프리팹의
    /// 트리)으로 하므로 씬을 편집한 뒤에도 그대로 쓸 수 있다.
    /// </para>
    /// </remarks>
    public static class PlayerSelectableUnitsSceneWiring
    {
        private const string Level1ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string MainMenuScenePath = "Assets/_HS/Tactics/Scenes/MainMenu.unity";
        private const string AllyBehaviourTreePath = "Assets/_HS/Tactics/AI/UnitBehaviourTree.asset";

        private const string DefinitionsPropertyName = "selectableUnitDefinitions";
        private const string BehaviourTreePropertyName = "behaviourTree";

        /// <summary>정의 목록과 같은 순서로 짝지어진 위젯 목록의 필드 이름이다. 뷰에 없는 이름은 건너뛴다.</summary>
        private static readonly string[] PairedWidgetPropertyNames = { "unitSelectionButtons", "unitPortraitImages" };

        /// <summary>두 씬의 선택 목록에서 아군 트리를 들지 않은 정의와 짝지어진 위젯을 걷어 내고 저장한다.</summary>
        [MenuItem("Tools/HS Tactics/Placement/Remove Enemy Units From Player Selection")]
        public static void RemoveEnemyUnitsFromPlayerSelection()
        {
            var allyTree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(AllyBehaviourTreePath);
            if (allyTree == null)
            {
                Debug.LogError($"[PlayerSelectableUnitsSceneWiring] {AllyBehaviourTreePath}를 읽지 못해 아군을 가를 수 없다. 손대지 않는다.");
                return;
            }

            // 에디터에서 손으로 실행했을 때 열려 있던 씬의 미저장 변경을 묻지 않고 버리지 않도록 먼저 묻는다.
            // 배치모드에서는 물을 사람이 없어 그대로 지나간다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[PlayerSelectableUnitsSceneWiring] 열려 있던 씬의 저장을 취소해 아무 씬도 손대지 않는다.");
                return;
            }

            PruneScene<PlacementWindowView>(Level1ScenePath, allyTree);
            PruneScene<HeroPlacementPanelView>(MainMenuScenePath, allyTree);
        }

        /// <summary>씬을 열어 뷰의 선택 목록을 걷어 내고, 바뀐 것이 있을 때만 저장한다.</summary>
        /// <typeparam name="TView">선택 목록을 든 뷰 컴포넌트 형식이다.</typeparam>
        /// <param name="scenePath">손볼 씬의 경로이다.</param>
        /// <param name="allyTree">아군 행동 트리 에셋이다.</param>
        private static void PruneScene<TView>(string scenePath, BehaviourTreeAsset allyTree) where TView : Component
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var view = FindInScene<TView>(scene);
            if (view == null)
            {
                Debug.LogError($"[PlayerSelectableUnitsSceneWiring] {scenePath}에서 {typeof(TView).Name}을 찾지 못해 손대지 않는다.");
                return;
            }

            var widgetsToDestroy = new List<GameObject>();
            if (!PruneView(view, allyTree, widgetsToDestroy))
            {
                Debug.Log($"[PlayerSelectableUnitsSceneWiring] {scenePath}의 {typeof(TView).Name}에 걷어 낼 정의가 없어 저장하지 않는다.");
                return;
            }

            foreach (var widget in widgetsToDestroy)
            {
                // 초상이 버튼의 자식이면 버튼을 지울 때 함께 사라지므로, 이미 사라진 것은 건너뛴다.
                if (widget != null)
                {
                    Object.DestroyImmediate(widget);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PlayerSelectableUnitsSceneWiring] {scenePath}를 저장했다.");
        }

        /// <summary>
        /// 뷰의 정의 목록을 뒤에서부터 훑어 아군 트리를 들지 않은 정의를 빼고, 같은 인덱스의 짝지어진 위젯도 목록에서 빼면서
        /// 그 GameObject를 지울 목록에 모은다. 판정할 수 없는 정의는 오류를 남기고 그대로 둔다.
        /// </summary>
        /// <param name="view">선택 목록을 든 뷰이다.</param>
        /// <param name="allyTree">아군 행동 트리 에셋이다.</param>
        /// <param name="widgetsToDestroy">목록에서 빠진 위젯의 GameObject를 모을 곳이다.</param>
        /// <returns>하나라도 뺐으면 true이다.</returns>
        private static bool PruneView(Component view, BehaviourTreeAsset allyTree, List<GameObject> widgetsToDestroy)
        {
            var serializedView = new SerializedObject(view);
            var definitions = serializedView.FindProperty(DefinitionsPropertyName);
            if (definitions == null || !definitions.isArray)
            {
                Debug.LogError($"[PlayerSelectableUnitsSceneWiring] {view.GetType().Name}에 {DefinitionsPropertyName} 목록이 없다.");
                return false;
            }

            var pairedLists = new List<SerializedProperty>();
            foreach (var propertyName in PairedWidgetPropertyNames)
            {
                var paired = serializedView.FindProperty(propertyName);
                if (paired != null && paired.isArray)
                {
                    pairedLists.Add(paired);
                }
            }

            var removedAny = false;
            for (var index = definitions.arraySize - 1; index >= 0; index--)
            {
                var definition = definitions.GetArrayElementAtIndex(index).objectReferenceValue as UnitDefinition;
                if (definition == null)
                {
                    Debug.LogWarning($"[PlayerSelectableUnitsSceneWiring] {view.name}의 선택 목록 {index}번이 비어 있다. 적이 아니므로 그대로 둔다.");
                    continue;
                }

                if (!TryReadBehaviourTree(definition, out var tree))
                {
                    Debug.LogError(
                        $"[PlayerSelectableUnitsSceneWiring] {view.name}의 선택 목록 {index}번 '{definition.DisplayName}'의 프리팹에서 행동 트리를 읽지 못해 " +
                        "아군인지 판정할 수 없다. 그대로 둔다.");
                    continue;
                }

                if (tree == allyTree)
                {
                    continue;
                }

                Debug.Log($"[PlayerSelectableUnitsSceneWiring] {view.name}의 선택 목록에서 '{definition.DisplayName}'({index}번)을 뺀다. 아군 트리를 들지 않은 프리팹이다.");
                foreach (var paired in pairedLists)
                {
                    if (index >= paired.arraySize)
                    {
                        continue;
                    }

                    if (paired.GetArrayElementAtIndex(index).objectReferenceValue is Component widget)
                    {
                        widgetsToDestroy.Add(widget.gameObject);
                    }

                    RemoveArrayElement(paired, index);
                }

                RemoveArrayElement(definitions, index);
                removedAny = true;
            }

            if (removedAny)
            {
                serializedView.ApplyModifiedPropertiesWithoutUndo();
            }

            return removedAny;
        }

        /// <summary>정의의 프리팹 뿌리 <see cref="TacticalUnit"/>이 든 행동 트리를 읽는다.</summary>
        /// <param name="definition">읽을 유닛 정의이다.</param>
        /// <param name="tree">읽은 행동 트리이며 비어 있을 수 있다.</param>
        /// <returns>
        /// 읽었으면 true이다. 프리팹이 없거나 뿌리에 유닛 컴포넌트가 없거나 트리 필드를 찾지 못하면 false이며, 그때는
        /// 아군인지 판정할 수 없는 것이므로 호출하는 쪽이 그 정의를 건드리지 않아야 한다.
        /// </returns>
        private static bool TryReadBehaviourTree(UnitDefinition definition, out BehaviourTreeAsset tree)
        {
            tree = null;
            if (definition == null || definition.UnitPrefab == null)
            {
                return false;
            }

            if (!definition.UnitPrefab.TryGetComponent<TacticalUnit>(out var unit))
            {
                return false;
            }

            var treeProperty = new SerializedObject(unit).FindProperty(BehaviourTreePropertyName);
            if (treeProperty == null)
            {
                return false;
            }

            tree = treeProperty.objectReferenceValue as BehaviourTreeAsset;
            return true;
        }

        /// <summary>
        /// 직렬화된 배열에서 한 칸을 실제로 없앤다. 참조가 든 칸은 먼저 비운 뒤 지우고, 크기가 줄지 않았으면 한 번 더 지운다.
        /// 참조가 남아 있는 칸을 지우는 첫 호출이 참조만 비우고 칸을 남기는 경우가 있기 때문이다.
        /// </summary>
        /// <param name="array">칸을 없앨 배열이다.</param>
        /// <param name="index">없앨 칸이다.</param>
        private static void RemoveArrayElement(SerializedProperty array, int index)
        {
            var sizeBefore = array.arraySize;
            var element = array.GetArrayElementAtIndex(index);
            if (element.propertyType == SerializedPropertyType.ObjectReference)
            {
                element.objectReferenceValue = null;
            }

            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == sizeBefore)
            {
                array.DeleteArrayElementAtIndex(index);
            }
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
