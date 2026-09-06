using System;
using HS.Tactics.Flow;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>전투 결과가 클리어 기록에만 닿고, 열려 있는 스테이지를 옮기지 않는 것을 고정한다.</summary>
    /// <remarks>
    /// 전투가 끝나면 이기든 지든 스테이지 선택으로 돌아가므로, 진행 상태에는 다음 스테이지를 찾거나
    /// 같은 스테이지를 다시 시작하는 계산이 없다. 여기 남는 것은 클리어 기록과 그것을 읽는 규칙뿐이다.
    /// </remarks>
    public sealed class StageProgressionTests
    {
        [Test]
        public void StartsAtFirstStageWithoutClearRecord()
        {
            var progression = new StageProgression();

            Assert.That(progression.CurrentStageId, Is.EqualTo(1));
            Assert.That(progression.HasClearedAnyStage, Is.False);
            Assert.That(progression.IsCleared(1), Is.False);
        }

        [Test]
        public void AFirstStageAboveOneStartsThereWithoutClearRecord()
        {
            var progression = new StageProgression(firstStageId: 5);

            Assert.That(progression.CurrentStageId, Is.EqualTo(5));
            Assert.That(progression.HasClearedAnyStage, Is.False, "아직 아무것도 깨지 않았으면 첫 스테이지 번호가 커도 기록은 없다.");
        }

        [Test]
        public void VictoryRecordsTheClearAndLeavesTheOpenStageAlone()
        {
            var progression = new StageProgression();

            progression.ApplyOutcome(BattleOutcome.Victory);

            Assert.That(progression.HighestClearedStageId, Is.EqualTo(1));
            Assert.That(progression.IsCleared(1), Is.True);
            Assert.That(progression.CurrentStageId, Is.EqualTo(1), "다음 스테이지를 고르는 것은 플레이어이지 진행 상태가 아니다.");
        }

        [Test]
        public void DefeatLeavesTheRecordAlone()
        {
            var progression = new StageProgression();
            progression.ApplyOutcome(BattleOutcome.Victory);
            progression.SetCurrentStage(2);

            progression.ApplyOutcome(BattleOutcome.Defeat);

            Assert.That(progression.HighestClearedStageId, Is.EqualTo(1));
            Assert.That(progression.IsCleared(2), Is.False);
            Assert.That(progression.CurrentStageId, Is.EqualTo(2));
        }

        [Test]
        public void ClearingAHigherStageMarksTheLowerOnesClearedToo()
        {
            var progression = new StageProgression();
            progression.SetCurrentStage(3);

            progression.ApplyOutcome(BattleOutcome.Victory);

            Assert.That(progression.IsCleared(1), Is.True, "최고 클리어 번호 이하는 전부 클리어로 본다.");
            Assert.That(progression.IsCleared(2), Is.True);
            Assert.That(progression.IsCleared(3), Is.True);
            Assert.That(progression.IsCleared(4), Is.False);
        }

        [Test]
        public void ClearingALowerStageAfterAHigherOneKeepsTheRecord()
        {
            var progression = new StageProgression();
            progression.SetCurrentStage(3);
            progression.ApplyOutcome(BattleOutcome.Victory);
            progression.SetCurrentStage(1);

            progression.ApplyOutcome(BattleOutcome.Victory);

            Assert.That(progression.HighestClearedStageId, Is.EqualTo(3));
        }

        [Test]
        public void UndecidedOutcomeIsRejected()
        {
            var progression = new StageProgression();

            Assert.Throws<ArgumentOutOfRangeException>(() => progression.ApplyOutcome(BattleOutcome.Undecided));
        }

        [Test]
        public void ClearRecordNeverGoesBackwards()
        {
            var progression = new StageProgression();
            progression.SetHighestClearedStage(2);

            progression.SetHighestClearedStage(1);

            Assert.That(progression.HighestClearedStageId, Is.EqualTo(2));
        }

        [Test]
        public void CurrentStageIsClampedToFirstStage()
        {
            var progression = new StageProgression();

            progression.SetCurrentStage(-5);

            Assert.That(progression.CurrentStageId, Is.EqualTo(1));
        }
    }
}
