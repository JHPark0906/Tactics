using HS.Tactics.Progress;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 레벨과 경험치가 저장·복원 후 유지되는지 검증한다.
    /// 경험치 필드가 없는 저장 파일은 경험치 0으로 읽으며, 해당 형식과의 호환도 확인한다.
    /// </summary>
    public sealed class UnitGrowthPersistenceTests
    {
        private UnitLevelProgress _progress;
        private UnitLevelSaveable _saveable;
        private UnitLevelCurve _curve;

        [SetUp]
        public void SetUp()
        {
            _progress = new UnitLevelProgress();
            _saveable = new UnitLevelSaveable(_progress);
            _curve = ScriptableObject.CreateInstance<UnitLevelCurve>();
            JsonUtility.FromJsonOverwrite("{\"maxLevel\":5,\"xpToNext\":[100,200,300,400]}", _curve);
        }

        [TearDown]
        public void TearDown()
        {
            if (_curve != null)
            {
                Object.DestroyImmediate(_curve);
                _curve = null;
            }
        }

        [Test]
        public void ExperienceSurvivesASaveAndLoad()
        {
            _progress.AddExperience("unit.rifleman", 120, _curve);
            var expectedLevel = _progress.GetLevel("unit.rifleman");
            var expectedExperience = _progress.GetExperience("unit.rifleman");

            var restored = RoundTrip();

            Assert.That(restored.GetLevel("unit.rifleman"), Is.EqualTo(expectedLevel));
            Assert.That(restored.GetExperience("unit.rifleman"), Is.EqualTo(expectedExperience));
        }

        [Test]
        public void AKindThatOnlyGatheredExperienceIsStillStored()
        {
            _progress.AddExperience("unit.medic", 40, _curve);

            Assert.That(_progress.GetLevel("unit.medic"), Is.EqualTo(UnitLevelProgress.StartingLevel));
            Assert.That(
                _progress.RaisedCount,
                Is.EqualTo(1),
                "레벨만 보고 버리면 다음 레벨을 코앞에 둔 경험치가 저장에서 사라진다.");

            Assert.That(RoundTrip().GetExperience("unit.medic"), Is.EqualTo(40));
        }

        [Test]
        public void AKindAtTheStartingStateIsNotStored()
        {
            _progress.SetGrowth("unit.sniper", UnitGrowth.Start);

            Assert.That(
                _progress.RaisedCount,
                Is.Zero,
                "물으면 어차피 시작 상태로 답하므로, 담아 두면 저장 파일만 커진다.");
        }

        [Test]
        public void AFileWrittenBeforeExperienceExistedReadsAsNoExperience()
        {
            const string oldFormat = "{\"levels\":[{\"id\":\"unit.rifleman\",\"level\":5}]}";

            _saveable.RestoreState(oldFormat);

            Assert.That(_progress.GetLevel("unit.rifleman"), Is.EqualTo(5));
            Assert.That(
                _progress.GetExperience("unit.rifleman"),
                Is.EqualTo(UnitLevelProgress.StartingExperience),
                "칸이 없던 옛 파일은 아무것도 쌓지 않은 상태를 뜻한다.");
        }

        [Test]
        public void GainingExperienceMakesTheStateWorthSaving()
        {
            _saveable.AcknowledgeSaved();
            Assert.That(_saveable.IsDirty, Is.False);

            _progress.AddExperience("unit.rifleman", 10, _curve);

            Assert.That(_saveable.IsDirty, Is.True, "쌓인 경험치가 dirty를 세우지 않으면 저장되지 않는다.");
        }

        [Test]
        public void GainingNothingLeavesTheStateAlone()
        {
            _progress.AddExperience("unit.rifleman", 10, _curve);
            var revision = _progress.Revision;

            var gained = _progress.AddExperience("unit.rifleman", 0, _curve);

            Assert.That(gained, Is.Zero);
            Assert.That(
                _progress.Revision,
                Is.EqualTo(revision),
                "달라진 것이 없는데 판이 오르면 바뀐 것이 없어도 매번 파일을 쓴다.");
        }

        /// <summary>지금 상태를 저장했다가 새 상태로 되돌린다.</summary>
        /// <returns>저장한 내용으로 되돌린 상태이다.</returns>
        private UnitLevelProgress RoundTrip()
        {
            var captured = _saveable.CaptureState();
            var restored = new UnitLevelProgress();
            new UnitLevelSaveable(restored).RestoreState(captured);
            return restored;
        }
    }
}
