using HS.Tactics.Progress;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 육성 레벨이 저장되고 그대로 되돌아오는지, 그리고 저장할 것이 있을 때만 저장하는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왕복이 이 검사의 중심이다.</b> 담고 꺼내는 두 방향이 서로 어긋나면 저장은 성공한 것처럼 보이는데
    /// 다음 실행에서 육성한 것이 사라진다. 그 어긋남은 저장하는 순간에는 아무 신호도 내지 않는다.
    /// </para>
    /// <para>
    /// <b>dirty 판정도 함께 본다.</b> 언제나 dirty이면 바뀐 것이 없어도 매번 파일을 쓰고,
    /// 반대로 바뀌었는데 dirty가 아니면 육성한 것이 저장되지 않는다.
    /// </para>
    /// </remarks>
    public sealed class UnitLevelSaveableTests
    {
        private UnitLevelProgress _progress;
        private UnitLevelSaveable _saveable;

        [SetUp]
        public void SetUp()
        {
            _progress = new UnitLevelProgress();
            _saveable = new UnitLevelSaveable(_progress);
        }

        [Test]
        public void RaisedLevelsSurviveASaveAndLoad()
        {
            _progress.SetLevel("unit.rifleman", 5);
            _progress.SetLevel("unit.sniper", 3);
            var captured = _saveable.CaptureState();

            var restored = new UnitLevelProgress();
            new UnitLevelSaveable(restored).RestoreState(captured);

            Assert.That(restored.GetLevel("unit.rifleman"), Is.EqualTo(5));
            Assert.That(restored.GetLevel("unit.sniper"), Is.EqualTo(3));
        }

        [Test]
        public void AKindThatWasNeverRaisedIsNotWrittenDown()
        {
            _progress.SetLevel("unit.rifleman", 5);

            var captured = _saveable.CaptureState();

            Assert.That(
                captured,
                Does.Not.Contain("unit.sniper"),
                "물으면 시작 레벨로 답하므로 적을 이유가 없다. 적으면 종류가 늘 때마다 파일이 커진다.");
        }

        [Test]
        public void LoadingReplacesWhatWasThereBefore()
        {
            _progress.SetLevel("unit.rifleman", 5);
            _progress.SetLevel("unit.medic", 2);
            var captured = new UnitLevelSaveable(new UnitLevelProgress()).CaptureState();

            _saveable.RestoreState(captured);

            Assert.That(
                _progress.RaisedCount,
                Is.Zero,
                "저장 파일에 없는 종류가 남으면 지난 판에서 키운 것이 되살아난다.");
        }

        [Test]
        public void AFreshStateIsDirtySoTheFirstSaveIsRecorded()
        {
            Assert.That(
                _saveable.IsDirty,
                Is.True,
                "첫 저장이 건너뛰어지면 저장 파일이 만들어지지 않아, 다음 실행이 복원할 것을 찾지 못한다.");
        }

        [Test]
        public void RaisingALevelMakesItDirty()
        {
            _progress.SetLevel("unit.rifleman", 5);

            Assert.That(_saveable.IsDirty, Is.True);
        }

        [Test]
        public void SavingClearsTheDirtyMark()
        {
            _progress.SetLevel("unit.rifleman", 5);
            _saveable.CaptureState();

            _saveable.AcknowledgeSaved();

            Assert.That(_saveable.IsDirty, Is.False);
        }

        [Test]
        public void ChangingDuringASaveLeavesItDirty()
        {
            _progress.SetLevel("unit.rifleman", 5);
            _saveable.CaptureState();
            _progress.SetLevel("unit.sniper", 2);

            _saveable.AcknowledgeSaved();

            Assert.That(
                _saveable.IsDirty,
                Is.True,
                "적어 둔 뒤에 바뀐 것은 아직 저장되지 않았다. 여기서 dirty를 지우면 그 변화를 잃는다.");
        }

        [Test]
        public void RestoringClearsTheDirtyMark()
        {
            var captured = _saveable.CaptureState();
            _progress.SetLevel("unit.rifleman", 5);

            _saveable.RestoreState(captured);

            Assert.That(_saveable.IsDirty, Is.False, "복원 직후는 저장소와 같으므로 저장할 것이 없다.");
        }

        [Test]
        public void BrokenOrEmptyStateIsIgnored()
        {
            _progress.SetLevel("unit.rifleman", 5);

            _saveable.RestoreState(null);
            _saveable.RestoreState("   ");

            Assert.That(
                _progress.GetLevel("unit.rifleman"),
                Is.EqualTo(5),
                "읽을 것이 없으면 아무 일도 하지 않는다. 비우면 첫 실행에서 육성한 것이 사라진다.");
        }

        [Test]
        public void TheSaveKeyIsSeparateFromStageProgress()
        {
            Assert.That(
                UnitLevelSaveable.DefaultSaveKey,
                Is.Not.EqualTo(Flow.StageProgressSaveable.DefaultSaveKey),
                "키가 같으면 한쪽이 다른 쪽을 덮어쓴다.");
        }
    }
}
