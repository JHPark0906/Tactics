using System.Text.RegularExpressions;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 스테이지가 준 경험치를 파티가 나누는 몫과, 표에 없는 스테이지를 다루는 방식을 고정한다.
    /// </summary>
    /// <remarks>
    /// <b>나누는 수가 무엇이냐가 이 검사의 중심이다.</b> 배치한 수로 나누는 것과 살아남은 수로 나누는 것은
    /// 대부분의 판에서 같은 답을 내고, 갈라지는 것은 <b>누가 죽었을 때뿐</b>이다. 그때 몫이 커지면
    /// 일부러 죽게 두는 것이 이득이 되는데, 그 사실은 사용자가 발견하기 전까지 어디에도 드러나지 않는다.
    /// </remarks>
    public sealed class StageExperienceRewardTests
    {
        private StageExperienceTable _table;

        [TearDown]
        public void TearDown()
        {
            if (_table != null)
            {
                Object.DestroyImmediate(_table);
                _table = null;
            }
        }

        [Test]
        public void APartyOfFiveSplitsTheRewardFiveWays()
        {
            Assert.That(StageExperienceReward.ResolveShare(500, 5), Is.EqualTo(100));
        }

        [Test]
        public void ASmallerPartyGrowsFaster()
        {
            var alone = StageExperienceReward.ResolveShare(500, 1);
            var crowded = StageExperienceReward.ResolveShare(500, 5);

            Assert.That(alone, Is.GreaterThan(crowded), "수를 늘리는 선택이 값을 치르지 않으면 늘 다섯을 데려간다.");
            Assert.That(alone, Is.EqualTo(500));
        }

        [Test]
        public void TheRemainderIsDropped()
        {
            Assert.That(
                StageExperienceReward.ResolveShare(100, 3),
                Is.EqualTo(33),
                "남는 몫을 주려면 플레이와 상관없는 순서를 정해야 한다.");
        }

        [Test]
        public void NobodyDeployedMeansNothingToSplit()
        {
            Assert.That(StageExperienceReward.ResolveShare(500, 0), Is.Zero);
            Assert.That(StageExperienceReward.ResolveShare(-10, 3), Is.Zero);
        }

        [Test]
        public void PartySizesAreCheckedAgainstTheDeploymentRule()
        {
            Assert.That(StageExperienceReward.IsPartySizeWithinRules(1), Is.True);
            Assert.That(StageExperienceReward.IsPartySizeWithinRules(PartyRules.MaxPartySize), Is.True);
            Assert.That(StageExperienceReward.IsPartySizeWithinRules(0), Is.False);
            Assert.That(
                StageExperienceReward.IsPartySizeWithinRules(PartyRules.MaxPartySize + 1),
                Is.False,
                "여기서 걸린다면 배치 상한이 파티 규칙과 어긋났다는 뜻이다.");
        }

        [Test]
        public void AStageWrittenInTheTableGivesWhatIsWritten()
        {
            _table = CreateTable("{\"entries\":[{\"stageId\":3,\"experience\":450}]}");

            Assert.That(_table.HasStage(3), Is.True);
            Assert.That(_table.GetStageExperience(3), Is.EqualTo(450));
        }

        [Test]
        public void AMissingStageGivesNothingButSaysSoOnce()
        {
            _table = CreateTable("{\"entries\":[{\"stageId\":1,\"experience\":100}]}");
            LogAssert.Expect(LogType.Warning, new Regex("StageExperienceTable"));

            Assert.That(_table.GetStageExperience(7), Is.Zero);
            Assert.That(
                _table.GetStageExperience(7),
                Is.Zero,
                "같은 스테이지를 다시 물을 때마다 알리면 같은 줄로 화면이 덮인다.");
        }

        /// <summary>적힌 값을 채운 표를 만든다.</summary>
        /// <param name="json">채울 값이 담긴 JSON이다.</param>
        /// <returns>만들어진 표이다.</returns>
        private static StageExperienceTable CreateTable(string json)
        {
            var table = ScriptableObject.CreateInstance<StageExperienceTable>();
            JsonUtility.FromJsonOverwrite(json, table);
            return table;
        }
    }
}
