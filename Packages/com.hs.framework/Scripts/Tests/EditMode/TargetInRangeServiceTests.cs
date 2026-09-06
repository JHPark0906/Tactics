using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>거리 표식 서비스가 경계를 넘는 순간에만 문맥을 바꾸는지 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// <b>이 서비스가 지키는 것.</b> 선택 자리는 진행 중인 자식을 기억해, 대상이 거리 안으로
    /// 들어와도 추격을 멈추지 못한다. 표식이 생기는 순간 조건 자리가 추격을 끊어야 하므로,
    /// <b>표식은 값이 아니라 있고 없음으로 말해야 하고</b>, <b>경계를 넘을 때만 알림이 나가야 한다.</b>
    /// </para>
    /// <para>
    /// 알림이 매 간격마다 나가면 거리 안에 서 있는 동안 진행 중인 가지가 끊임없이 끊긴다.
    /// 그것을 막는 것은 문맥이 같은 값을 다시 써도 알리지 않는다는 성질인데,
    /// 그 성질에 기대고 있다는 사실을 여기서 검사로 붙잡아 둔다.
    /// </para>
    /// </remarks>
    public sealed class TargetInRangeServiceTests
    {
        private const string TargetKey = "target";
        private const string InRangeKey = "target.inRange";
        private const float Range = 10f;

        private readonly List<GameObject> _createdObjects = new();

        private Transform _self;
        private Transform _target;
        private BehaviourContext _context;
        private BehaviourTreeInstance _tree;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _self = CreateObject("Self", Vector3.zero).transform;
            _target = CreateObject("Target", new Vector3(0f, 0f, 5f)).transform;
            _context = new BehaviourContext();
            _now = 0f;

            // 서비스는 꾸미는 자리라 홀로 돌 수 없다. 언제나 진행 중인 자식 하나를 아래에 둔다.
            _tree = new BehaviourTreeInstance();
            var service = _tree.SetRoot(new TargetInRangeService(_self, Range, 0f, TargetKey, InRangeKey, () => _now));
            _tree.AddChild(service, new ActionBehaviour(_ => BehaviourStatus.Running));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TheMarkAppearsWhenTheTargetIsWithinRange()
        {
            _context.SetValue(TargetKey, _target);

            _tree.Tick(_context);

            Assert.That(HasMark(), Is.True, "거리 안이면 표식이 있어야 조건 자리가 추격을 끊는다.");
        }

        [Test]
        public void TheMarkDisappearsWhenTheTargetLeavesRange()
        {
            _context.SetValue(TargetKey, _target);
            _tree.Tick(_context);
            Assert.That(HasMark(), Is.True);

            _target.position = new Vector3(0f, 0f, Range + 1f);
            _tree.Tick(_context);

            Assert.That(HasMark(), Is.False, "거리를 벗어나면 표식이 사라져야 한다. false를 담으면 값이 있는 것으로 읽힌다.");
        }

        [Test]
        public void ThereIsNoMarkWithoutATarget()
        {
            _tree.Tick(_context);

            Assert.That(HasMark(), Is.False, "대상이 없으면 거리 안일 수 없다.");
        }

        [Test]
        public void AMarkLeftFromAnOldTargetIsClearedWhenTheTargetIsGone()
        {
            _context.SetValue(TargetKey, _target);
            _tree.Tick(_context);
            Assert.That(HasMark(), Is.True);

            _context.RemoveValue(TargetKey);
            _tree.Tick(_context);

            Assert.That(HasMark(), Is.False, "대상이 사라졌는데 표식이 남으면 없는 대상에 대해 거리 안이라고 믿는다.");
        }

        [Test]
        public void StayingInRangeDoesNotNotifyAgain()
        {
            _context.SetValue(TargetKey, _target);
            var notifications = 0;
            using var watching = _context.Observe(InRangeKey, _ => notifications++);

            _tree.Tick(_context);
            _tree.Tick(_context);
            _tree.Tick(_context);

            Assert.That(notifications, Is.EqualTo(1),
                "거리 안에 계속 있는 동안 매 간격마다 알리면 진행 중인 가지가 끊임없이 끊긴다. 경계를 넘을 때만 알려야 한다.");
        }

        [Test]
        public void StayingOutOfRangeDoesNotNotifyAtAll()
        {
            _target.position = new Vector3(0f, 0f, Range + 1f);
            _context.SetValue(TargetKey, _target);
            var notifications = 0;
            using var watching = _context.Observe(InRangeKey, _ => notifications++);

            _tree.Tick(_context);
            _tree.Tick(_context);

            Assert.That(notifications, Is.Zero, "없는 표식을 다시 지우는 것은 바뀐 것이 없으므로 알리지 않는다.");
        }

        [Test]
        public void CrossingTheBoundaryNotifiesOncePerCrossing()
        {
            _context.SetValue(TargetKey, _target);
            var notifications = 0;
            using var watching = _context.Observe(InRangeKey, _ => notifications++);

            _tree.Tick(_context);
            _target.position = new Vector3(0f, 0f, Range + 1f);
            _tree.Tick(_context);
            _target.position = new Vector3(0f, 0f, 5f);
            _tree.Tick(_context);

            Assert.That(notifications, Is.EqualTo(3), "경계를 넘을 때마다 정확히 한 번씩 알린다.");
        }

        [Test]
        public void TheBoundaryItselfCountsAsInRange()
        {
            _target.position = new Vector3(0f, 0f, Range);
            _context.SetValue(TargetKey, _target);

            _tree.Tick(_context);

            Assert.That(HasMark(), Is.True, "딱 거리에 선 대상은 안으로 본다. 밖으로 보면 거리 끝에서 쏠 수 없다.");
        }

        [Test]
        public void TheServiceWaitsOutItsIntervalBeforeMeasuringAgain()
        {
            _context.SetValue(TargetKey, _target);
            var tree = new BehaviourTreeInstance();
            var service = tree.SetRoot(new TargetInRangeService(_self, Range, 1f, TargetKey, InRangeKey, () => _now));
            tree.AddChild(service, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(_context);
            _target.position = new Vector3(0f, 0f, Range + 1f);
            _now = 0.5f;
            tree.Tick(_context);
            Assert.That(HasMark(), Is.True, "간격이 차기 전에는 다시 재지 않는다.");

            _now = 1f;
            tree.Tick(_context);
            Assert.That(HasMark(), Is.False);
        }

        /// <summary>거리 표식이 문맥에 있는지 본다. 값이 아니라 있고 없음이 기준이다.</summary>
        private bool HasMark() => _context.TryGetValue<object>(InRangeKey, out _);

        private GameObject CreateObject(string objectName, Vector3 position)
        {
            var created = new GameObject(objectName);
            created.transform.position = position;
            _createdObjects.Add(created);
            return created;
        }
    }
}
