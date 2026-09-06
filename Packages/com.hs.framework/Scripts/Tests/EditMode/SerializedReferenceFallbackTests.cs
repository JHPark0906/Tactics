using System.Reflection;
using HS.Framework.Character.FirstPerson;
using HS.Framework.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 인스펙터 참조를 비워 둔 컴포넌트가 깨어날 때 같은 오브젝트에서 스스로 찾는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 에디터에서 직렬화된 오브젝트 참조를 비우면 C# null 이 아니라 Unity 만 null 로 보는 가짜 null 이 된다.
    /// C# null 만 보는 <c>??=</c> 는 그 참조에 대입을 건너뛰어 폴백이 돌지 않는다.
    /// 캐릭터 컨트롤러 참조는 <see cref="SerializedObject"/>로 비워 그 상태를 만든다.
    /// MonoBehaviour 참조는 Unity가 직렬화 갱신 중 다시 만들 수 있어, 상호작용 입력 검사는
    /// 파괴된 구성요소의 관리 참조를 직접 넣어 같은 null 비교 조건을 안정적으로 만든다.
    /// </para>
    /// </remarks>
    public sealed class SerializedReferenceFallbackTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Host");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void InteractionInputDriverFindsTheControllerOnItsObject()
        {
            var controller = _host.AddComponent<InteractionController>();
            var driver = _host.AddComponent<InteractionInputDriver>();
            var obsoleteHost = new GameObject("Obsolete controller");
            try
            {
                var obsoleteController = obsoleteHost.AddComponent<InteractionController>();
                Object.DestroyImmediate(obsoleteHost);

                // 파괴된 네이티브 구성요소의 관리 참조는 살아 있다. 직렬화 층을 다시 통과시키지
                // 않고 넣어야 에디터 버전에 따른 가짜 null 생성·복원 동작에 기대지 않는다.
                var field = typeof(InteractionInputDriver).GetField(
                    "interactionController", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(driver, obsoleteController);
                var raw = field.GetValue(driver);
                Assert.That(ReferenceEquals(raw, null), Is.False, "C# null이면 ??= 회귀를 검사할 수 없다.");
                Assert.That(raw is Object unityObject && unityObject == null, Is.True,
                    "파괴된 Unity 구성요소는 Unity의 null 비교에서 비어 있어야 한다.");

                InvokeAwake(driver);

                Assert.That(field.GetValue(driver), Is.SameAs(controller));
            }
            finally
            {
                if (obsoleteHost != null)
                {
                    Object.DestroyImmediate(obsoleteHost);
                }
            }
        }

        [Test]
        public void FirstPersonCharacterControllerReusesTheCharacterControllerOnItsObject()
        {
            var characterController = _host.AddComponent<CharacterController>();
            var movement = _host.AddComponent<FirstPersonCharacterController>();
            ClearInspectorReference(movement, "characterController", characterController);
            ClearInspectorReference(movement, "movementReference", _host.transform);

            InvokeAwake(movement);

            Assert.That(ReadReference(movement, "characterController"), Is.EqualTo(characterController));
            Assert.That(
                _host.GetComponents<CharacterController>(),
                Has.Length.EqualTo(1),
                "있는 캐릭터 컨트롤러를 못 보고 하나를 더 붙이면 안 된다.");
            Assert.That(ReadReference(movement, "movementReference"), Is.EqualTo(_host.transform));
        }

        /// <summary>
        /// 참조를 한 번 채웠다가 비워 인스펙터에서 비운 것과 같은 상태를 만든다. 두 번 적용하는 것은 값이 바뀌어야
        /// 직렬화 층이 관리 객체로 옮겨 적기 때문이다. 비운 뒤에는 그 참조가 정말 가짜 null 인지 확인한다.
        /// </summary>
        private static void ClearInspectorReference(Component component, string fieldName, Object placeholder)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(fieldName).objectReferenceValue = placeholder;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.FindProperty(fieldName).objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            var value = field.GetValue(component);
            Assume.That(
                value is Object unityObject && unityObject == null && !ReferenceEquals(value, null),
                Is.True,
                $"무대 확인: 비운 {fieldName} 이 가짜 null 이어야 이 검사가 뜻을 갖는다.");
        }

        private static Object ReadReference(Component component, string fieldName)
        {
            return new SerializedObject(component).FindProperty(fieldName).objectReferenceValue;
        }

        private static void InvokeAwake(Component component)
        {
            var awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, $"{component.GetType().Name} 에 Awake 가 없다.");
            awake.Invoke(component, null);
        }
    }
}
