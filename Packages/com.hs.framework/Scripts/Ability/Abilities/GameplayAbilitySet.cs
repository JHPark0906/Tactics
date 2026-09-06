using System;
using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 한 액터에게 한꺼번에 부여할 어빌리티와 시작 어트리뷰트, 시작 태그를 묶은 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>"무엇을 할 수 있는가"만 담는다.</b> 어빌리티를 <b>무슨 순서로 시도하는가</b>는 여기 담지 않는다.
    /// 둘은 서로 다른 축이라, 같은 어빌리티를 가진 두 액터가 서로 다른 순서를 가질 수 있기 때문이다.
    /// 예컨대 저격수와 돌격병이 똑같이 엄폐 어빌리티를 갖더라도 그것을 먼저 시도할지 나중에 시도할지는 반대일 수 있다.
    /// 두 개념을 한 목록에 합치면 "엄폐를 주되 우선순위는 낮게"를 표현할 방법이 사라진다.
    /// 순서가 필요하면 이 집합과 별개의 자료로 두어야 한다.
    /// </para>
    /// <para>
    /// 어트리뷰트는 <see cref="AttributeSetDefinition"/> 묶음으로 갖추는 것이 기본이다. 어떤 어트리뷰트를 갖추는가는
    /// 액터의 종류가 정하는 것이라 어빌리티 구성과 별개의 데이터이며, 같은 묶음을 여러 집합이 공유한다.
    /// 인라인 시작 어트리뷰트는 그 위에 얹는 개별 예외이다. 둘 다 집합에 없으면 추가하고, 이미 있으면 기본값만 덮어쓴다.
    /// 시작 태그는 부여 횟수 1로 들어간다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Ability/Gameplay Ability Set", fileName = "GameplayAbilitySet")]
    public sealed class GameplayAbilitySet : ScriptableObject
    {
        [Tooltip("이 액터에게 부여할 어빌리티 목록이다. 시도 순서는 여기서 정하지 않는다.")]
        [SerializeField]
        private List<GameplayAbilityDefinition> abilities = new();

        [Tooltip("액터가 시작할 때 갖출 어트리뷰트 묶음이다. 어빌리티보다 먼저, 아래 인라인 항목보다 먼저 적용된다.")]
        [SerializeField]
        private List<AttributeSetDefinition> attributeSets = new();

        [Tooltip("묶음 위에 얹을 개별 시작 어트리뷰트와 그 기본값이다.")]
        [SerializeField]
        private List<StartingAttribute> startingAttributes = new();

        [Tooltip("액터가 시작할 때 가질 태그 이름이다. 예: Unit.Infantry")]
        [SerializeField]
        private List<string> startingTagNames = new();

        /// <summary>부여할 어빌리티 목록이다.</summary>
        public IReadOnlyList<GameplayAbilityDefinition> Abilities => abilities;

        /// <summary>시작할 때 갖출 어트리뷰트 묶음 목록이다.</summary>
        public IReadOnlyList<AttributeSetDefinition> AttributeSets => attributeSets;

        /// <summary>묶음 위에 얹는 개별 시작 어트리뷰트 목록이다.</summary>
        public IReadOnlyList<StartingAttribute> StartingAttributes => startingAttributes;

        /// <summary>시작 태그 이름 목록이다.</summary>
        public IReadOnlyList<string> StartingTagNames => startingTagNames;

        /// <summary>
        /// 이 집합의 내용을 어빌리티 시스템에 적용한다.
        /// 어트리뷰트와 태그를 먼저 갖춘 뒤 어빌리티를 부여하므로,
        /// 필요 태그를 시작 태그로 가진 어빌리티도 곧바로 활성화 조건을 만족할 수 있다.
        /// </summary>
        /// <remarks>
        /// 적용하기 전에 집합 전체를 검증하고, 실패하면 오류를 남기고 아무것도 적용하지 않는다.
        /// 어빌리티 하나가 잘못됐다고 나머지만 들이면 액터가 반쯤 갖춰진 채 오류 없이 굴러가고,
        /// 무엇이 빠졌는지는 나중에 증상으로만 드러난다. 집합은 한 벌로 작성된 것이므로 한 벌로 거부한다.
        /// </remarks>
        /// <param name="system">적용할 어빌리티 시스템이다.</param>
        /// <returns>실제로 부여한 어빌리티 수이다.</returns>
        /// <exception cref="ArgumentNullException">시스템이 null이면 발생한다.</exception>
        public int GrantTo(GameplayAbilitySystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (!TryValidate(out var validationError))
            {
                Debug.LogError($"[GameplayAbilitySet] {name}이 검증에 실패해 아무것도 부여하지 않았다: {validationError}", this);
                return 0;
            }

            foreach (var attributeSet in attributeSets)
            {
                attributeSet.ApplyTo(system.Attributes);
            }

            foreach (var startingAttribute in startingAttributes)
            {
                if (startingAttribute.Definition == null)
                {
                    continue;
                }

                if (!system.Attributes.AddAttribute(startingAttribute.Definition, startingAttribute.BaseValue))
                {
                    system.Attributes.SetBaseValue(startingAttribute.Definition, startingAttribute.BaseValue);
                }
            }

            foreach (var tagName in startingTagNames)
            {
                if (GameplayTag.TryParse(tagName, out var tag))
                {
                    system.Tags.AddTag(tag);
                }
            }

            var grantedCount = 0;
            foreach (var definition in abilities)
            {
                if (definition != null && system.GrantAbility(definition) != null)
                {
                    grantedCount++;
                }
            }

            return grantedCount;
        }

        /// <summary>
        /// 집합이 적용 가능한 형태인지 검사한다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 첫 원인이며 성공하면 null이다.</param>
        /// <returns>문제가 없으면 true이다.</returns>
        public bool TryValidate(out string errorMessage)
        {
            for (var index = 0; index < attributeSets.Count; index++)
            {
                if (attributeSets[index] == null)
                {
                    errorMessage = $"{index}번 어트리뷰트 묶음이 비어 있다.";
                    return false;
                }

                if (!attributeSets[index].TryValidate(out var attributeSetError))
                {
                    errorMessage = $"{attributeSets[index].name}: {attributeSetError}";
                    return false;
                }
            }

            // 중복을 알릴 때 두 항목을 다 보여 주려고 먼저 등록된 이름을 작성한 그대로 기억해 둔다.
            var seenTags = new Dictionary<GameplayTag, string>();
            for (var index = 0; index < abilities.Count; index++)
            {
                var definition = abilities[index];
                if (definition == null)
                {
                    errorMessage = $"{index}번 어빌리티가 비어 있다.";
                    return false;
                }

                if (!definition.TryValidate(out var definitionError))
                {
                    errorMessage = $"{definition.name}: {definitionError}";
                    return false;
                }

                if (seenTags.TryGetValue(definition.AbilityTag, out var registeredName))
                {
                    errorMessage = string.Equals(registeredName, definition.AbilityTag.Name, StringComparison.Ordinal)
                        ? $"어빌리티 태그 '{registeredName}'이 중복 등록되어 있다."
                        : $"어빌리티 태그 '{registeredName}'과 '{definition.AbilityTag.Name}'은 대소문자만 달라 같은 태그로 중복 등록되어 있다.";
                    return false;
                }

                seenTags.Add(definition.AbilityTag, definition.AbilityTag.Name);
            }

            foreach (var tagName in startingTagNames)
            {
                if (!GameplayTag.TryParse(tagName, out _))
                {
                    errorMessage = $"시작 태그 '{tagName}'은 유효한 태그 형식이 아니다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 어빌리티 집합을 만든다.
        /// </summary>
        /// <param name="grantedAbilities">부여할 어빌리티 목록이다.</param>
        /// <param name="attributes">시작 어트리뷰트 목록이다.</param>
        /// <param name="startingTags">시작 태그 이름 목록이다.</param>
        /// <param name="attributeSetDefinitions">시작할 때 갖출 어트리뷰트 묶음 목록이다.</param>
        /// <returns>만든 어빌리티 집합이다.</returns>
        public static GameplayAbilitySet CreateRuntime(
            IEnumerable<GameplayAbilityDefinition> grantedAbilities = null,
            IEnumerable<StartingAttribute> attributes = null,
            IEnumerable<string> startingTags = null,
            IEnumerable<AttributeSetDefinition> attributeSetDefinitions = null)
        {
            var set = CreateInstance<GameplayAbilitySet>();
            if (grantedAbilities != null)
            {
                set.abilities.AddRange(grantedAbilities);
            }

            if (attributeSetDefinitions != null)
            {
                set.attributeSets.AddRange(attributeSetDefinitions);
            }

            if (attributes != null)
            {
                set.startingAttributes.AddRange(attributes);
            }

            if (startingTags != null)
            {
                set.startingTagNames.AddRange(startingTags);
            }

            return set;
        }

        /// <summary>
        /// 액터가 시작할 때 갖출 어트리뷰트 하나와 그 기본값이다.
        /// </summary>
        [Serializable]
        public struct StartingAttribute
        {
            [Tooltip("갖출 어트리뷰트 정의이다.")]
            [SerializeField]
            private AttributeDefinition definition;

            [Tooltip("시작 기본값이다.")]
            [SerializeField]
            private float baseValue;

            /// <summary>시작 어트리뷰트 항목을 생성한다.</summary>
            /// <param name="definition">갖출 어트리뷰트 정의이다.</param>
            /// <param name="baseValue">시작 기본값이다.</param>
            public StartingAttribute(AttributeDefinition definition, float baseValue)
            {
                this.definition = definition;
                this.baseValue = baseValue;
            }

            /// <summary>갖출 어트리뷰트 정의이다.</summary>
            public AttributeDefinition Definition => definition;

            /// <summary>시작 기본값이다.</summary>
            public float BaseValue => baseValue;
        }
    }
}
