using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 기본값이 바뀌기 직전에 끼어드는 필터와, 변화 알림에 실리는 원인을 검증한다.
    /// </summary>
    /// <remarks>
    /// 피해를 가로채는 보호막과 체력이 0에 닿는 것을 지켜보는 사망 처리가 이 자리 위에 선다.
    /// 필터가 기본값 경로에만 걸리고 수정자 경로에는 걸리지 않는다는 구분이 이 파일의 핵심이다.
    /// </remarks>
    public sealed class AttributeBaseValueFilterTests
    {
        private readonly List<AttributeDefinition> _createdDefinitions = new();
        private AttributeSet _attributes;
        private AttributeDefinition _health;
        private GameObject _instigator;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _health = CreateDefinition("Health", 100f);
            _attributes.AddAttribute(_health);
            _instigator = new GameObject("Instigator");
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            _attributes = null;
            if (_instigator != null)
            {
                Object.DestroyImmediate(_instigator);
                _instigator = null;
            }

            foreach (var definition in _createdDefinitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            _createdDefinitions.Clear();
        }

        [Test]
        public void AFilterSeesTheProposedValueAndTheCause()
        {
            var filter = new RecordingFilter();
            _attributes.AddBaseValueFilter(_health, filter);
            var context = new AttributeChangeContext("shot", _instigator);

            _attributes.AddToBaseValue(_health, -30f, context);

            Assert.That(filter.CallCount, Is.EqualTo(1));
            Assert.That(filter.LastCurrent, Is.EqualTo(100f).Within(0.001f));
            Assert.That(filter.LastProposed, Is.EqualTo(70f).Within(0.001f));
            Assert.That(filter.LastContext.Cause, Is.EqualTo("shot"));
            Assert.That(filter.LastContext.Instigator, Is.SameAs(_instigator));
        }

        [Test]
        public void AFilterCanReduceTheChange()
        {
            _attributes.AddBaseValueFilter(_health, new HalvingFilter());

            _attributes.AddToBaseValue(_health, -40f);

            Assert.That(
                _attributes.GetBaseValue(_health),
                Is.EqualTo(80f).Within(0.001f),
                "필터가 돌려준 값이 실제로 적용되어야 한다. 보호막이 절반을 대신 받는 것이 이 형태이다.");
        }

        [Test]
        public void AFilterThatReturnsTheCurrentValueSwallowsTheChangeWithoutNotifying()
        {
            _attributes.AddBaseValueFilter(_health, new SwallowingFilter());
            var announced = 0;
            using var subscription = _attributes.Changed.Subscribe(_ => announced++);

            _attributes.AddToBaseValue(_health, -40f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f).Within(0.001f));
            Assert.That(announced, Is.Zero, "없던 것이 된 변화를 알리면 받은 쪽이 일어나지 않은 피해에 반응한다.");
        }

        [Test]
        public void FiltersRunInRegistrationOrderAndChainTheirResults()
        {
            var order = new List<string>();
            _attributes.AddBaseValueFilter(_health, new NamedFilter("first", order));
            _attributes.AddBaseValueFilter(_health, new HalvingFilter());
            _attributes.AddBaseValueFilter(_health, new NamedFilter("last", order));

            _attributes.AddToBaseValue(_health, -40f);

            Assert.That(order, Is.EqualTo(new[] { "first", "last" }));
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void TheFilterResultIsClampedToTheBounds()
        {
            _attributes.AddBaseValueFilter(_health, new OverflowingFilter());

            _attributes.AddToBaseValue(_health, -10f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(0f).Within(0.001f), "필터는 범위를 지킬 책임이 없다.");
        }

        [Test]
        public void FiltersDoNotSeeModifierChanges()
        {
            var filter = new RecordingFilter();
            _attributes.AddBaseValueFilter(_health, filter);

            _attributes.AddModifier(_health, AttributeModifierOperation.Add, -30f);

            Assert.That(filter.CallCount, Is.Zero, "수정자는 원인이 사라지면 되돌아가는 변화라 필터가 볼 것이 아니다.");
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(70f).Within(0.001f));
        }

        [Test]
        public void TheChangeNotificationCarriesTheCause()
        {
            var received = new List<AttributeChangedEvent>();
            using var subscription = _attributes.Changed.Subscribe(received.Add);
            var context = new AttributeChangeContext("shot", _instigator);

            _attributes.AddToBaseValue(_health, -30f, context);

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].Context.Cause, Is.EqualTo("shot"));
            Assert.That(received[0].Context.Instigator, Is.SameAs(_instigator));
            Assert.That(received[0].Delta, Is.EqualTo(-30f).Within(0.001f));
        }

        [Test]
        public void AChangeWithoutACauseIsAnnouncedWithAnEmptyContext()
        {
            var received = new List<AttributeChangedEvent>();
            using var subscription = _attributes.Changed.Subscribe(received.Add);

            _attributes.SetBaseValue(_health, 50f);

            Assert.That(received[0].Context.HasCause, Is.False);
        }

        [Test]
        public void DisposingTheHandleRemovesOnlyThatFilter()
        {
            var handle = _attributes.AddBaseValueFilter(_health, new SwallowingFilter());
            _attributes.AddBaseValueFilter(_health, new HalvingFilter());
            Assert.That(_attributes.GetBaseValueFilterCount(_health), Is.EqualTo(2));

            handle.Dispose();
            handle.Dispose();

            Assert.That(_attributes.GetBaseValueFilterCount(_health), Is.EqualTo(1));
            _attributes.AddToBaseValue(_health, -40f);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void AFilterMayRemoveItselfWhileRunning()
        {
            SelfRemovingFilter filter = null;
            filter = new SelfRemovingFilter(() => _attributes.RemoveBaseValueFilter(_health, filter));
            _attributes.AddBaseValueFilter(_health, filter);

            Assert.DoesNotThrow(() => _attributes.AddToBaseValue(_health, -10f));

            Assert.That(_attributes.GetBaseValueFilterCount(_health), Is.Zero);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void RestoringASnapshotIdentifiesItselfAsTheCause()
        {
            var filter = new RecordingFilter();
            _attributes.AddBaseValueFilter(_health, filter);
            var snapshot = _attributes.CaptureSnapshot();
            _attributes.SetBaseValue(_health, 40f);

            _attributes.RestoreSnapshot(snapshot);

            Assert.That(filter.LastContext.Cause, Is.SameAs(snapshot), "복원은 피해가 아니므로 필터가 스냅숏임을 알아볼 수 있어야 한다.");
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f).Within(0.001f));
        }

        /// <summary>정리 목록에 등록된 어트리뷰트 정의를 만든다.</summary>
        private AttributeDefinition CreateDefinition(string id, float defaultBaseValue)
        {
            var definition = AttributeDefinition.CreateRuntime(id, defaultBaseValue);
            _createdDefinitions.Add(definition);
            return definition;
        }

        /// <summary>받은 것을 기록만 하고 값은 그대로 통과시키는 필터이다.</summary>
        private sealed class RecordingFilter : IAttributeBaseValueFilter
        {
            public int CallCount { get; private set; }

            public float LastCurrent { get; private set; }

            public float LastProposed { get; private set; }

            public AttributeChangeContext LastContext { get; private set; }

            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                CallCount++;
                LastCurrent = currentBaseValue;
                LastProposed = proposedBaseValue;
                LastContext = context;
                return proposedBaseValue;
            }
        }

        /// <summary>변화량을 절반으로 줄이는 필터이다.</summary>
        private sealed class HalvingFilter : IAttributeBaseValueFilter
        {
            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                return currentBaseValue + (proposedBaseValue - currentBaseValue) * 0.5f;
            }
        }

        /// <summary>모든 변화를 없던 것으로 만드는 필터이다.</summary>
        private sealed class SwallowingFilter : IAttributeBaseValueFilter
        {
            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                return currentBaseValue;
            }
        }

        /// <summary>범위를 벗어난 값을 돌려주는 필터이다.</summary>
        private sealed class OverflowingFilter : IAttributeBaseValueFilter
        {
            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                return -1000f;
            }
        }

        /// <summary>불린 차례를 기록하는 필터이다.</summary>
        private sealed class NamedFilter : IAttributeBaseValueFilter
        {
            private readonly string _name;
            private readonly List<string> _order;

            public NamedFilter(string name, List<string> order)
            {
                _name = name;
                _order = order;
            }

            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                _order.Add(_name);
                return proposedBaseValue;
            }
        }

        /// <summary>불리는 동안 스스로를 빼는 필터이다.</summary>
        private sealed class SelfRemovingFilter : IAttributeBaseValueFilter
        {
            private readonly System.Action _remove;

            public SelfRemovingFilter(System.Action remove)
            {
                _remove = remove;
            }

            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                _remove();
                return proposedBaseValue;
            }
        }
    }
}
