using System;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;

namespace SnowMaker.Tests
{
    [TestFixture]
    public class UniqueIdGeneratorTest
    {
        [Test]
        public async Task ConstructorShouldNotRetrieveDataFromStore()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            // ReSharper disable once ObjectCreationAsStatement
            new UniqueIdGenerator(store);
            await store.DidNotReceiveWithAnyArgs().GetDataAsync(null);
        }

        [Test]
        public void MaxWriteAttemptsShouldThrowArgumentOutOfRangeExceptionWhenValueIsZero()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var store = Substitute.For<IOptimisticDataStore>();
                // ReSharper disable once ObjectCreationAsStatement
                new UniqueIdGenerator(store)
                {
                    MaxWriteAttempts = 0
                };
            });

        }

        [Test]
        public void MaxWriteAttemptsShouldThrowArgumentOutOfRangeExceptionWhenValueIsNegative()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var store = Substitute.For<IOptimisticDataStore>();
                // ReSharper disable once ObjectCreationAsStatement
                new UniqueIdGenerator(store)
                {
                    MaxWriteAttempts = -1
                };
            });
        }

        [Test]
#pragma warning disable 1998
        public async Task NextIdShouldThrowExceptionOnCorruptData()
#pragma warning restore 1998
        {
            var ex = Assert.ThrowsAsync<UniqueIdGenerationException>(async  ()  =>
            {
                var store = Substitute.For<IOptimisticDataStore>();
                store.GetDataAsync("test").Returns(Task.FromResult("abc"));

                var generator = new UniqueIdGenerator(store);

                await generator.NextIdAsync("test");
            });
        }

        [Test]
#pragma warning disable 1998
        public async Task NextIdShouldThrowExceptionOnNullData()
#pragma warning restore 1998
        {
            var ex = Assert.ThrowsAsync<UniqueIdGenerationException>(async () =>
            {
                var store = Substitute.For<IOptimisticDataStore>();
                store.GetDataAsync("test").Returns(Task.FromResult((string)null));

                var generator = new UniqueIdGenerator(store);

                await generator.NextIdAsync("test");
            });
        }

        [Test]
        public async Task NextIdShouldReturnNumbersSequentially()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetDataAsync("test").Returns(Task.FromResult("0"), Task.FromResult("250"));
            store.TryOptimisticWriteAsync("test", "3").Returns(Task.FromResult(true));

            var subject = new UniqueIdGenerator(store)
            {
                BatchSize = 3
            };

            Assert.AreEqual(0, await subject.NextIdAsync("test"));
            Assert.AreEqual(1, await subject.NextIdAsync("test"));
            Assert.AreEqual(2, await subject.NextIdAsync("test"));
        }

        [Test]
        public async Task NextIdShouldRollOverToNewBlockWhenCurrentBlockIsExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetDataAsync("test").Returns(Task.FromResult("0"), Task.FromResult("250"));
            store.TryOptimisticWriteAsync("test", "3").Returns(Task.FromResult(true));
            store.TryOptimisticWriteAsync("test", "253").Returns(Task.FromResult(true));

            var subject = new UniqueIdGenerator(store)
            {
                BatchSize = 3
            };

            Assert.AreEqual(0, (await subject.NextIdAsync("test")));
            Assert.AreEqual(1, (await subject.NextIdAsync("test")));
            Assert.AreEqual(2, (await subject.NextIdAsync("test")));
            Assert.AreEqual(250, (await subject.NextIdAsync("test")));
            Assert.AreEqual(251, (await subject.NextIdAsync("test")));
            Assert.AreEqual(252, (await subject.NextIdAsync("test")));
        }

        [Test]
        public async Task NextIdShouldThrowExceptionWhenRetriesAreExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetDataAsync("test").Returns(Task.FromResult("0"));
            store.TryOptimisticWriteAsync("test", "3").Returns(Task.FromResult(false), Task.FromResult(false), Task.FromResult(false), Task.FromResult(true));

            var generator = new UniqueIdGenerator(store)
            {
                MaxWriteAttempts = 3
            };

            try
            {
                await generator.NextIdAsync("test");
            }
            catch (Exception ex)
            {
                StringAssert.StartsWith("Failed to update the data store after 3 attempts.", ex.Message);
                return;
            }
            Assert.Fail("NextId should have thrown and been caught in the try block");
        }
    }
}
