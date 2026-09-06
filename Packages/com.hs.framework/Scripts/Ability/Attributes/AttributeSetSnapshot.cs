using System;
using System.Collections.Generic;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// 어트리뷰트 집합의 기본값을 (식별자, 값) 목록으로 직렬화 가능한 형태로 보관한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>저장 대상은 기본값이다.</b> 수정자는 버프와 장비가 다시 얹어 주므로 저장하지 않는다.
    /// 수정자까지 저장하면 복원 뒤에 효과가 다시 얹으면서 같은 수정자가 두 번 쌓이고,
    /// 원인이 사라진 수정자가 되돌릴 주체 없이 영원히 남는다.
    /// 반대로 피해와 회복은 기본값을 바꾸므로 기본값만 저장해도 상태가 그대로 되살아난다.
    /// </para>
    /// <para>
    /// <b>새 저장 수단을 만들지 않는다.</b> 이 형식은 <c>JsonUtility</c>로 직렬화되는 평범한 데이터라,
    /// 프레임워크의 <c>ISaveable</c> 구현이 <c>CaptureState</c>에서 이것을 JSON으로 바꾸고
    /// <c>RestoreState</c>에서 되돌리면 기존 <c>SaveOrchestrator</c> 경로에 그대로 얹힌다.
    /// 어트리뷰트를 저장에 참여시키는 어댑터는 그 저장을 실제로 쓰는 쪽이 만든다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class AttributeSetSnapshot
    {
        /// <summary>저장된 (어트리뷰트 식별자, 기본값) 항목 목록이다.</summary>
        public List<AttributeSnapshotEntry> entries = new();

        /// <summary>
        /// 어트리뷰트 집합의 기본값을 캡처한다. 식별자가 없는 정의는 되돌릴 수 없으므로 건너뛴다.
        /// </summary>
        /// <param name="attributeSet">캡처할 어트리뷰트 집합이다.</param>
        /// <returns>캡처한 스냅숏이다.</returns>
        /// <exception cref="ArgumentNullException">집합이 null이면 발생한다.</exception>
        public static AttributeSetSnapshot Capture(AttributeSet attributeSet)
        {
            if (attributeSet == null)
            {
                throw new ArgumentNullException(nameof(attributeSet));
            }

            var snapshot = new AttributeSetSnapshot();
            foreach (var definition in attributeSet.Definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                snapshot.entries.Add(
                    new AttributeSnapshotEntry(definition.Id, attributeSet.GetBaseValue(definition)));
            }

            return snapshot;
        }

        /// <summary>
        /// 담고 있는 기본값을 어트리뷰트 집합에 되돌린다.
        /// 집합에 없는 식별자는 건너뛰고, 스냅숏에 없는 어트리뷰트는 손대지 않는다.
        /// </summary>
        /// <param name="attributeSet">되돌릴 대상 어트리뷰트 집합이다.</param>
        /// <returns>실제로 되돌린 어트리뷰트 수이다.</returns>
        /// <exception cref="ArgumentNullException">집합이 null이면 발생한다.</exception>
        public int RestoreTo(AttributeSet attributeSet)
        {
            if (attributeSet == null)
            {
                throw new ArgumentNullException(nameof(attributeSet));
            }

            // 복원은 피해도 회복도 아니므로 원인을 스냅숏으로 밝힌다. 피해를 가로채는 필터가 복원까지 가로채지 않게 하기 위함이다.
            var context = new AttributeChangeContext(this);
            var restoredCount = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!attributeSet.TryFindDefinition(entry.id, out var definition))
                {
                    continue;
                }

                attributeSet.SetBaseValue(definition, entry.baseValue, context);
                restoredCount++;
            }

            return restoredCount;
        }
    }

    /// <summary>어트리뷰트 하나의 저장 항목이다.</summary>
    [Serializable]
    public struct AttributeSnapshotEntry
    {
        /// <summary>어트리뷰트 정의의 고유 식별자이다.</summary>
        public string id;

        /// <summary>저장 시점의 기본값이다.</summary>
        public float baseValue;

        /// <summary>저장 항목을 생성한다.</summary>
        /// <param name="id">어트리뷰트 정의의 고유 식별자이다.</param>
        /// <param name="baseValue">저장할 기본값이다.</param>
        public AttributeSnapshotEntry(string id, float baseValue)
        {
            this.id = id;
            this.baseValue = baseValue;
        }
    }
}
