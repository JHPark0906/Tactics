using System.Collections.Generic;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>태그 이름을 모아 두는 카탈로그와 태그 보유 컴포넌트를 검증한다.</summary>
    public sealed class GameplayTagAuthoringTests
    {
        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
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
        public void CatalogFindsRegisteredNames()
        {
            var catalog = CreateCatalog("State.Slowed", "Cooldown.Attack");

            Assert.That(catalog.TryGetTag("State.Slowed", out var tag), Is.True);
            Assert.That(tag.Name, Is.EqualTo("State.Slowed"));
            Assert.That(catalog.Count, Is.EqualTo(2));
        }

        [Test]
        public void CatalogLookupIgnoresCaseAndSurroundingWhitespace()
        {
            var catalog = CreateCatalog("State.Slowed");

            Assert.That(catalog.TryGetTag("  state.slowed ", out _), Is.True);
        }

        [Test]
        public void CatalogRejectsUnregisteredNamesEvenIfWellFormed()
        {
            var catalog = CreateCatalog("State.Slowed");

            Assert.That(catalog.TryGetTag("State.Stunned", out var tag), Is.False);
            Assert.That(tag.IsValid, Is.False);
            Assert.That(catalog.Contains(GameplayTag.Parse("State.Stunned")), Is.False);
        }

        [Test]
        public void CatalogValidationPassesForUniqueWellFormedNames()
        {
            var catalog = CreateCatalog("State.Slowed", "Cooldown.Attack");

            Assert.That(catalog.TryValidate(out var errorMessage), Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void CatalogValidationDetectsMalformedName()
        {
            var catalog = CreateCatalog("State..Slowed");

            Assert.That(catalog.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void CatalogValidationDetectsDuplicateNameAcrossCases()
        {
            var catalog = CreateCatalog("State.Slowed", "state.slowed");

            Assert.That(catalog.TryValidate(out var errorMessage), Is.False);
            Assert.That(
                errorMessage,
                Does.Contain("State.Slowed").And.Contain("state.slowed"),
                "사용자가 목록에서 두 항목을 다 찾을 수 있도록 작성한 그대로 보여 주어야 한다.");
        }

        [Test]
        public void ComponentStartsWithAnEmptyContainer()
        {
            var component = CreateComponent();

            Assert.That(component.Container, Is.Not.Null);
            Assert.That(component.Container.DistinctTagCount, Is.Zero);
        }

        [Test]
        public void ComponentAppliesInitialTagsOnlyOnce()
        {
            var component = CreateComponent();
            SetInitialTagNames(component, "Unit.Infantry", "State.Ready");

            component.ApplyInitialTags();
            component.ApplyInitialTags();

            Assert.That(component.HasAppliedInitialTags, Is.True);
            Assert.That(component.Container.DistinctTagCount, Is.EqualTo(2));
            Assert.That(component.Container.GetCount(GameplayTag.Parse("Unit.Infantry")), Is.EqualTo(1));
            Assert.That(component.Container.HasTag(GameplayTag.Parse("Unit")), Is.True);
        }

        [Test]
        public void ComponentSkipsMalformedInitialTags()
        {
            var component = CreateComponent();
            SetInitialTagNames(component, "Unit.Infantry", "State..Ready");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("State..Ready"));

            component.ApplyInitialTags();

            Assert.That(component.Container.DistinctTagCount, Is.EqualTo(1));
        }

        /// <summary>지정한 이름을 담은 비저장 카탈로그를 만든다.</summary>
        /// <param name="names">등록할 태그 이름이다.</param>
        private GameplayTagCatalog CreateCatalog(params string[] names)
        {
            var catalog = GameplayTagCatalog.CreateRuntime(names);
            _createdObjects.Add(catalog);
            return catalog;
        }

        /// <summary>
        /// 태그 보유 컴포넌트를 붙인 오브젝트를 만든다.
        /// 오브젝트를 비활성 상태로 두어 Awake가 시작 태그를 먼저 적용해 버리지 않게 하고,
        /// 테스트가 원하는 시점에 공개 진입점으로 적용 경로를 태운다.
        /// </summary>
        private GameplayTagComponent CreateComponent()
        {
            var componentObject = new GameObject("GameplayTagComponent");
            _createdObjects.Add(componentObject);
            componentObject.SetActive(false);
            return componentObject.AddComponent<GameplayTagComponent>();
        }

        /// <summary>
        /// 인스펙터에서 지정하는 시작 태그 목록을 직렬화 필드에 직접 넣는다.
        /// EditMode에서는 프리팹을 거치지 않으므로 직렬화 경로를 그대로 태우기 위해 이 방법을 쓴다.
        /// </summary>
        /// <param name="component">설정할 컴포넌트이다.</param>
        /// <param name="names">시작 태그 이름이다.</param>
        private static void SetInitialTagNames(GameplayTagComponent component, params string[] names)
        {
            var serializedComponent = new UnityEditor.SerializedObject(component);
            var namesProperty = serializedComponent.FindProperty("initialTagNames");
            namesProperty.arraySize = names.Length;
            for (var index = 0; index < names.Length; index++)
            {
                namesProperty.GetArrayElementAtIndex(index).stringValue = names[index];
            }

            serializedComponent.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
