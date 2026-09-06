using HS.Tactics.Battlefield;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>표본 스테이지 에셋 Stage1이 생성기가 읽을 수 있는 값을 담고 있는지 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// 표본 전장 길이는 84이며, 배치 구역과 카메라가 그 길이에 맞춰
    /// 손으로 놓여 있다. 경계는 x ∈ (−6, 6)·z ∈ (−50, 50)이고 도로면은 y=0.1이다 — 그 밖의 좌표는 유닛이 걷지 못하거나
    /// 발이 바닥에 묻힌다.
    /// </para>
    /// <para>
    /// 배치 넷은 z=−40 한 줄에 3 m 간격이다. 유닛 반지름을 감안하면 2 m 아래로 붙이면 서로 겹쳐 서므로,
    /// 간격의 이유인 그 하한을 검사로 고정한다. 수·자리·레벨은 스테이지 에셋에 저장된 표본 값이다.
    /// </para>
    /// </remarks>
    public sealed class Stage1AssetTests
    {
        private const string AssetPath = "Assets/_HS/Tactics/Definitions/Stages/Stage1.asset";
        private const float RoadSurfaceY = 0.1f;
        private const float MinimumSpacing = 2f;

        [Test]
        public void TheBattlefieldIsEightyFourMetersLikeLevel1()
        {
            var stage = LoadStage();

            Assert.That(stage.BattlefieldLength, Is.EqualTo(84f));
        }

        [Test]
        public void FourLevelOnePlacementsWithSpawnableDefinitions()
        {
            var stage = LoadStage();

            Assert.That(stage.UnitPlacements, Has.Count.EqualTo(4), "표본은 적 넷을 세운다.");
            foreach (var placement in stage.UnitPlacements)
            {
                Assert.That(placement.Definition, Is.Not.Null, $"{placement}의 종류가 비어 있다.");
                Assert.That(placement.Definition.HasUnitPrefab, Is.True, $"{placement}의 종류에 프리팹이 없어 세울 수 없다.");
                Assert.That(placement.Level, Is.EqualTo(1), $"{placement}의 레벨이 1이 아니다.");
            }
        }

        [Test]
        public void EveryPlacementStandsOnTheRoadInsideTheBounds()
        {
            var stage = LoadStage();

            foreach (var placement in stage.UnitPlacements)
            {
                var position = placement.Position;
                Assert.That(Mathf.Abs(position.x), Is.LessThan(6f), $"{placement}이 폭 방향으로 바닥 밖이다.");
                Assert.That(Mathf.Abs(position.z), Is.LessThan(50f), $"{placement}이 길이 방향으로 바닥 밖이다.");
                Assert.That(position.y, Is.EqualTo(RoadSurfaceY).Within(0.0001f), $"{placement}이 도로면 높이에 서지 않는다.");
            }
        }

        [Test]
        public void NoTwoPlacementsAreCloserThanTwoMeters()
        {
            var stage = LoadStage();
            var placements = stage.UnitPlacements;

            for (var first = 0; first < placements.Count; first++)
            {
                for (var second = first + 1; second < placements.Count; second++)
                {
                    var a = placements[first].Position;
                    var b = placements[second].Position;
                    var distance = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

                    Assert.That(
                        distance,
                        Is.GreaterThanOrEqualTo(MinimumSpacing),
                        $"{placements[first]}과 {placements[second]}이 {distance:F2} m로 붙어 있어 겹쳐 선다.");
                }
            }
        }

        /// <summary>표본 에셋을 읽는다. 없으면 검사가 그 자리에서 실패한다.</summary>
        private static StageData LoadStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(AssetPath);
            Assert.That(stage, Is.Not.Null, $"표본 에셋이 {AssetPath}에 있어야 한다.");
            return stage;
        }
    }
}
