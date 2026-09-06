using System;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Framework.Character
{
    /// <summary>플레이어와 NPC가 공통으로 상속하는 캐릭터 오케스트레이터이다.</summary>
    [DisallowMultipleComponent]
    public abstract class CharacterBase : MonoBehaviour
    {
        private bool _isInitialized;

        protected ICharacterComponent[] CharacterComponents { get; private set; } =
            Array.Empty<ICharacterComponent>();

        /// <summary>캐릭터 구성요소 초기화가 완료되었는지 여부이다.</summary>
        public bool IsInitialized => _isInitialized;

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>캐릭터 GameObject에 연결된 구성요소를 한 번만 초기화한다.</summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            CharacterComponents = GetComponents<ICharacterComponent>();
            foreach (var component in CharacterComponents)
            {
                component.Initialize(this);
            }

            _isInitialized = true;
        }

        /// <summary>요청한 타입의 캐릭터 구성요소를 반환한다.</summary>
        public bool TryGetCharacterComponent<T>(out T component) where T : class, ICharacterComponent
        {
            var componentInstance = GetComponent<T>();
            component = componentInstance;
            return componentInstance != null;
        }
    }
}
