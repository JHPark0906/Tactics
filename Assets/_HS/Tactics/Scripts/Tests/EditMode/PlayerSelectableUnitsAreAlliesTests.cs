using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using HS.Tactics.Placement;
using HS.Tactics.UI;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 플레이어가 세울 수 있는 유닛 목록(Level1의 배치 창, MainMenu의 영웅 배치 패널)에 아군 트리 프리팹만 있는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 아군인지는 정의의 프리팹 뿌리 <see cref="TacticalUnit"/>이 든 행동 트리로 가른다. 정의의 기본 진영은 두 정의 모두 비어
    /// 있고 다른 표지도 없으며, 아군과 적을 실제로 다르게 움직이게 하는 것이 그 트리 하나이기 때문이다. 두 정의의 트리를
    /// 먼저 직접 읽어 하나는 아군 트리, 하나는 적 트리인지 확인한다 — 「아군이 아니다」만 보면 프리팹이 빠져도 통과한다.
    /// </para>
    /// <para>
    /// 목록이 비어 있어도 헛되이 통과하지 않도록 하나 이상 있는지 함께 본다. 씬은 덧붙여 열고 끝나면 닫아 열려 있던 씬을
    /// 그대로 둔다.
    /// </para>
    /// </remarks>
    public sealed class PlayerSelectableUnitsAreAlliesTests
    {
        private const string Level1ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string MainMenuScenePath = "Assets/_HS/Tactics/Scenes/MainMenu.unity";
        private const string AllyBehaviourTreePath = "Assets/_HS/Tactics/AI/UnitBehaviourTree.asset";
        private const string EnemyBehaviourTreePath = "Assets/_HS/Tactics/AI/EnemyBehaviourTree.asset";
        private const string RiflemanDefinitionPath = "Assets/_HS/Tactics/Definitions/Rifleman.asset";
        private const string GruntDefinitionPath = "Assets/_HS/Tactics/Definitions/Grunt.asset";

        private const string DefinitionsPropertyName = "selectableUnitDefinitions";
        private const string ButtonsPropertyName = "unitSelectionButtons";
        private const string PortraitsPropertyName = "unitPortraitImages";
        private const string BehaviourTreePropertyName = "behaviourTree";

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
        public void TheTwoDefinitionsCarryTheAllyAndTheEnemyTree()
        {
            var allyTree = LoadTree(AllyBehaviourTreePath);
            var enemyTree = LoadTree(EnemyBehaviourTreePath);
            Assert.That(enemyTree, Is.Not.SameAs(allyTree), "아군 트리와 적 트리가 같은 에셋이면 규칙이 아무것도 가르지 못한다.");
            var rifleman = LoadDefinition(RiflemanDefinitionPath);
            var grunt = LoadDefinition(GruntDefinitionPath);

            Assert.That(ReadBehaviourTree(rifleman), Is.SameAs(allyTree), "Rifleman 프리팹은 아군 트리를 들고 있어야 한다.");
            Assert.That(ReadBehaviourTree(grunt), Is.SameAs(enemyTree), "Grunt 프리팹은 적 트리를 들고 있어야 한다. 그래야 목록 검사가 둘을 가른다.");
        }

        [Test]
        public void Level1PlacementWindowOffersOnlyAllyUnits()
        {
            AssertSelectionOffersOnlyAllies<PlacementWindowView>(Level1ScenePath, ButtonsPropertyName);
        }

        [Test]
        public void MainMenuHeroPanelOffersOnlyAllyUnits()
        {
            AssertSelectionOffersOnlyAllies<HeroPlacementPanelView>(MainMenuScenePath, ButtonsPropertyName, PortraitsPropertyName);
        }

        /// <summary>씬을 덧붙여 열고 뷰의 선택 목록이 비지 않았고 전부 아군 트리를 들며, 짝지어진 위젯 목록과 길이가 같은지 단언한다.</summary>
        /// <typeparam name="TView">선택 목록을 든 뷰 컴포넌트 형식이다.</typeparam>
        /// <param name="scenePath">열 씬의 경로이다.</param>
        /// <param name="pairedPropertyNames">정의 목록과 같은 길이여야 하는 위젯 목록의 필드 이름이다.</param>
        private void AssertSelectionOffersOnlyAllies<TView>(string scenePath, params string[] pairedPropertyNames)
            where TView : Component
        {
            var allyTree = LoadTree(AllyBehaviourTreePath);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            _openedScenes.Add(scene);

            var view = FindInScene<TView>(scene);
            Assert.That(view, Is.Not.Null, $"{scenePath}에 {typeof(TView).Name}이 있어야 한다.");

            var serializedView = new SerializedObject(view);
            var definitions = serializedView.FindProperty(DefinitionsPropertyName);
            Assert.That(definitions, Is.Not.Null, $"{typeof(TView).Name}에 {DefinitionsPropertyName} 목록이 있어야 한다.");
            Assert.That(definitions.arraySize, Is.GreaterThan(0), "세울 수 있는 유닛이 하나도 없으면 플레이어가 아무것도 세우지 못한다.");

            for (var index = 0; index < definitions.arraySize; index++)
            {
                var definition = definitions.GetArrayElementAtIndex(index).objectReferenceValue as UnitDefinition;
                Assert.That(definition, Is.Not.Null, $"{scenePath}의 선택 목록 {index}번이 비어 있다.");
                Assert.That(
                    ReadBehaviourTree(definition),
                    Is.SameAs(allyTree),
                    $"{scenePath}의 선택 목록 {index}번 '{definition.DisplayName}'은 아군 트리 프리팹이 아니다. 플레이어가 적을 아군으로 세울 수 있게 된다.");
            }

            foreach (var pairedPropertyName in pairedPropertyNames)
            {
                var paired = serializedView.FindProperty(pairedPropertyName);
                Assert.That(paired, Is.Not.Null, $"{typeof(TView).Name}에 {pairedPropertyName} 목록이 있어야 한다.");
                Assert.That(
                    paired.arraySize,
                    Is.EqualTo(definitions.arraySize),
                    $"{scenePath}의 {pairedPropertyName}은 정의 목록과 같은 길이여야 한다. 인덱스로 짝지어지므로 길이가 다르면 버튼이 다른 정의를 가리킨다.");
            }
        }

        /// <summary>
        /// 정의의 프리팹 뿌리 <see cref="TacticalUnit"/>이 든 행동 트리를 읽는다. 프리팹이나 유닛이나 트리 필드가 없으면
        /// 읽을 수 없는 것이므로 그 자리에서 실패한다 — 「없다」가 「아군이 아니다」로 읽히면 안 된다.
        /// </summary>
        /// <param name="definition">읽을 유닛 정의이다.</param>
        /// <returns>프리팹이 든 행동 트리이며 비어 있으면 null이다.</returns>
        private static BehaviourTreeAsset ReadBehaviourTree(UnitDefinition definition)
        {
            Assert.That(definition.UnitPrefab, Is.Not.Null, $"'{definition.DisplayName}' 정의에 프리팹이 없어 트리를 읽을 수 없다.");
            Assert.That(
                definition.UnitPrefab.TryGetComponent<TacticalUnit>(out var unit),
                Is.True,
                $"'{definition.DisplayName}' 프리팹 뿌리에 TacticalUnit이 없어 트리를 읽을 수 없다.");

            var treeProperty = new SerializedObject(unit).FindProperty(BehaviourTreePropertyName);
            Assert.That(
                treeProperty,
                Is.Not.Null,
                $"TacticalUnit에 {BehaviourTreePropertyName} 필드가 있어야 한다. 이름이 바뀌었으면 이 검사와 에디터 메뉴를 함께 고쳐야 한다.");
            return treeProperty.objectReferenceValue as BehaviourTreeAsset;
        }

        private static BehaviourTreeAsset LoadTree(string path)
        {
            var tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(path);
            Assert.That(tree, Is.Not.Null, $"{path}를 읽지 못했다.");
            return tree;
        }

        private static UnitDefinition LoadDefinition(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            Assert.That(definition, Is.Not.Null, $"{path}를 읽지 못했다.");
            return definition;
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
