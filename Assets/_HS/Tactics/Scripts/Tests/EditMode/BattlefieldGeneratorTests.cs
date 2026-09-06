using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Scene;
using HS.Tactics.Battlefield;
using HS.Tactics.Lane;
using HS.Tactics.Pathfinding;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>전장 생성기가 스테이지 데이터대로 바닥·레인·경계를 세우고, 주입 때 진행률을 순서대로 보고하는지 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// EditMode라 <c>Awake</c>가 돌지 않으므로 <see cref="BattlefieldGenerator.GenerateBattlefield"/>를 직접 부른다.
    /// 프리팹 자리에는 빈 GameObject를 세운다 — 생성기는 프리팹의 모양을 보지 않고 자리만 정한다.
    /// 필드는 직렬화 경로로 넣는다(비공개 직렬화 필드라 편집기의 직렬화 경로를 쓴다).
    /// </para>
    /// <para>
    /// <see cref="BattleLane.Active"/>가 정적 참조를 들고 있으므로 레인 오브젝트는 반드시 <c>DestroyImmediate</c>로 지운다.
    /// </para>
    /// </remarks>
    public sealed class BattlefieldGeneratorTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<Object> _created = new();

        private GameObject _root;
        private BattlefieldGenerator _generator;
        private BattleLane _lane;

        [SetUp]
        public void SetUp()
        {
            _root = Track(new GameObject("Battlefield"));
            _generator = _root.AddComponent<BattlefieldGenerator>();
            _lane = Track(new GameObject("Lane")).AddComponent<BattleLane>();

            var serialized = new SerializedObject(_generator);
            serialized.FindProperty("stageData").objectReferenceValue = Track(StageData.CreateRuntime(84f));
            serialized.FindProperty("bodyTilePrefab").objectReferenceValue = Track(new GameObject("Tile"));
            serialized.FindProperty("capPrefab").objectReferenceValue = Track(new GameObject("Cap"));
            serialized.FindProperty("lane").objectReferenceValue = _lane;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void EightyFourMetersBecomesSevenBodyTilesTwoCapsAndABounds()
        {
            _generator.GenerateBattlefield();

            Assert.That(_root.transform.childCount, Is.EqualTo(10), "몸통 일곱 장, 끝단 둘, 경계 하나다.");
            var expectedZ = new[] { -36f, -24f, -12f, 0f, 12f, 24f, 36f };
            for (var index = 0; index < expectedZ.Length; index++)
            {
                var tile = _root.transform.Find($"BodyTile_{index}");
                Assert.That(tile, Is.Not.Null, $"{index}번 몸통 타일이 있어야 한다.");
                Assert.That(tile.position.z, Is.EqualTo(expectedZ[index]).Within(Tolerance));
                Assert.That(tile.position.x, Is.EqualTo(0f).Within(Tolerance));
            }

            var capPlus = _root.transform.Find("CapPlus");
            var capMinus = _root.transform.Find("CapMinus");
            Assert.That(capPlus.position.z, Is.EqualTo(42f).Within(Tolerance));
            Assert.That(capMinus.position.z, Is.EqualTo(-42f).Within(Tolerance));
            Assert.That(Quaternion.Angle(capMinus.rotation, Quaternion.Euler(0f, 180f, 0f)), Is.EqualTo(0f).Within(Tolerance),
                "-Z 끝단은 돌려 세워야 몸통 바깥으로 뻗는다.");
        }

        [Test]
        public void TheLaneRunsFromThePlusCapToTheMinusCap()
        {
            _generator.GenerateBattlefield();

            Assert.That(_lane.IsConfigured, Is.True);
            Assert.That(_lane.StartPosition.z, Is.EqualTo(42f).Within(Tolerance));
            Assert.That(_lane.EndPosition.z, Is.EqualTo(-42f).Within(Tolerance));
            Assert.That(_lane.ForwardTeam, Is.EqualTo(new TeamId(1)), "레인이 갖고 있던 정방향 진영은 그대로 남는다.");
        }

        [Test]
        public void TheBoundsCoverTheBodyAndBothCaps()
        {
            _generator.GenerateBattlefield();

            var bounds = _root.GetComponentInChildren<BattleBounds>();
            Assert.That(bounds, Is.Not.Null, "경계 컴포넌트가 자식에 있어야 한다.");
            Assert.That(bounds.Bounds.HalfExtents.X, Is.EqualTo(6f).Within(Tolerance));
            Assert.That(bounds.Bounds.HalfExtents.Z, Is.EqualTo(50f).Within(Tolerance));
            Assert.That(bounds.Bounds.Center.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(bounds.Bounds.Center.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void GeneratingTwiceDoesNotDuplicateAnything()
        {
            _generator.GenerateBattlefield();
            var childCountAfterFirst = _root.transform.childCount;

            _generator.GenerateBattlefield();

            Assert.That(_generator.IsGenerated, Is.True);
            Assert.That(_root.transform.childCount, Is.EqualTo(childCountAfterFirst), "두 번째 호출은 아무것도 더 세우지 않는다.");
        }

        [Test]
        public void WithoutStageDataNothingIsBuiltAndItWarns()
        {
            var serialized = new SerializedObject(_generator);
            serialized.FindProperty("stageData").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.Expect(LogType.Warning, new Regex("BattlefieldGenerator"));

            _generator.GenerateBattlefield();

            Assert.That(_root.transform.childCount, Is.Zero);
            Assert.That(_generator.IsGenerated, Is.False);
        }

        [Test]
        public void TheEnemyTeamDefaultsToTwo()
        {
            Assert.That(_generator.EnemyTeam, Is.EqualTo(new TeamId(2)), "플레이어가 1이므로 전장이 세우는 유닛은 2다.");
        }

        [Test]
        public void InjectionReportsZeroFirstAndOneLast()
        {
            var transition = new RecordingTransitionService();

            _generator.InjectRuntimeDependencies(transition, null);

            Assert.That(transition.Reports, Is.EqualTo(new[] { 0f, 1f }), "첫 줄이 0, 마지막 줄이 1을 보고한다.");
        }

        /// <summary>정리 목록에 등록하고 그대로 돌려준다.</summary>
        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>진행률 보고만 기록하는 테스트용 씬 전환 서비스이다.</summary>
        private sealed class RecordingTransitionService : ISceneTransitionService
        {
            public List<float> Reports { get; } = new();

            public bool IsLoading => true;

            public Observable<SceneTransitionState> State => Observable.Empty<SceneTransitionState>();

            public UniTask LoadSceneAsync(SceneReference scene) => UniTask.CompletedTask;

            public UniTask LoadSceneAfterLoadingSceneAsync(SceneReference loadingScene, SceneReference destination)
                => UniTask.CompletedTask;

            public void ReportInitializationProgress(float progress)
            {
                Reports.Add(progress);
            }
        }
    }
}
