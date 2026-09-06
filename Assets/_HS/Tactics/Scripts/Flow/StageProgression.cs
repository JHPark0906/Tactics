using System;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 스테이지 진행 상태를 보관하고 전투 결과를 클리어 기록으로 옮기는 순수 클래스이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>전투가 끝나면 이기든 지든 스테이지 선택으로 돌아간다.</b> 그래서 여기에는 다음 스테이지를
    /// 찾거나 같은 스테이지를 다시 시작하는 계산이 없다. 결과가 남기는 것은 클리어 기록 하나이며,
    /// 어느 스테이지를 다음에 고를지는 플레이어가 목록에서 정한다. 해금은 없으므로 목록의 모든
    /// 스테이지를 처음부터 고를 수 있다.
    /// </para>
    /// <para>
    /// 클리어 표시는 <see cref="IsCleared"/> 하나로 판단한다. 최고 클리어 번호 이하는 전부 클리어로 본다.
    /// </para>
    /// <para>
    /// Unity와 프레임워크 수명주기에 의존하지 않으므로 EditMode에서 그대로 검증할 수 있다.
    /// 실제 씬 이동과 저장 연결은 <see cref="StageFlowController"/>가 담당한다.
    /// </para>
    /// </remarks>
    public sealed class StageProgression
    {
        /// <summary>스테이지 진행 상태를 생성한다.</summary>
        /// <param name="firstStageId">캠페인의 첫 스테이지 식별자이다.</param>
        public StageProgression(int firstStageId = 1)
        {
            FirstStageId = firstStageId;
            CurrentStageId = firstStageId;
        }

        /// <summary>캠페인의 첫 스테이지 식별자이다.</summary>
        public int FirstStageId { get; }

        /// <summary>지금 열려 있는 스테이지 식별자이다. 전투 씬이 열릴 때 그 씬의 번호로 맞춘다.</summary>
        public int CurrentStageId { get; private set; }

        /// <summary>지금까지 클리어한 가장 높은 스테이지 식별자이며, 아직 없으면 첫 스테이지보다 작다.</summary>
        public int HighestClearedStageId { get; private set; }

        /// <summary>클리어한 스테이지가 하나라도 있는지 여부이다.</summary>
        public bool HasClearedAnyStage => HighestClearedStageId >= FirstStageId;

        /// <summary>지정한 스테이지를 클리어한 것으로 보는지 확인한다.</summary>
        /// <remarks>
        /// 번호가 <see cref="HighestClearedStageId"/> 이하이면 클리어로 본다. 스테이지는 처음부터 전부 고를 수 있으므로
        /// 3번을 먼저 깨면 1·2번도 클리어로 표시되는데, 클리어 상태는 스테이지별 집합이 아니라 최고 클리어 번호로 표현한다.
        /// 클리어 표시는 어디서든 이 질문 하나로 판단한다.
        /// </remarks>
        /// <param name="stageId">확인할 스테이지 식별자이다.</param>
        /// <returns>클리어한 것으로 보면 true이다.</returns>
        public bool IsCleared(int stageId)
        {
            return HasClearedAnyStage && stageId <= HighestClearedStageId;
        }

        /// <summary>
        /// 지금 열려 있는 스테이지를 지정한다. 전투 씬이 열릴 때와 저장된 진행도를 불러올 때 사용한다.
        /// </summary>
        /// <param name="stageId">열린 스테이지 식별자이다.</param>
        public void SetCurrentStage(int stageId)
        {
            CurrentStageId = Math.Max(FirstStageId, stageId);
        }

        /// <summary>
        /// 클리어 기록을 지정한다. 저장된 진행도를 복원할 때 사용하며, 기존 기록보다 낮은 값은 무시한다.
        /// </summary>
        /// <param name="stageId">클리어한 가장 높은 스테이지 식별자이다.</param>
        public void SetHighestClearedStage(int stageId)
        {
            HighestClearedStageId = Math.Max(HighestClearedStageId, stageId);
        }

        /// <summary>
        /// 전투 결과를 클리어 기록에 반영한다.
        /// 승리하면 지금 스테이지를 클리어로 기록하고, 패배하면 아무것도 바꾸지 않는다. 어느 쪽이든 열려 있는 스테이지는 그대로다.
        /// </summary>
        /// <param name="outcome">확정된 전투 결과이다.</param>
        /// <exception cref="ArgumentOutOfRangeException">결과가 확정되지 않은 값이면 발생한다.</exception>
        public void ApplyOutcome(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.Defeat:
                    return;

                case BattleOutcome.Victory:
                    SetHighestClearedStage(CurrentStageId);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(outcome), outcome, "확정되지 않은 전투 결과는 클리어 기록에 반영할 수 없다.");
            }
        }
    }
}
