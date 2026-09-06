using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Item
{
    /// <summary>
    /// 프로젝트의 ItemDefinition 에셋을 등록하고 저장된 아이템 Id를 정의로 되돌리는 룩업을 제공한다.
    /// 인벤토리 복원 등 Id 기반 역참조가 필요한 모든 곳에서 단일 카탈로그를 공유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Item/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> definitions = new();

        /// <summary>
        /// Id로 정의를 조회하기 위한 지연 생성 캐시이다.
        /// </summary>
        private Dictionary<string, ItemDefinition> _lookup;

        /// <summary>
        /// 등록된 아이템 정의 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<ItemDefinition> Definitions => definitions;

        /// <summary>
        /// 지정한 Id로 등록된 아이템 정의를 조회한다.
        /// </summary>
        /// <param name="id">조회할 아이템 Id이다.</param>
        /// <param name="definition">찾은 아이템 정의이며, 없으면 null이다.</param>
        /// <returns>정의를 찾았으면 true를 반환한다.</returns>
        public bool TryGetById(string id, out ItemDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureLookup();
            return _lookup.TryGetValue(id, out definition);
        }

        /// <summary>
        /// 등록 목록에 빈 항목, 빈 Id, 중복 Id가 없는지 검증한다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 첫 원인을 설명하는 메시지이며, 성공하면 null이다.</param>
        /// <returns>모든 항목이 유효하면 true를 반환한다.</returns>
        public bool TryValidate(out string errorMessage)
        {
            var seenIds = new HashSet<string>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null)
                {
                    errorMessage = $"{index}번 항목이 비어 있습니다.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    errorMessage = $"{index}번 항목({definition.name})의 Id가 비어 있습니다.";
                    return false;
                }

                if (!seenIds.Add(definition.Id))
                {
                    errorMessage = $"Id '{definition.Id}'가 중복 등록되어 있습니다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 카탈로그를 만든다.
        /// </summary>
        /// <param name="itemDefinitions">등록할 아이템 정의 목록이다.</param>
        /// <returns>생성된 카탈로그 인스턴스를 반환한다.</returns>
        public static ItemCatalog CreateRuntime(IEnumerable<ItemDefinition> itemDefinitions)
        {
            var catalog = CreateInstance<ItemCatalog>();
            if (itemDefinitions != null)
            {
                catalog.definitions.AddRange(itemDefinitions);
            }

            return catalog;
        }

        /// <summary>
        /// Id 조회 캐시를 아직 만들지 않았으면 만든다. 빈 항목과 빈 Id는 건너뛰고, 중복 Id는 먼저 등록된 정의가 우선한다.
        /// </summary>
        private void EnsureLookup()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<string, ItemDefinition>(definitions.Count);
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                _lookup.TryAdd(definition.Id, definition);
            }
        }

        /// <summary>
        /// 에디터에서 목록이 수정되면 Id 조회 캐시를 무효화한다.
        /// </summary>
        private void OnValidate()
        {
            _lookup = null;
        }
    }
}
