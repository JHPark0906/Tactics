using HS.Tactics.Battlefield;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 전장 길이에서 타일 위치·회전·경계를 계산하는 산술을 검증한다.
    /// 길이 84의 표본은 몸통 일곱 장을 z=−36부터 36까지 12 간격으로 놓고 끝단은 ±42, 경계 반크기는 (6, 50)이다.
    /// </summary>
    public sealed class BattlefieldTilingTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void EightyFourMetersNeedsSevenBodyTiles()
        {
            Assert.That(BattlefieldTiling.GetBodyTileCount(84f), Is.EqualTo(7));
        }

        [Test]
        public void ALengthThatIsNotAMultipleOfTheTileSizeRoundsUp()
        {
            Assert.That(BattlefieldTiling.GetBodyTileCount(85f), Is.EqualTo(8), "85는 일곱 장으로 다 덮이지 않으므로 한 장 더 깐다.");
        }

        [Test]
        public void TheShortestBattlefieldStillGetsOneBodyTile()
        {
            Assert.That(BattlefieldTiling.GetBodyTileCount(1f), Is.EqualTo(1));
        }

        [Test]
        public void SevenBodyTilesAreCenteredOnTheOriginTwelveMetersApart()
        {
            var expectedZ = new[] { -36f, -24f, -12f, 0f, 12f, 24f, 36f };

            for (var index = 0; index < expectedZ.Length; index++)
            {
                var position = BattlefieldTiling.GetBodyTilePosition(index, 7);

                Assert.That(position.x, Is.EqualTo(0f).Within(Tolerance), $"{index}번 타일은 폭 방향 가운데에 선다.");
                Assert.That(position.y, Is.EqualTo(0f).Within(Tolerance), $"{index}번 타일은 바닥 높이에 선다.");
                Assert.That(position.z, Is.EqualTo(expectedZ[index]).Within(Tolerance), $"{index}번 타일의 길이 방향 자리이다.");
            }
        }

        [Test]
        public void ASingleBodyTileSitsOnTheOrigin()
        {
            var position = BattlefieldTiling.GetBodyTilePosition(0, 1);

            Assert.That(position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheCapsSitAtBothEndsOfTheBody()
        {
            Assert.That(BattlefieldTiling.GetPlusCapPosition(7).z, Is.EqualTo(42f).Within(Tolerance));
            Assert.That(BattlefieldTiling.GetMinusCapPosition(7).z, Is.EqualTo(-42f).Within(Tolerance));
            Assert.That(BattlefieldTiling.GetPlusCapPosition(7).x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(BattlefieldTiling.GetMinusCapPosition(7).x, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TheMinusCapIsTurnedAroundSoItExtendsOutward()
        {
            Assert.That(
                Quaternion.Angle(BattlefieldTiling.PlusCapRotation, Quaternion.identity),
                Is.EqualTo(0f).Within(Tolerance),
                "+Z 끝단은 프리팹이 뻗는 방향 그대로 둔다.");
            Assert.That(
                Quaternion.Angle(BattlefieldTiling.MinusCapRotation, Quaternion.Euler(0f, 180f, 0f)),
                Is.EqualTo(0f).Within(Tolerance),
                "−Z 끝단은 180도 돌려야 몸통 바깥으로 뻗는다.");
        }

        [Test]
        public void TheBoundsCoverTheBodyAndBothCaps()
        {
            var halfExtents = BattlefieldTiling.GetBoundsHalfExtents(7);

            Assert.That(halfExtents.x, Is.EqualTo(6f).Within(Tolerance), "폭 12의 절반이다.");
            Assert.That(halfExtents.y, Is.EqualTo(50f).Within(Tolerance), "몸통 절반 42에 끝단 8을 더한 값이다.");
        }
    }
}
