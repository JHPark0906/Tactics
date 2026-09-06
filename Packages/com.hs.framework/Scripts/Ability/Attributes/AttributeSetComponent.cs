using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// GameObject 하나가 소유하는 어트리뷰트 집합을 노출하는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 계산과 규칙은 모두 <see cref="AttributeSet"/>이 맡고 이 컴포넌트는 그 집합을 소유해
    /// 씬의 다른 컴포넌트가 찾아 쓸 수 있게 하는 역할만 한다. 그래서 규칙은 씬 없이 검증할 수 있다.
    /// </para>
    /// <para>
    /// 인스펙터에서 어트리뷰트 묶음을 연결하거나 시작 어트리뷰트를 나열하고 기본값을 덮어쓸 수 있다.
    /// 어빌리티 시스템이 없는 액터(파괴 가능한 소품 같은 것)가 어트리뷰트를 갖추는 길이 이것이다.
    /// 상한으로 쓰이는 어트리뷰트도 함께 나열해야 상한이 실제로 적용된다.
    /// 나열 순서는 결과에 영향을 주지 않는다. 상한이 나중에 추가되어도 그 시점에 상한이 다시 반영된다.
    /// </para>
    /// <para>
    /// <b>파괴된 뒤에는 집합이 되살아나지 않는다.</b> 집합은 첫 사용 때 만드는 지연 조립이므로, 파괴 뒤의
    /// 접근이 그 조립을 다시 타면 죽은 오브젝트가 시작값 그대로의 새 집합을 갖게 된다. 거기 얹히는 필터와
    /// 수정자, 구독은 아무도 정리하지 않는다. 그래서 파괴 뒤에는 새 집합을 만들지 않고 파괴 전에 쓰던
    /// 집합을 닫힌 채 돌려주며, 처음 한 번 경고를 남긴다. 파괴 뒤의 접근은 파괴된 오브젝트를 아직 붙들고
    /// 있는 쪽이 있다는 뜻이고, 그것이 드러나야 고칠 수 있다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AttributeSetComponent : MonoBehaviour
    {
        [Tooltip("시작할 때 집합에 갖출 어트리뷰트 묶음이다. 아래 개별 항목보다 먼저 적용된다.")]
        [SerializeField]
        private List<AttributeSetDefinition> attributeSets = new();

        [Tooltip("묶음 위에 얹을 개별 시작 어트리뷰트 목록이다.")]
        [SerializeField]
        private List<StartingAttribute> startingAttributes = new();

        private AttributeSet _attributes;
        private bool _isDestroyed;
        private bool _hasWarnedAccessAfterDestroy;

        /// <summary>
        /// 이 오브젝트가 소유한 어트리뷰트 집합이며 첫 사용 시점에 만들어진다.
        /// 파괴된 뒤에는 새로 만들지 않고 닫힌 집합을 돌려준다.
        /// </summary>
        /// <remarks>
        /// 닫힌 집합은 값을 읽으면 마지막 값을 답하고, 변화 스트림은 닫혀 있어 값이 바뀌어도 아무것도
        /// 알리지 않는다. 파괴 전에 한 번도 만들지 않았으면 시작 어트리뷰트를 넣지 않은 빈 집합을 닫아
        /// 돌려준다. null은 돌려주지 않는다 — 부르는 쪽이 null 검사 없이 값을 읽는 것이 이 컴포넌트의
        /// 계약이기 때문이다.
        /// </remarks>
        public AttributeSet Attributes
        {
            get
            {
                if (_isDestroyed)
                {
                    return GetAttributesAfterDestroy();
                }

                if (_attributes == null)
                {
                    _attributes = new AttributeSet();
                    ApplyStartingAttributes();
                }

                return _attributes;
            }
        }

        private void Awake()
        {
            // 다른 컴포넌트가 접근하기 전에 집합을 준비해 둔다.
            _ = Attributes;
        }

        /// <summary>
        /// 집합의 변화 스트림을 닫는다. 참조는 지우지 않는다. 지우면 파괴된 뒤의 접근이 새 집합을 만든다.
        /// </summary>
        private void OnDestroy()
        {
            _isDestroyed = true;
            _attributes?.Dispose();
        }

        /// <summary>파괴 뒤의 접근에 닫힌 집합을 돌려주고, 처음 한 번 경고를 남긴다.</summary>
        /// <returns>파괴 전에 쓰던 집합이며, 없었으면 닫힌 빈 집합이다.</returns>
        private AttributeSet GetAttributesAfterDestroy()
        {
            if (_attributes == null)
            {
                _attributes = new AttributeSet();
                _attributes.Dispose();
            }

            if (!_hasWarnedAccessAfterDestroy)
            {
                _hasWarnedAccessAfterDestroy = true;
                Debug.LogWarning(
                    $"[AttributeSetComponent] {name}이 파괴된 뒤에 어트리뷰트 집합에 접근했다. " +
                    "새 집합을 만들지 않고 닫힌 집합을 돌려준다. 파괴된 오브젝트를 아직 붙들고 있는 쪽이 있다.",
                    this);
            }

            return _attributes;
        }

        /// <summary>인스펙터에 연결한 묶음과 나열한 시작 어트리뷰트를 집합에 넣는다.</summary>
        private void ApplyStartingAttributes()
        {
            if (attributeSets != null)
            {
                foreach (var attributeSet in attributeSets)
                {
                    if (attributeSet != null)
                    {
                        attributeSet.ApplyTo(_attributes);
                    }
                }
            }

            if (startingAttributes == null)
            {
                return;
            }

            for (var index = 0; index < startingAttributes.Count; index++)
            {
                var starting = startingAttributes[index];
                if (starting.Definition == null)
                {
                    continue;
                }

                _attributes.AddAttribute(
                    starting.Definition,
                    starting.OverridesBaseValue ? starting.BaseValue : null);
            }
        }

        /// <summary>인스펙터에서 시작 어트리뷰트 하나를 지정하는 항목이다.</summary>
        [Serializable]
        public struct StartingAttribute
        {
            [Tooltip("집합에 넣을 어트리뷰트 정의이다.")]
            [SerializeField]
            private AttributeDefinition definition;

            [Tooltip("정의의 기본값 대신 아래 값을 사용할지 여부이다.")]
            [SerializeField]
            private bool overridesBaseValue;

            [Tooltip("정의의 기본값을 덮어쓸 때 사용할 시작 기본값이다.")]
            [SerializeField]
            private float baseValue;

            /// <summary>집합에 넣을 어트리뷰트 정의이다.</summary>
            public AttributeDefinition Definition => definition;

            /// <summary>정의의 기본값 대신 지정한 값을 사용할지 여부이다.</summary>
            public bool OverridesBaseValue => overridesBaseValue;

            /// <summary>정의의 기본값을 덮어쓸 때 사용할 시작 기본값이다.</summary>
            public float BaseValue => baseValue;
        }
    }
}
