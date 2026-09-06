using HS.Tactics.Placement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>배치 구역의 포함 판정과 좌표 보정을 검증한다.</summary>
    public sealed class PlacementAreaTests
    {
        [Test]
        public void AreaContainsPositionsInsideTheBox()
        {
            var area = new PlacementArea(new Vector3(10f, 0f, 0f), new Vector3(4f, 2f, 6f));

            Assert.That(area.Contains(new Vector3(10f, 0f, 0f)), Is.True);
            Assert.That(area.Contains(new Vector3(11.9f, 0.9f, 2.9f)), Is.True);
            Assert.That(area.Contains(new Vector3(13f, 0f, 0f)), Is.False, "x축 범위를 벗어난 좌표이다.");
        }

        [Test]
        public void HeightIsIgnoredWhenJudgingGroundPositions()
        {
            var area = new PlacementArea(Vector3.zero, new Vector3(4f, 1f, 4f));
            var highAboveGround = new Vector3(1f, 50f, 1f);

            Assert.That(area.Contains(highAboveGround), Is.False, "높이를 포함하면 범위를 벗어난다.");
            Assert.That(
                area.ContainsIgnoringHeight(highAboveGround),
                Is.True,
                "지면 클릭 좌표의 높이 오차는 배치 판정에 영향을 주지 않아야 한다.");
        }

        [Test]
        public void NegativeSizeIsNormalizedToAbsoluteLengths()
        {
            var area = new PlacementArea(Vector3.zero, new Vector3(-4f, -2f, -6f));

            Assert.That(area.Size, Is.EqualTo(new Vector3(4f, 2f, 6f)));
            Assert.That(area.ContainsIgnoringHeight(new Vector3(1.9f, 0f, 2.9f)), Is.True);
        }

        [Test]
        public void ClampPullsOutsidePositionsBackIntoTheArea()
        {
            var area = new PlacementArea(Vector3.zero, new Vector3(4f, 2f, 4f));

            var clamped = area.ClampToArea(new Vector3(100f, 0f, -100f));

            Assert.That(area.Contains(clamped), Is.True);
            Assert.That(clamped.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(clamped.z, Is.EqualTo(-2f).Within(0.001f));
        }

        [Test]
        public void ClampKeepsPositionsThatAreAlreadyInside()
        {
            var area = new PlacementArea(Vector3.zero, new Vector3(4f, 2f, 4f));
            var inside = new Vector3(1f, 0f, 1f);

            Assert.That(area.ClampToArea(inside), Is.EqualTo(inside));
        }

        [Test]
        public void AreaWithoutFootprintIsEmptyAndContainsNothing()
        {
            var empty = PlacementArea.None;

            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.Contains(Vector3.zero), Is.False);
            Assert.That(empty.ContainsIgnoringHeight(Vector3.zero), Is.False);
            Assert.That(empty.ClampToArea(Vector3.one), Is.EqualTo(Vector3.one), "보정할 기준이 없으면 좌표를 그대로 둔다.");
        }
    }
}
