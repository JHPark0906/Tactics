using HS.Framework.ProjectManagement;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 스테이지 클리어 판정 규칙을 고정한다. 클리어 기록은 최고 번호 하나이고, 그 이하는 전부 클리어로 본다.
    /// 스테이지를 처음부터 전부 고를 수 있으므로 뒤 번호를 먼저 깨면 앞 번호도 클리어로 보고되며,
    /// 클리어 여부는 스테이지별 기록 대신 최고 번호로 계산한다.
    /// </summary>
    public sealed class StageClearRuleTests
    {
        [Test]
        public void ClearingStageThreeFirstReportsOneAndTwoAsClearedToo()
        {
            var progression = CreateProgression(stageCount: 4);
            progression.SetCurrentStage(3);

            progression.ApplyOutcome(BattleOutcome.Victory);

            Assert.That(progression.IsCleared(1), Is.True);
            Assert.That(progression.IsCleared(2), Is.True);
            Assert.That(progression.IsCleared(3), Is.True);
            Assert.That(progression.IsCleared(4), Is.False);
        }

        [Test]
        public void NothingIsClearedBeforeTheFirstVictory()
        {
            var progression = CreateProgression(stageCount: 2);

            Assert.That(progression.IsCleared(1), Is.False);
            Assert.That(progression.IsCleared(2), Is.False);
        }

        [Test]
        public void DefeatDoesNotClearTheCurrentStage()
        {
            var progression = CreateProgression(stageCount: 2);

            progression.ApplyOutcome(BattleOutcome.Defeat);

            Assert.That(progression.IsCleared(1), Is.False);
        }

        private static StageProgression CreateProgression(int stageCount)
        {
            var scenes = new ProjectSceneDefinition[stageCount];
            for (var index = 0; index < stageCount; index++)
            {
                scenes[index] = FakeSceneCatalog.Gameplay(index + 1);
            }

            return new StageProgressionService(new FakeSceneCatalog(1, scenes)).Progression;
        }
    }
}
