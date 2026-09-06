using System.Text.RegularExpressions;
using HS.Tactics.Progress;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 경험치가 레벨로 바뀌는 규칙과, 곡선이 그 규칙에 값을 대는 방식을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>경계가 이 검사의 중심이다.</b> 필요한 만큼 <b>정확히</b> 모였을 때 오르는지, 하나 모자랄 때
    /// 멈추는지는 부등호 하나로 갈리는데, 어느 쪽으로 틀려도 화면에는 "레벨이 안 오른다"로만 보인다.
    /// </para>
    /// <para>
    /// <b>배수가 데이터에서 온다는 것도 함께 고정한다.</b> 코드가 어느 레벨을 특별 취급하기 시작하면
    /// 에셋의 그 칸은 바꿔도 아무 일이 일어나지 않는 죽은 값이 되는데, 그 사실은 아무 신호도 내지 않는다.
    /// </para>
    /// </remarks>
    public sealed class UnitLevelUpTests
    {
        private UnitLevelCurve _curve;

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
        public void ExactlyEnoughExperienceRaisesTheLevel()
        {
            _curve = CreateCurve("{\"maxLevel\":5,\"xpToNext\":[100,200,300,400]}");

            var result = UnitLevelUp.Apply(1, 0, 100, _curve);

            Assert.That(result.Level, Is.EqualTo(2));
            Assert.That(result.Experience, Is.Zero, "쓴 만큼 줄지 않으면 다음 레벨이 공짜가 된다.");
            Assert.That(result.GainedLevels, Is.EqualTo(1));
        }

        [Test]
        public void OneShortOfTheRequirementKeepsTheLevel()
        {
            _curve = CreateCurve("{\"maxLevel\":5,\"xpToNext\":[100,200,300,400]}");

            var result = UnitLevelUp.Apply(1, 0, 99, _curve);

            Assert.That(result.Level, Is.EqualTo(1));
            Assert.That(result.Experience, Is.EqualTo(99), "모자란 경험치는 쌓인 채로 남아야 한다.");
            Assert.That(result.GainedLevels, Is.Zero);
        }

        [Test]
        public void OneGrantCanRaiseSeveralLevels()
        {
            _curve = CreateCurve("{\"maxLevel\":5,\"xpToNext\":[100,200,300,400]}");

            var result = UnitLevelUp.Apply(1, 0, 350, _curve);

            Assert.That(result.Level, Is.EqualTo(3), "한 번에 한 레벨만 오르면 남은 몫이 잠긴 채 멈춘다.");
            Assert.That(result.Experience, Is.EqualTo(50));
            Assert.That(result.GainedLevels, Is.EqualTo(2));
        }

        [Test]
        public void TheMaximumLevelStopsTheRiseAndDropsTheRest()
        {
            _curve = CreateCurve("{\"maxLevel\":3,\"xpToNext\":[100,200,300,400]}");

            var result = UnitLevelUp.Apply(1, 0, 100000, _curve);

            Assert.That(result.Level, Is.EqualTo(3));
            Assert.That(result.GainedLevels, Is.EqualTo(2));
            Assert.That(
                result.Experience,
                Is.Zero,
                "남겨 두면 최대 레벨을 올린 날 플레이하지 않은 몫이 한꺼번에 들어온다.");
        }

        [Test]
        public void WithoutACurveNothingMoves()
        {
            var result = UnitLevelUp.Apply(4, 30, 5000, null);

            Assert.That(result.Level, Is.EqualTo(4));
            Assert.That(result.Experience, Is.EqualTo(30), "곡선 없이 올리면 그 값이 어디서 왔는지 알 수 없다.");
            Assert.That(result.GainedLevels, Is.Zero);
        }

        [Test]
        public void TheHighestLevelReportsNothingLeftToPayFor()
        {
            _curve = CreateCurve("{\"maxLevel\":3,\"xpToNext\":[100,200,300]}");

            Assert.That(
                _curve.GetXpToNext(3),
                Is.Zero,
                "큰 수를 돌려주면 '아직 모자란 것'과 '더 오를 수 없는 것'을 구별할 수 없다.");
        }

        [Test]
        public void TheFirstLevelReadsItsMultiplierFromTheData()
        {
            _curve = CreateCurve("{\"maxLevel\":3,\"healthMultipliers\":[1.5,2.0,2.5]}");

            Assert.That(
                _curve.GetHealthMultiplier(1),
                Is.EqualTo(1.5f).Within(0.0001f),
                "레벨 1을 코드에서 1.0으로 못 박으면 에셋의 첫 칸이 죽은 값이 된다.");
        }

        [Test]
        public void ALevelBeyondTheDataHoldsTheLastValueAndSaysSo()
        {
            _curve = CreateCurve("{\"maxLevel\":5,\"attackMultipliers\":[1.0,1.2]}");
            LogAssert.Expect(LogType.Warning, new Regex("UnitLevelCurve"));

            Assert.That(
                _curve.GetAttackMultiplier(5),
                Is.EqualTo(1.2f).Within(0.0001f),
                "1로 물러나면 레벨이 올랐는데 스탯이 도로 내려간다.");
        }

        /// <summary>적힌 값만 채운 곡선을 만든다. 적지 않은 칸은 에셋의 기본값 그대로이다.</summary>
        /// <param name="json">채울 값이 담긴 JSON이다.</param>
        /// <returns>만들어진 곡선이다.</returns>
        private static UnitLevelCurve CreateCurve(string json)
        {
            var curve = ScriptableObject.CreateInstance<UnitLevelCurve>();
            JsonUtility.FromJsonOverwrite(json, curve);
            return curve;
        }
    }
}
