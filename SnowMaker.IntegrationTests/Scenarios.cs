using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SnowMaker.IntegrationTests
{
    public abstract class Scenarios<TTestScope> where TTestScope : ITestScope
    {
        protected abstract Task<IOptimisticDataStore> BuildStoreAsync(TTestScope scope);
        protected abstract TTestScope BuildTestScope();

        [Test]
        public async Task ShouldReturnOneForFirstIdInNewScope()
        {
            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store = await BuildStoreAsync(testScope);
                var generator = new UniqueIdGenerator(store) { BatchSize = 3 };

                // Act
                var generatedId = await generator.NextIdAsync(testScope.IdScopeName);

                // Assert
                Assert.AreEqual(1, generatedId);
            }
        }

        [Test]
        public async Task ShouldInitializeBlobForFirstIdInNewScope()
        {
            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store = await BuildStoreAsync(testScope);
                var generator = new UniqueIdGenerator(store) { BatchSize = 3 };

                // Act
                await generator.NextIdAsync(testScope.IdScopeName); //1

                // Assert
                Assert.AreEqual("4", await testScope.ReadCurrentPersistedValueAsync());
            }
        }

        [Test]
        public async Task ShouldNotUpdateBlobAtEndOfBatch()
        {
            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store = await BuildStoreAsync(testScope);
                var generator = new UniqueIdGenerator(store) { BatchSize = 3 };

                // Act
                await generator.NextIdAsync(testScope.IdScopeName); //1
                await generator.NextIdAsync(testScope.IdScopeName); //2
                await generator.NextIdAsync(testScope.IdScopeName); //3

                // Assert
                Assert.AreEqual("4", await testScope.ReadCurrentPersistedValueAsync());
            }
        }

        [Test]
        public async Task ShouldUpdateBlobWhenGeneratingNextIdAfterEndOfBatch()
        {
            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store = await BuildStoreAsync(testScope);
                var generator = new UniqueIdGenerator(store) { BatchSize = 3 };

                // Act
                await generator.NextIdAsync(testScope.IdScopeName); //1
                await generator.NextIdAsync(testScope.IdScopeName); //2
                await generator.NextIdAsync(testScope.IdScopeName); //3
                await generator.NextIdAsync(testScope.IdScopeName); //4

                // Assert
                Assert.AreEqual("7", await testScope.ReadCurrentPersistedValueAsync());
            }
        }

        [Test]
        public async Task ShouldReturnIdsFromThirdBatchIfSecondBatchTakenByAnotherGenerator()
        {
            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store1 = await BuildStoreAsync(testScope);
                var generator1 = new UniqueIdGenerator(store1) { BatchSize = 3 };
                var store2 = await BuildStoreAsync(testScope);
                var generator2 = new UniqueIdGenerator(store2) { BatchSize = 3 };

                // Act
                await generator1.NextIdAsync(testScope.IdScopeName); //1
                await generator1.NextIdAsync(testScope.IdScopeName); //2
                await generator1.NextIdAsync(testScope.IdScopeName); //3
                await generator2.NextIdAsync(testScope.IdScopeName); //4
                var lastId = await generator1.NextIdAsync(testScope.IdScopeName); //7

                // Assert
                Assert.AreEqual(7, lastId);
            }
        }

        [Test]
        public async Task ShouldReturnIdsAcrossMultipleGenerators()
        {
            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store1 = await BuildStoreAsync(testScope);
                var generator1 = new UniqueIdGenerator(store1) { BatchSize = 3 };
                var store2 = await BuildStoreAsync(testScope);
                var generator2 = new UniqueIdGenerator(store2) { BatchSize = 3 };

                // Act
                var generatedIds = new[]
                {
                    await generator1.NextIdAsync(testScope.IdScopeName), //1
                    await generator1.NextIdAsync(testScope.IdScopeName), //2
                    await generator1.NextIdAsync(testScope.IdScopeName), //3
                    await generator2.NextIdAsync(testScope.IdScopeName), //4
                    await generator1.NextIdAsync(testScope.IdScopeName), //7
                    await generator2.NextIdAsync(testScope.IdScopeName), //5
                    await generator2.NextIdAsync(testScope.IdScopeName), //6
                    await generator2.NextIdAsync(testScope.IdScopeName), //10
                    await generator1.NextIdAsync(testScope.IdScopeName), //8
                    await generator1.NextIdAsync(testScope.IdScopeName)  //9
                };

                // Assert
                CollectionAssert.AreEqual(
                    new[] { 1, 2, 3, 4, 7, 5, 6, 10, 8, 9 },
                    generatedIds);
            }
        }

        [Test, Parallelizable(ParallelScope.All)]
        public async Task ShouldSupportUsingOneGeneratorFromMultipleThreads()
        {

            // Arrange
            using (var testScope = BuildTestScope())
            {
                var store = await BuildStoreAsync(testScope);
                var generator = new UniqueIdGenerator(store) { BatchSize = 1000 };
                const int testLength = 10000;

                // Act
                var generatedIds = new ConcurrentQueue<long>();
                var threadIds = new ConcurrentQueue<int>();
                var scopeName = testScope.IdScopeName;

                var listToExecute = new List<int>();
                for (int i = 0; i < testLength; i++)
                {
                    listToExecute.Add(i);
                }

                var tasks = listToExecute.ForEachAsync(10,async item =>
                {
                    var idToAdd = await generator.NextIdAsync(scopeName);
                    generatedIds.Enqueue(idToAdd);
                    threadIds.Enqueue(Thread.CurrentThread.ManagedThreadId);
                });
                await Task.WhenAll(tasks);

                // Assert we generated the right count of ids
                Assert.AreEqual(testLength, generatedIds.Count);

                // Assert there were no duplicates
                Assert.IsFalse(generatedIds.GroupBy(n => n).Any(g => g.Count() != 1));

                // Assert we used multiple threads
                var uniqueThreadsUsed = threadIds.Distinct().Count();
                if (uniqueThreadsUsed == 1)
                    Assert.Inconclusive("The test failed to actually utilize multiple threads. {0} uniqueThreadsUsed", uniqueThreadsUsed);
            }
        }
    }
}
