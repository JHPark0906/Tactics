using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Ability.Tags
{
    /// <summary>
    /// GameObject가 보유한 게임플레이 태그 컨테이너를 노출하는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 컴포넌트는 컨테이너를 세계에 붙여 주는 얇은 껍데기이며 규칙을 따로 갖지 않는다.
    /// 태그를 부여하고 회수하는 일은 <see cref="Container"/>를 통해 이루어지고,
    /// 참조 계수와 계층 질의는 모두 컨테이너가 담당한다.
    /// </para>
    /// <para>
    /// 어빌리티나 효과가 없어도 상태 표식만으로 쓸 수 있다. 예를 들어 상호작용이나 AI가
    /// "이 대상이 특정 상태인가"를 묻는 용도로 이 컴포넌트만 붙여 사용해도 된다.
    /// </para>
    /// <para>
    /// 시작 태그는 부여 횟수 1로 들어간다. 어떤 태그를 초기값으로 둘지는 게임이 정하며,
    /// 카탈로그를 연결해 두면 등록되지 않은 이름을 시작 태그에 적었을 때 진단으로 알려 준다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameplayTagComponent : MonoBehaviour
    {
        [Tooltip("시작할 때 부여할 태그 이름 목록이다. 점으로 계층을 구분한다. 예: Unit.Infantry")]
        [SerializeField]
        private List<string> initialTagNames = new();

        [Tooltip("시작 태그 이름을 검사할 태그 카탈로그이다. 비워 두면 형식만 검사한다.")]
        [SerializeField]
        private GameplayTagCatalog tagCatalog;

        /// <summary>시작 태그를 이미 적용했는지 여부이다.</summary>
        private bool _hasAppliedInitialTags;

        /// <summary>이 대상이 보유한 태그 컨테이너이다.</summary>
        public GameplayTagContainer Container { get; } = new();

        /// <summary>시작 태그를 이미 적용했는지 여부이다.</summary>
        public bool HasAppliedInitialTags => _hasAppliedInitialTags;

        /// <summary>
        /// 인스펙터에 적어 둔 시작 태그를 컨테이너에 부여한다.
        /// 여러 번 호출해도 처음 한 번만 적용하며, 스폰한 쪽이 Awake보다 먼저 태그를 갖추고 싶을 때 직접 호출한다.
        /// </summary>
        public void ApplyInitialTags()
        {
            if (_hasAppliedInitialTags)
            {
                return;
            }

            _hasAppliedInitialTags = true;
            foreach (var tagName in initialTagNames)
            {
                if (!GameplayTag.TryParse(tagName, out var tag))
                {
                    Debug.LogWarning($"[GameplayTagComponent] {name}의 시작 태그 '{tagName}'은 유효한 형식이 아니라 건너뛴다.", this);
                    continue;
                }

                if (tagCatalog != null && !tagCatalog.Contains(tag))
                {
                    Debug.LogWarning($"[GameplayTagComponent] {name}의 시작 태그 '{tag.Name}'이 카탈로그에 등록되어 있지 않다.", this);
                }

                Container.AddTag(tag);
            }
        }

        private void Awake()
        {
            ApplyInitialTags();
        }
    }
}
