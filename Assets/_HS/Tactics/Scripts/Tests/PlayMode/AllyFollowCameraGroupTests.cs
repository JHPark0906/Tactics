using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Cameras;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// <see cref="AllyFollowCameraGroup"/>이 배치 컨트롤러와 사망 이벤트를 듣고 대상 그룹의 멤버를 넣고 빼는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이벤트는 프로덕션 경로로 일으킨다.</b> 배치·회수는 실제 <see cref="UnitPlacementController"/>의 공개 메서드
    /// (<c>TryPlaceUnitAtCell</c>·<c>TryRecallUnit</c>)로, 사망은 컨트롤러와 컴포넌트가 주입받는 것과 같은 창구
    /// (<see cref="TestMessageChannel{TMessage}"/>)로 발행한다. 컴포넌트의 <c>[Inject]</c> 메서드를 직접 부르는 것은 컨테이너가 하는
    /// 일을 대신하는 것이지 검사용 통로가 아니다.
    /// </para>
    /// <para>
    /// <b>PlayMode인 이유.</b> 컴포넌트는 유닛의 진영을 <see cref="TacticalUnit.Team"/>으로 읽는데, 그 값은 유닛이 깨어나 조립될 때
    /// 채워진다. EditMode에서는 스폰된 유닛이 깨어나지 않아 진영이 비고, 그러면 아군 판정이 늘 거짓이 되어 검사가 아무것도 보지 못한다.
    /// </para>
    /// <para>
    /// 컨트롤러와 컴포넌트의 직렬화 필드(배치 구역·진영)는 인스펙터가 채우는 값이라 무대 준비로 리플렉션으로 넣는다 — 프로덕션 통로가
    /// 아니다. 컨트롤러는 켜지는 순간 배치 단계를 시작하므로 꺼진 채 만들어 구역을 넣은 뒤 켠다.
    /// </para>
    /// </remarks>
    public sealed class AllyFollowCameraGroupTests
    {
        private static readonly TeamId AllyTeam = new(1);
        private static readonly TeamId OtherTeam = new(2);

        private readonly List<Object> _created = new();
        private UnitPlacementController _controller;
        private CinemachineTargetGroup _targetGroup;
        private TestMessageChannel<DeathEvent> _deathChannel;
        private UnitDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = Track(UnitDefinition.CreateRuntime("Rifleman", 100, unitPrefab: CreateUnitTemplate()));
            _deathChannel = new TestMessageChannel<DeathEvent>();

            var zoneObject = new GameObject("PlaceZone");
            _created.Add(zoneObject);
            var zone = zoneObject.AddComponent<UnitPlacementZone>();

            // 켜지는 순간 배치 단계를 시작하며 구역을 읽으므로, 꺼진 채 만들어 구역을 넣은 뒤 켠다.
            var controllerObject = new GameObject("PlacementController");
            _created.Add(controllerObject);
            controllerObject.SetActive(false);
            _controller = controllerObject.AddComponent<UnitPlacementController>();
            SetField(_controller, "placementZone", zone);
            controllerObject.SetActive(true);

            var groupObject = new GameObject("AllyTargetGroup");
            _created.Add(groupObject);
            _targetGroup = groupObject.AddComponent<CinemachineTargetGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in Object.FindObjectsByType<TacticalUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                _created.Add(unit.gameObject);
            }

            foreach (var created in _created)
            {
                if (created == null)
                {
                    continue;
                }

                if (created is GameObject createdObject)
                {
                    createdObject.SetActive(false);
                }

                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void APlacedAllyBecomesAMemberOfTheTargetGroup()
        {
            CreateCameraGroup(AllyTeam);

            var unit = PlaceUnit(cellIndex: 4);

            Assert.That(_targetGroup.FindMember(unit.transform), Is.GreaterThanOrEqualTo(0), "배치된 아군이 대상 그룹에 없다.");
            Assert.That(_targetGroup.Targets.Count, Is.EqualTo(1), $"멤버가 {_targetGroup.Targets.Count}명이다. 아군 하나를 세웠으니 하나여야 한다.");
        }

        [Test]
        public void ARecalledUnitLeavesTheTargetGroup()
        {
            CreateCameraGroup(AllyTeam);
            var unit = PlaceUnit(cellIndex: 4);
            Assert.That(_targetGroup.FindMember(unit.transform), Is.GreaterThanOrEqualTo(0), "무대 확인: 세운 아군이 그룹에 들어가야 한다.");

            var entryId = _controller.Plan.Entries[0].Id;
            var result = _controller.TryRecallUnit(entryId);

            Assert.That(result, Is.EqualTo(PlacementResult.Success), $"회수 결과가 {result}이다.");
            Assert.That(_targetGroup.Targets.Count, Is.EqualTo(0), $"회수한 뒤에도 멤버가 {_targetGroup.Targets.Count}명 남았다.");
        }

        [Test]
        public void ADeadUnitLeavesTheTargetGroup()
        {
            CreateCameraGroup(AllyTeam);
            var unit = PlaceUnit(cellIndex: 4);
            Assert.That(_targetGroup.FindMember(unit.transform), Is.GreaterThanOrEqualTo(0), "무대 확인: 세운 아군이 그룹에 들어가야 한다.");

            _deathChannel.Publish(new DeathEvent(unit.gameObject, null));

            Assert.That(_targetGroup.FindMember(unit.transform), Is.LessThan(0), "죽은 유닛이 대상 그룹에 남아 있다.");
        }

        [Test]
        public void AUnitOfAnotherTeamIsNotAddedToTheTargetGroup()
        {
            // 컨트롤러는 진영 1로 세우므로, 컴포넌트가 진영 2를 따라가게 하면 세운 유닛은 아군이 아니다.
            CreateCameraGroup(OtherTeam);

            PlaceUnit(cellIndex: 4);

            Assert.That(_targetGroup.Targets.Count, Is.EqualTo(0), $"다른 진영의 유닛이 대상 그룹에 들어갔다(멤버 {_targetGroup.Targets.Count}명).");
        }

        /// <summary>추적 컴포넌트를 만들어 그룹과 진영을 넣고, 컨테이너가 하듯 컨트롤러와 사망 창구를 주입한다.</summary>
        private AllyFollowCameraGroup CreateCameraGroup(TeamId allyTeam)
        {
            var hostObject = new GameObject("AllyFollowCamera");
            _created.Add(hostObject);
            hostObject.SetActive(false);
            var cameraGroup = hostObject.AddComponent<AllyFollowCameraGroup>();
            SetField(cameraGroup, "targetGroup", _targetGroup);
            SetField(cameraGroup, "allyTeamId", allyTeam);
            hostObject.SetActive(true);

            cameraGroup.InjectDependencies(_controller, _deathChannel);
            return cameraGroup;
        }

        /// <summary>
        /// 컨트롤러로 유닛 하나를 격자 칸에 세운다. 컨트롤러에 컨테이너가 없어 스포너가 한 번 경고하고, 대역 정의에 어빌리티 집합이,
        /// 대역 프리팹에 행동 트리 에셋이 없어 조립이 그 둘을 이 순서로 경고한다.
        /// </summary>
        private TacticalUnit PlaceUnit(int cellIndex)
        {
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이"));
            LogAssert.Expect(LogType.Warning, new Regex("체력 어트리뷰트"));
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));

            var result = _controller.TryPlaceUnitAtCell(_definition, cellIndex, out var unit);
            Assert.That(result, Is.EqualTo(PlacementResult.Success), $"무대 확인: 배치 결과가 {result}이다.");
            Assert.That(unit, Is.Not.Null, "무대 확인: 유닛이 세워지지 않았다.");
            Assert.That(unit.Team, Is.Not.Null, "무대 확인: 세운 유닛의 진영 구성요소가 없다.");
            Assert.That(unit.Team.TeamId, Is.EqualTo(AllyTeam), $"무대 확인: 세운 유닛의 진영이 {unit.Team.TeamId}이다.");
            return unit;
        }

        /// <summary>
        /// 비활성으로 저장된 프리팹처럼, 꺼진 채 <see cref="TacticalUnit"/>만 붙인 대역을 만든다.
        /// PlayMode에서 활성 오브젝트에 붙이면 그 자리에서 깨어나 정의 없이 조립되므로 먼저 끈다.
        /// </summary>
        private GameObject CreateUnitTemplate()
        {
            var template = new GameObject("UnitTemplate");
            _created.Add(template);
            template.SetActive(false);
            template.AddComponent<TacticalUnit>();
            return template;
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }

        /// <summary>인스펙터가 채우는 직렬화 필드를 무대 준비로 넣는다. 필드 이름이 바뀌면 여기서 걸린다.</summary>
        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했다.");
            field.SetValue(target, value);
        }
    }
}
