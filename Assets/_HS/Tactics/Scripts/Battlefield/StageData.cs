using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Battlefield
{
    /// <summary>
    /// 스테이지 하나가 전장을 동적으로 만드는 데 필요한 데이터이다. 전장의 길이와 유닛 배치 리스트를 담는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>필드는 둘뿐이다.</b> 전장은 이 두 값만으로 만들어진다 — 길이가 바닥의 크기를 정하고, 배치 리스트가 누가 어디에
    /// 서는지를 정한다. 그 밖의 것(지형지물, 조명, 카메라)은 스테이지마다 다르지 않으므로 여기 들어오지 않는다. 값이 늘면
    /// 스테이지를 만드는 사람이 채워야 할 칸이 늘고, 대부분은 같은 값을 복사하게 된다.
    /// </para>
    /// <para>
    /// <b>이 에셋은 데이터일 뿐이고 아무것도 만들지 않는다.</b> 바닥을 세우고 유닛을 스폰하는 것은 이 데이터를 읽는 쪽의
    /// 일이다. 데이터가 생성 방법을 알면 생성 방법이 바뀔 때마다 데이터 형식도 함께 바뀐다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "StageData", menuName = "Tactics/Stage Data")]
    public sealed class StageData : ScriptableObject
    {
        [Tooltip("전장의 길이(미터)이다. 아군이 전진하는 축 방향의 길이이며 스테이지마다 정한다.")]
        [SerializeField]
        [Min(1f)]
        private float battlefieldLength = 1f;

        [Tooltip("전장에 세울 유닛의 배치 리스트이다. 종류, 레벨, 좌표를 항목마다 정한다.")]
        [SerializeField]
        private List<UnitPlacement> unitPlacements = new();

        /// <summary>전장의 길이(미터)이며 항상 1 이상이다.</summary>
        public float BattlefieldLength => Mathf.Max(1f, battlefieldLength);

        /// <summary>전장에 세울 유닛의 배치 리스트이다. 비어 있을 수 있으며 null은 아니다.</summary>
        public IReadOnlyList<UnitPlacement> UnitPlacements => unitPlacements ??= new List<UnitPlacement>();

        /// <summary>
        /// 테스트와 에디터 도구에서 쓸 비저장 스테이지 데이터를 만든다. 에셋으로 저장하면 그대로 에셋이 된다.
        /// </summary>
        /// <param name="battlefieldLength">전장의 길이(미터)이며 1 미만은 1로 본다.</param>
        /// <param name="placements">배치 항목이다. null 항목은 넣지 않는다.</param>
        /// <returns>만든 스테이지 데이터이다.</returns>
        public static StageData CreateRuntime(float battlefieldLength, params UnitPlacement[] placements)
        {
            var stage = CreateInstance<StageData>();
            stage.battlefieldLength = Mathf.Max(1f, battlefieldLength);
            stage.unitPlacements = new List<UnitPlacement>();
            if (placements == null)
            {
                return stage;
            }

            foreach (var placement in placements)
            {
                if (placement != null)
                {
                    stage.unitPlacements.Add(placement);
                }
            }

            return stage;
        }
    }
}
