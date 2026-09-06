using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Ability.Tags
{
    /// <summary>
    /// 게임이 사용하는 태그 이름을 한곳에 모아 두고, 이름으로 태그를 찾아 주는 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 에셋인가.</b> 태그 이름을 호출부에 문자열로 흩뿌리면 오타가 조용한 실패가 된다.
    /// 그렇다고 프레임워크가 상수 묶음을 제공할 수는 없다. 어떤 태그가 존재하는지는 게임마다 다르고,
    /// 프레임워크가 특정 게임의 태그를 알면 그 순간 범용 자산이 아니게 되기 때문이다.
    /// 그래서 프레임워크는 <b>이름을 모으는 그릇</b>만 제공하고 내용은 게임이 채운다.
    /// 에셋으로 만든 이유는 코드를 고치지 않고도 태그를 늘릴 수 있고, 인스펙터에서 목록을 눈으로 확인할 수 있으며,
    /// 데이터 에셋이 태그를 참조할 때 같은 목록을 함께 쓸 수 있기 때문이다.
    /// </para>
    /// <para>
    /// <b>코드에서 쓰는 방법.</b> 게임은 자기 코드에 상수 묶음을 두고 그 값을 이 카탈로그에도 등록해 두는 것이 좋다.
    /// 상수는 컴파일 시점에 오타를 막아 주고, 카탈로그는 데이터와 코드가 같은 목록을 보고 있는지
    /// <see cref="TryValidate"/>와 <see cref="Contains"/>로 확인해 준다. 두 수단은 경쟁하지 않고 서로를 보완한다.
    /// 등록되지 않은 이름을 코드에서 쓰면 <see cref="RequireTag"/>가 진단을 남기므로 조용히 지나가지 않는다.
    /// </para>
    /// <para>
    /// <b>카탈로그는 강제가 아니다.</b> <see cref="GameplayTag"/>와 <see cref="GameplayTagContainer"/>는
    /// 카탈로그 없이도 동작한다. 이 에셋은 이름을 관리하고 검증하는 편의 수단이며,
    /// 태그 시스템이 이 에셋에 의존하지는 않는다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Ability/Gameplay Tag Catalog", fileName = "GameplayTagCatalog")]
    public sealed class GameplayTagCatalog : ScriptableObject
    {
        [Tooltip("이 게임이 사용하는 태그 이름 목록이다. 점으로 계층을 구분한다. 예: State.Slowed")]
        [SerializeField]
        private List<string> tagNames = new();

        /// <summary>이름으로 태그를 찾기 위한 지연 생성 캐시이며 대소문자를 구분하지 않는다.</summary>
        private Dictionary<string, GameplayTag> _tagsByName;

        /// <summary>등록된 태그 이름 목록이며 검증 전의 원본 문자열이다.</summary>
        public IReadOnlyList<string> TagNames => tagNames;

        /// <summary>등록된 이름 가운데 유효한 태그로 해석된 것의 수이다.</summary>
        public int Count
        {
            get
            {
                EnsureLookup();
                return _tagsByName.Count;
            }
        }

        /// <summary>등록된 태그를 열거한다. 유효하지 않은 이름은 건너뛴다.</summary>
        public IEnumerable<GameplayTag> Tags
        {
            get
            {
                EnsureLookup();
                return _tagsByName.Values;
            }
        }

        /// <summary>
        /// 등록된 이름으로 태그를 찾는다. 등록되지 않은 이름은 형식이 맞더라도 찾지 않는다.
        /// </summary>
        /// <param name="tagName">찾을 태그 이름이다.</param>
        /// <param name="tag">찾은 태그이며 없으면 <see cref="GameplayTag.None"/>이다.</param>
        /// <returns>등록된 태그를 찾았으면 true이다.</returns>
        public bool TryGetTag(string tagName, out GameplayTag tag)
        {
            tag = GameplayTag.None;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            EnsureLookup();
            return _tagsByName.TryGetValue(tagName.Trim(), out tag);
        }

        /// <summary>
        /// 등록된 이름으로 태그를 찾고, 등록되어 있지 않으면 진단을 남긴 뒤 <see cref="GameplayTag.None"/>을 반환한다.
        /// 코드가 기대하는 태그가 카탈로그에서 빠졌을 때 조용히 넘어가지 않게 하는 경로이다.
        /// </summary>
        /// <param name="tagName">찾을 태그 이름이다.</param>
        /// <returns>찾은 태그이며 없으면 <see cref="GameplayTag.None"/>이다.</returns>
        public GameplayTag RequireTag(string tagName)
        {
            if (TryGetTag(tagName, out var tag))
            {
                return tag;
            }

            Debug.LogError($"[GameplayTagCatalog] '{tagName}' 태그가 {name} 카탈로그에 등록되어 있지 않다.", this);
            return GameplayTag.None;
        }

        /// <summary>
        /// 지정한 태그가 이 카탈로그에 등록되어 있는지 확인한다.
        /// </summary>
        /// <param name="tag">확인할 태그이다.</param>
        /// <returns>등록되어 있으면 true이다.</returns>
        public bool Contains(GameplayTag tag)
        {
            return tag.IsValid && TryGetTag(tag.Name, out _);
        }

        /// <summary>
        /// 등록 목록에 형식이 잘못된 이름이나 중복이 없는지 검사한다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 첫 원인을 설명하는 메시지이며 성공하면 null이다.</param>
        /// <returns>모든 이름이 유효하고 중복이 없으면 true이다.</returns>
        public bool TryValidate(out string errorMessage)
        {
            // 이미 등록된 이름을 작성한 그대로 기억해 두어야, 중복을 알릴 때 사용자가 자기 목록에서 두 항목을 다 찾을 수 있다.
            var seenNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < tagNames.Count; index++)
            {
                var tagName = tagNames[index];
                if (!GameplayTag.TryParse(tagName, out var tag))
                {
                    errorMessage = $"{index}번 이름 '{tagName}'은 유효한 태그 형식이 아니다.";
                    return false;
                }

                if (seenNames.TryGetValue(tag.Name, out var registeredName))
                {
                    errorMessage = string.Equals(registeredName, tag.Name, System.StringComparison.Ordinal)
                        ? $"태그 '{tag.Name}'이 중복 등록되어 있다."
                        : $"태그 '{registeredName}'과 '{tag.Name}'은 대소문자만 달라 같은 태그로 중복 등록되어 있다.";
                    return false;
                }

                seenNames.Add(tag.Name, tag.Name);
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 카탈로그를 만든다.
        /// </summary>
        /// <param name="names">등록할 태그 이름 목록이다.</param>
        /// <returns>생성한 카탈로그이다.</returns>
        public static GameplayTagCatalog CreateRuntime(IEnumerable<string> names)
        {
            var catalog = CreateInstance<GameplayTagCatalog>();
            if (names != null)
            {
                catalog.tagNames.AddRange(names);
            }

            return catalog;
        }

        /// <summary>
        /// 이름 조회 캐시를 아직 만들지 않았으면 만든다.
        /// 형식이 잘못된 이름은 건너뛰고, 중복된 이름은 먼저 등록된 것이 남는다.
        /// 잘못된 이름을 여기서 조용히 넘기는 대신 <see cref="TryValidate"/>가 문제를 드러낸다.
        /// </summary>
        private void EnsureLookup()
        {
            if (_tagsByName != null)
            {
                return;
            }

            _tagsByName = new Dictionary<string, GameplayTag>(tagNames.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (var tagName in tagNames)
            {
                if (GameplayTag.TryParse(tagName, out var tag))
                {
                    _tagsByName.TryAdd(tag.Name, tag);
                }
            }
        }

        /// <summary>에디터에서 목록이 바뀌면 이름 조회 캐시를 무효화한다.</summary>
        private void OnValidate()
        {
            _tagsByName = null;
        }
    }
}
