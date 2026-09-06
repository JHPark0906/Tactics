using System.Collections;
using System.Reflection;
using HS.Tactics.Character.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.PlayMode
{
    public sealed class FixedStepMotionInterpolatorStageTests
    {
        private GameObject _unit;

        [TearDown]
        public void TearDown()
        {
            if (_unit != null)
            {
                Object.DestroyImmediate(_unit);
            }
        }

        [UnityTest]
        public IEnumerator LateUpdateUsesTheFrameClockAndLeavesTheLogicPositionUnchanged()
        {
            _unit = new GameObject("Interpolated unit");
            _unit.SetActive(false);
            var mover = _unit.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(_unit.transform, false);
            var interpolator = _unit.AddComponent<FixedStepMotionInterpolator>();
            typeof(FixedStepMotionInterpolator).GetField("visualRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(interpolator, visual);
            _unit.SetActive(true);
            mover.Initialize(null);
            mover.SetPathPlanner((from, to, corners) =>
            {
                corners.Add(from);
                corners.Add(to);
                return true;
            });
            Assert.That(mover.MoveTo(Vector3.forward * 10f), Is.True);
            mover.Tick(0.1f);
            var logicPosition = mover.CurrentLogicPosition;
            Assert.That(logicPosition.z, Is.GreaterThan(0f));

            // 실제 프레임 시각이 고정 스텝 경계 사이에 놓이는 순간을 검사한다.
            var deadline = Time.realtimeSinceStartup + 2f;
            float expectedAlpha;
            do
            {
                yield return null;
                expectedAlpha = Mathf.Clamp01((float)(Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime);
            } while (expectedAlpha <= 0.001f && Time.realtimeSinceStartup < deadline);

            Assert.That(expectedAlpha, Is.GreaterThan(0.001f), "실제 프레임이 고정 스텝 사이까지 진행해야 한다.");
            typeof(FixedStepMotionInterpolator).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(interpolator, null);

            Assert.That(interpolator.LastAlpha, Is.EqualTo(expectedAlpha).Within(0.00001f));
            Assert.That(visual.position.z, Is.EqualTo(Vector3.Lerp(mover.PreviousLogicPosition, logicPosition, expectedAlpha).z)
                .Within(0.00001f));
            Assert.That(_unit.transform.position, Is.EqualTo(logicPosition));
        }
    }
}
