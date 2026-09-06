using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// 한 종류의 액터가 갖출 어트리뷰트 묶음을 데이터로 정의한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>어떤 어트리뷰트를 갖추는가는 액터의 종류가 정한다.</b> 경험치를 쌓는 액터와 쌓지 않는 액터는
    /// 어트리뷰트 목록부터 다르며, 그 차이는 클래스가 아니라 데이터로 표현한다. 이 에셋 하나가 한 종류의
    /// 목록이고, 같은 목록을 어빌리티 구성이 다른 여러 어빌리티 집합이 공유할 수 있다.
    /// </para>
    /// <para>
    /// <b>기본값은 정의의 것이 기본이고 항목이 덮어쓸 수 있다.</b> 덮어쓰지 않는 항목은 어트리뷰트 정의에 적힌
    /// 기본값으로 시작한다. 이미 집합에 있는 어트리뷰트는 덮어쓰기를 지정한 항목만 기본값을 바꾸고,
    /// 지정하지 않은 항목은 있는 값을 존중한다.
    /// </para>
    /// <para>
    /// 상한 어트리뷰트가 이 묶음 안에 함께 있어야 상한이 실제로 걸린다. 나열 순서는 결과에 영향을 주지 않는다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Ability/Attribute Set Definition", fileName = "AttributeSetDefinition")]
    public sealed class AttributeSetDefinition : ScriptableObject
    {
        [Tooltip("이 묶음에 들어갈 어트리뷰트와 시작 기본값이다.")]
        [SerializeField]
        private List<Entry> entries = new();

        /// <summary>이 묶음의 항목 목록이다.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>
        /// 이 묶음의 어트리뷰트를 집합에 갖춘다. 없는 것은 더하고, 있는 것은 덮어쓰기를 지정한 항목만 기본값을 바꾼다.
        /// </summary>
        /// <param name="attributes">갖출 대상 집합이다.</param>
        /// <returns>새로 더한 어트리뷰트 수이다.</returns>
        /// <exception cref="ArgumentNullException">집합이 null이면 발생한다.</exception>
        public int ApplyTo(AttributeSet attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            var addedCount = 0;
            foreach (var entry in entries)
            {
                if (entry.Definition == null)
                {
                    continue;
                }

                if (attributes.AddAttribute(entry.Definition, entry.OverridesBaseValue ? entry.BaseValue : null))
                {
                    addedCount++;
                }
                else if (entry.OverridesBaseValue)
                {
                    attributes.SetBaseValue(entry.Definition, entry.BaseValue);
                }
            }

            return addedCount;
        }

        /// <summary>지정한 어트리뷰트가 이 묶음에 있는지 확인한다.</summary>
        /// <param name="definition">확인할 어트리뷰트 정의이다.</param>
        /// <returns>있으면 true이다.</returns>
        public bool Contains(AttributeDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            foreach (var entry in entries)
            {
                if (entry.Definition == definition)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 묶음이 적용 가능한 형태인지 검사한다. 빈 항목과 같은 어트리뷰트의 중복을 문제로 본다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 첫 원인이며 성공하면 null이다.</param>
        /// <returns>문제가 없으면 true이다.</returns>
        public bool TryValidate(out string errorMessage)
        {
            var seen = new HashSet<AttributeDefinition>();
            for (var index = 0; index < entries.Count; index++)
            {
                var definition = entries[index].Definition;
                if (definition == null)
                {
                    errorMessage = $"{index}번 항목에 어트리뷰트가 지정되지 않았다.";
                    return false;
                }

                if (!seen.Add(definition))
                {
                    errorMessage = $"어트리뷰트 '{definition.Id}'이 두 번 나열되어 있다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 묶음을 만든다.
        /// </summary>
        /// <param name="definitionEntries">묶음에 넣을 항목이다.</param>
        /// <returns>만든 묶음이다.</returns>
        public static AttributeSetDefinition CreateRuntime(IEnumerable<Entry> definitionEntries)
        {
            var set = CreateInstance<AttributeSetDefinition>();
            if (definitionEntries != null)
            {
                set.entries.AddRange(definitionEntries);
            }

            return set;
        }

        /// <summary>묶음의 항목 하나이며, 어트리뷰트 정의와 시작 기본값을 담는다.</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("갖출 어트리뷰트 정의이다.")]
            [SerializeField]
            private AttributeDefinition definition;

            [Tooltip("정의의 기본값 대신 아래 값으로 시작할지 여부이다.")]
            [SerializeField]
            private bool overridesBaseValue;

            [Tooltip("정의의 기본값을 덮어쓸 때 사용할 시작 기본값이다.")]
            [SerializeField]
            private float baseValue;

            /// <summary>정의의 기본값으로 시작하는 항목을 생성한다.</summary>
            /// <param name="definition">갖출 어트리뷰트 정의이다.</param>
            public Entry(AttributeDefinition definition)
            {
                this.definition = definition;
                overridesBaseValue = false;
                baseValue = 0f;
            }

            /// <summary>지정한 기본값으로 시작하는 항목을 생성한다.</summary>
            /// <param name="definition">갖출 어트리뷰트 정의이다.</param>
            /// <param name="baseValue">시작 기본값이다.</param>
            public Entry(AttributeDefinition definition, float baseValue)
            {
                this.definition = definition;
                overridesBaseValue = true;
                this.baseValue = baseValue;
            }

            /// <summary>갖출 어트리뷰트 정의이다.</summary>
            public AttributeDefinition Definition => definition;

            /// <summary>정의의 기본값 대신 지정한 값으로 시작하는지 여부이다.</summary>
            public bool OverridesBaseValue => overridesBaseValue;

            /// <summary>정의의 기본값을 덮어쓸 때 사용할 시작 기본값이다.</summary>
            public float BaseValue => baseValue;
        }
    }
}
