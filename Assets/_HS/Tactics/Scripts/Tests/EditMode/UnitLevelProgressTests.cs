using System.Collections.Generic;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 육성 레벨 상태가 "없으면 시작 레벨"이라는 약속을 지키는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 약속이 깨지면 새 유닛 종류를 더할 때마다 저장 데이터를 손봐야 한다.</b>
    /// 담기지 않은 종류를 물었을 때 답이 없거나 0이면, 그 종류를 쓰는 쪽이 저마다 없는 경우를
    /// 처리하게 되고 그 처리가 서로 달라진다.
    /// </para>
    /// </remarks>
    public sealed class UnitLevelProgressTests
    {
        private UnitLevelProgress _progress;

        [SetUp]
        public void SetUp() => _progress = new UnitLevelProgress();

        [Test]
        public void AnUnraisedKindIsAtTheStartingLevel()
        {
            Assert.That(
                _progress.GetLevel("Rifleman"),
                Is.EqualTo(UnitLevelProgress.StartingLevel),
                "키운 적 없는 종류도 레벨을 물으면 답해야 한다. 없다고 하면 부르는 쪽마다 다르게 처리한다.");
        }

        [Test]
        public void ARaisedKindKeepsItsLevel()
        {
            _progress.SetLevel("Rifleman", 5);

            Assert.That(_progress.GetLevel("Rifleman"), Is.EqualTo(5));
        }

        [Test]
        public void RaisingOneKindDoesNotTouchAnother()
        {
            _progress.SetLevel("Rifleman", 5);

            Assert.That(_progress.GetLevel("Sniper"), Is.EqualTo(UnitLevelProgress.StartingLevel));
        }

        [Test]
        public void TheLevelNeverFallsBelowTheStartingLevel()
        {
            _progress.SetLevel("Rifleman", -3);

            Assert.That(
                _progress.GetLevel("Rifleman"),
                Is.EqualTo(UnitLevelProgress.StartingLevel),
                "시작 레벨 아래는 뜻이 없다. 그런 값이 저장되면 읽는 쪽이 다시 걸러야 한다.");
        }

        [Test]
        public void FallingBackToTheStartingLevelStopsTakingUpRoom()
        {
            _progress.SetLevel("Rifleman", 5);
            Assert.That(_progress.RaisedCount, Is.EqualTo(1));

            _progress.SetLevel("Rifleman", UnitLevelProgress.StartingLevel);

            Assert.That(
                _progress.RaisedCount,
                Is.Zero,
                "물으면 어차피 시작 레벨로 답하므로 담아 둘 이유가 없다. 담아 두면 저장 파일만 커진다.");
        }

        [Test]
        public void WritingTheSameLevelChangesNothing()
        {
            _progress.SetLevel("Rifleman", 5);
            var revisionAfterFirst = _progress.Revision;

            var changed = _progress.SetLevel("Rifleman", 5);

            Assert.That(changed, Is.False);
            Assert.That(
                _progress.Revision,
                Is.EqualTo(revisionAfterFirst),
                "바뀐 것이 없는데 판이 오르면 저장할 것이 없는데도 저장한다.");
        }

        [Test]
        public void AnEmptyIdentifierIsIgnored()
        {
            Assert.That(_progress.SetLevel("   ", 5), Is.False);
            Assert.That(_progress.RaisedCount, Is.Zero, "빈 식별자로 담으면 어느 종류인지 되찾을 수 없다.");
        }

        [Test]
        public void RaisedKindsComeOutInAStableOrder()
        {
            _progress.SetLevel("Sniper", 2);
            _progress.SetLevel("Rifleman", 3);
            _progress.SetLevel("Medic", 4);

            var identifiers = new List<string>();
            foreach (var raised in _progress.EnumerateRaised())
            {
                identifiers.Add(raised.Key);
            }

            Assert.That(
                identifiers,
                Is.EqualTo(new[] { "Medic", "Rifleman", "Sniper" }),
                "차례가 흔들리면 바뀐 것이 없어도 저장 파일이 달라 보인다.");
        }

        [Test]
        public void ClearingReturnsEverythingToTheStartingLevel()
        {
            _progress.SetLevel("Rifleman", 5);

            _progress.Clear();

            Assert.That(_progress.RaisedCount, Is.Zero);
            Assert.That(_progress.GetLevel("Rifleman"), Is.EqualTo(UnitLevelProgress.StartingLevel));
        }

        [Test]
        public void ADefinitionIsLookedUpByItsIdentifier()
        {
            var definition = UnitDefinition.CreateRuntime("소총병", 100, id: "unit.rifleman");
            try
            {
                _progress.SetLevel("unit.rifleman", 7);

                Assert.That(_progress.GetLevel(definition), Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ADefinitionWithoutAnIdentifierFallsBackToItsAssetName()
        {
            var definition = UnitDefinition.CreateRuntime("소총병", 100);
            try
            {
                definition.name = "Rifleman";
                _progress.SetLevel("Rifleman", 4);

                Assert.That(
                    _progress.GetLevel(definition),
                    Is.EqualTo(4),
                    "식별자를 아직 채우지 않은 정의도 저장에 참여할 수 있어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void AMissingDefinitionIsAtTheStartingLevel()
        {
            Assert.That(_progress.GetLevel((UnitDefinition)null), Is.EqualTo(UnitLevelProgress.StartingLevel));
        }
    }
}
