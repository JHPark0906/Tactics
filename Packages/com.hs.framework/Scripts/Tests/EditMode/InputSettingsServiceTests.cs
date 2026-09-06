using HS.Framework.Settings;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>입력 설정 서비스의 차단 토큰과 기본 상태 합성 규칙을 검증한다.</summary>
    public sealed class InputSettingsServiceTests
    {
        [Test]
        public void AcquireInputBlockDisablesInputUntilTokenIsDisposed()
        {
            var service = new InputSettingsService();
            Assert.That(service.IsInputEnabled, Is.True);

            var token = service.AcquireInputBlock();
            Assert.That(service.IsInputEnabled, Is.False);

            token.Dispose();
            Assert.That(service.IsInputEnabled, Is.True);
        }

        [Test]
        public void InputStaysBlockedUntilTheLastTokenIsDisposed()
        {
            var service = new InputSettingsService();
            var firstToken = service.AcquireInputBlock();
            var secondToken = service.AcquireInputBlock();

            firstToken.Dispose();
            Assert.That(service.IsInputEnabled, Is.False);

            secondToken.Dispose();
            Assert.That(service.IsInputEnabled, Is.True);
        }

        [Test]
        public void SetInputEnabledTrueIsDeferredWhileTokenIsAlive()
        {
            var service = new InputSettingsService();
            service.SetInputEnabled(false);
            var token = service.AcquireInputBlock();

            service.SetInputEnabled(true);
            Assert.That(service.IsInputEnabled, Is.False, "토큰이 살아 있는 동안에는 활성화가 지연되어야 한다.");

            token.Dispose();
            Assert.That(service.IsInputEnabled, Is.True, "마지막 토큰 해제 시 기본 상태가 반영되어야 한다.");
        }

        [Test]
        public void SetInputEnabledFalseDuringBlockPersistsAfterRelease()
        {
            var service = new InputSettingsService();
            var token = service.AcquireInputBlock();

            service.SetInputEnabled(false);
            token.Dispose();

            Assert.That(service.IsInputEnabled, Is.False);
        }

        [Test]
        public void DisposingTheSameTokenTwiceReleasesOnlyOnce()
        {
            var service = new InputSettingsService();
            var firstToken = service.AcquireInputBlock();
            var secondToken = service.AcquireInputBlock();

            firstToken.Dispose();
            firstToken.Dispose();
            Assert.That(service.IsInputEnabled, Is.False, "중복 해제가 다른 토큰의 차단을 무효화하면 안 된다.");

            secondToken.Dispose();
            Assert.That(service.IsInputEnabled, Is.True);
        }

        [Test]
        public void TokenAcquireAndReleaseDoNotRaiseSettingsChangedEvent()
        {
            var service = new InputSettingsService();
            var changedCount = 0;
            service.OnInputSettingsChanged += _ => changedCount++;

            var token = service.AcquireInputBlock();
            token.Dispose();

            Assert.That(changedCount, Is.Zero, "차단 토큰은 설정 변경이 아니므로 알림을 발생시키면 안 된다.");
        }
    }
}
