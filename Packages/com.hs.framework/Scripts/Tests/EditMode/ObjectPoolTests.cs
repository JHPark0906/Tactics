using System;
using System.Collections.Generic;
using HS.Framework.Foundation.Collections;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>범용 오브젝트 풀의 대여·반환 규칙과 선택적 훅을 검증한다.</summary>
    public sealed class ObjectPoolTests
    {
        [Test]
        public void ConstructorRejectsNullFactory()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ObjectPool<object>(null));
        }

        [Test]
        public void PrewarmCreatesRequestedNumberOfFreeItems()
        {
            var pool = CreatePool(out var creation);

            pool.Prewarm(3);

            Assert.That(creation.Count, Is.EqualTo(3));
            Assert.That(pool.CreatedCount, Is.EqualTo(3));
            Assert.That(pool.FreeCount, Is.EqualTo(3));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void AcquireUsesPrewarmedItemBeforeCreatingNew()
        {
            var pool = CreatePool(out var creation);
            pool.Prewarm(1);

            pool.Acquire();

            Assert.That(creation.Count, Is.EqualTo(1));
            Assert.That(pool.FreeCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void AcquireAutoExpandsWhenPoolIsEmpty()
        {
            var pool = CreatePool(out var creation);

            var first = pool.Acquire();
            var second = pool.Acquire();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(creation.Count, Is.EqualTo(2));
            Assert.That(pool.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void ReleasedItemIsReusedOnNextAcquire()
        {
            var pool = CreatePool(out var creation);
            var item = pool.Acquire();

            var released = pool.Release(item);
            var reacquired = pool.Acquire();

            Assert.That(released, Is.True);
            Assert.That(reacquired, Is.SameAs(item));
            Assert.That(creation.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateReleaseIsRejected()
        {
            var pool = CreatePool(out _);
            var item = pool.Acquire();
            pool.Release(item);

            Assert.That(pool.Release(item), Is.False);
            Assert.That(pool.FreeCount, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseRejectsNullAndForeignItems()
        {
            var pool = CreatePool(out _);
            pool.Acquire();

            Assert.That(pool.Release(null), Is.False);
            Assert.That(pool.Release(new object()), Is.False);
            Assert.That(pool.FreeCount, Is.EqualTo(0));
        }

        [Test]
        public void HooksAreOptionalAndDefaultToNoOp()
        {
            var pool = new ObjectPool<object>(() => new object());

            Assert.DoesNotThrow(() =>
            {
                var item = pool.Acquire();
                pool.Release(item);
            });
        }

        [Test]
        public void AcquireCallsOnAcquireBeforeHandingOutTheItem()
        {
            var acquired = new List<object>();
            var pool = new ObjectPool<object>(() => new object(), onAcquire: acquired.Add);

            var item = pool.Acquire();

            Assert.That(acquired, Is.EqualTo(new[] { item }));
        }

        [Test]
        public void ReleaseCallsOnReleaseOnlyWhenTheReleaseSucceeds()
        {
            var released = new List<object>();
            var pool = new ObjectPool<object>(() => new object(), onRelease: released.Add);
            var item = pool.Acquire();

            pool.Release(new object());
            Assert.That(released, Is.Empty, "이 풀이 만들지 않은 항목은 반환이 거부되므로 훅도 불리면 안 된다.");

            pool.Release(item);
            Assert.That(released, Is.EqualTo(new[] { item }));

            released.Clear();
            pool.Release(item);
            Assert.That(released, Is.Empty, "중복 반환은 거부되므로 훅이 다시 불리면 안 된다.");
        }

        private static ObjectPool<object> CreatePool(out CreationCounter creation)
        {
            var counter = new CreationCounter();
            creation = counter;
            return new ObjectPool<object>(counter.Create);
        }

        private sealed class CreationCounter
        {
            public int Count { get; private set; }

            public object Create()
            {
                Count++;
                return new object();
            }
        }
    }
}
