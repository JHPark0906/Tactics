using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>캐릭터 구성 요소 초기화 수명주기를 검증한다.</summary>
    public sealed class CharacterBaseTests
    {
        [Test]
        public void InitializeConfiguresCharacterComponentsExactlyOnce()
        {
            var gameObject = new GameObject("CharacterBaseTests");
            try
            {
                var component = gameObject.AddComponent<TestCharacterComponent>();
                var character = gameObject.AddComponent<TestCharacter>();
                character.Initialize();

                Assert.That(character.IsInitialized, Is.True);
                Assert.That(component.InitializeCount, Is.EqualTo(1));
                Assert.That(component.Owner, Is.SameAs(character));

                character.Initialize();

                Assert.That(component.InitializeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class TestCharacter : CharacterBase
        {
        }

        private sealed class TestCharacterComponent : MonoBehaviour, ICharacterComponent
        {
            public int InitializeCount { get; private set; }

            public CharacterBase Owner { get; private set; }

            public void Initialize(CharacterBase characterBase)
            {
                Owner = characterBase;
                InitializeCount++;
            }
        }
    }
}
