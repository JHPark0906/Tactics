using HS.Framework.Character;
using HS.Framework.Character.FirstPerson;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>1인칭 컨트롤러의 캐릭터 구성요소 검색과 소유자 연결을 검증한다.</summary>
    public sealed class FirstPersonCharacterControllerTests
    {
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("FirstPersonCharacterControllerTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ControllerIsDiscoveredAsCharacterComponent()
        {
            var controller = _gameObject.AddComponent<FirstPersonCharacterController>();

            Assert.That(controller, Is.InstanceOf<ICharacterComponent>());
        }

        [Test]
        public void CharacterAssemblyConnectsOwnerToController()
        {
            var controller = _gameObject.AddComponent<FirstPersonCharacterController>();
            var character = _gameObject.AddComponent<TestCharacter>();

            character.Initialize();

            Assert.That(controller.Owner, Is.SameAs(character));
        }

        [Test]
        public void OwnerIsNullWithoutCharacterAssembly()
        {
            var controller = _gameObject.AddComponent<FirstPersonCharacterController>();

            Assert.That(controller.Owner, Is.Null);
        }

        [Test]
        public void RepeatedAssemblyKeepsControllerUsable()
        {
            var controller = _gameObject.AddComponent<FirstPersonCharacterController>();
            var character = _gameObject.AddComponent<TestCharacter>();

            character.Initialize();
            controller.Initialize(character);

            Assert.That(controller.Owner, Is.SameAs(character));
            Assert.That(_gameObject.GetComponent<CharacterController>(), Is.Not.Null);
        }

        /// <summary>조립 대상이 되는 최소 캐릭터 구현이다.</summary>
        private sealed class TestCharacter : CharacterBase
        {
        }
    }
}
