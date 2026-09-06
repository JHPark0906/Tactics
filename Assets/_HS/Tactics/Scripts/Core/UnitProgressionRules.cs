using UnityEngine;
using HS.Tactics.Progress;

namespace HS.Tactics.Core
{
    /// <summary>스테이지 경험치 표와 유닛 레벨 곡선을 읽는 계약이다.</summary>
    /// <remarks>
    /// DI 컨테이너에는 구체 <see cref="ScriptableObject"/> 타입이 아니라 이 계약으로 등록한다.
    /// 구체 타입을 그대로 토큰으로 쓰면 등록이 중복되거나 빠졌을 때 그 자리에서 드러나지 않고,
    /// 여러 등록자가 같은 구체 타입을 저마다 등록하면 어느 쪽이 이겼는지도 알 수 없다.
    /// </remarks>
    public interface IUnitProgressionRules
    {
        /// <summary>스테이지별 클리어 경험치 표이다. 없으면 null이다.</summary>
        StageExperienceTable StageExperienceTable { get; }

        /// <summary>레벨별 배율과 다음 레벨까지의 경험치 곡선이다. 없으면 null이다.</summary>
        UnitLevelCurve UnitLevelCurve { get; }
    }

    /// <summary>인스펙터에서 채우는 스테이지·레벨 진행 규칙 컴포넌트이다.</summary>
    public sealed class TacticsProgressionRules : MonoBehaviour, IUnitProgressionRules
    {
        [Tooltip("스테이지별 클리어 경험치 표이다. 비워 두면 보상을 주지 않는다.")]
        [SerializeField] private StageExperienceTable stageExperienceTable;

        [Tooltip("레벨별 배율과 다음 레벨까지의 경험치 곡선이다. 비워 두면 보상을 주지 않는다.")]
        [SerializeField] private UnitLevelCurve unitLevelCurve;

        /// <inheritdoc />
        public StageExperienceTable StageExperienceTable => stageExperienceTable;

        /// <inheritdoc />
        public UnitLevelCurve UnitLevelCurve => unitLevelCurve;
    }
}
