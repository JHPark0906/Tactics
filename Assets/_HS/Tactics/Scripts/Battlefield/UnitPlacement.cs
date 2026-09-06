using System;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Battlefield
{
    /// <summary>
    /// 스테이지가 전장에 놓을 유닛 하나의 배치 항목이다. 어떤 종류를, 몇 레벨로, 어디에 세울지를 담는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>좌표는 <see cref="Vector3"/>로 저장한다.</b> 로직은 평면 위에서만 돌지만 스폰은 <c>UnitSpawner.Spawn</c>이
    /// <see cref="Vector3"/>를 받으므로 저장 형식을 그쪽에 맞춘다. 높이 성분은 스폰 위치로만 쓰이고 판정에는 들어가지 않는다.
    /// 여기서 평면 좌표로 바꿔 두면 스폰하는 쪽이 매번 높이를 붙여야 하고, 그 자리가 둘이 되면 어긋난다.
    /// </para>
    /// <para>
    /// <b>인스펙터에서 편집할 수 있어야 한다.</b> 그래서 직렬화 필드와 읽기 전용 프로퍼티를 나눈다. 코드에서 만들 때는
    /// 생성자를 쓰고, 에셋에서 읽을 때는 Unity 직렬화가 필드를 채운다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class UnitPlacement
    {
        [Tooltip("세울 유닛의 종류이다.")]
        [SerializeField]
        private UnitDefinition definition;

        [Tooltip("세울 때의 레벨이다. 1 미만은 1로 본다.")]
        [SerializeField]
        [Min(1)]
        private int level = 1;

        [Tooltip("세울 월드 좌표이다. 스폰이 그대로 쓴다.")]
        [SerializeField]
        private Vector3 position;

        /// <summary>
        /// Unity 직렬화가 쓰는 생성자이다. 코드에서 기본 생성하면 레벨 1, 원점이며, 에셋에서 읽을 때는 직렬화된 값이 그 위를
        /// 덮어쓴다. 인스펙터가 새 항목에 0을 채워 넣어도 <see cref="Level"/>이 하한을 지킨다.
        /// </summary>
        public UnitPlacement()
        {
        }

        /// <summary>코드에서 배치 항목을 만든다.</summary>
        /// <param name="definition">세울 유닛의 종류이다.</param>
        /// <param name="level">세울 때의 레벨이며 1 미만은 1로 본다.</param>
        /// <param name="position">세울 월드 좌표이다.</param>
        public UnitPlacement(UnitDefinition definition, int level, Vector3 position)
        {
            this.definition = definition;
            this.level = Mathf.Max(1, level);
            this.position = position;
        }

        /// <summary>세울 유닛의 종류이며 지정되지 않았으면 null이다.</summary>
        public UnitDefinition Definition => definition;

        /// <summary>세울 때의 레벨이며 항상 1 이상이다.</summary>
        public int Level => Mathf.Max(1, level);

        /// <summary>세울 월드 좌표이다.</summary>
        public Vector3 Position => position;

        /// <inheritdoc />
        public override string ToString()
        {
            var definitionName = definition != null ? definition.DisplayName : "없음";
            return $"{definitionName} Lv.{Level} @ {position}";
        }
    }
}
