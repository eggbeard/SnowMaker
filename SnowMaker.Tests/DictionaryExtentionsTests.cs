using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Internal;
using NUnit.Framework;

namespace SnowMaker.Tests
{
    [TestFixture]
    public class DictionaryExtentionsTests
    {
        [Test]
        public async Task GetValueShouldReturnExistingValueWithoutUsingTheLock()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };

            // Act
            // null can't be used as a lock and will throw an exception if attempted
            var value = await dictionary.GetValueAsync("foo", null, null);

            // Assert
            Assert.AreEqual("bar", value);
        }

        [Test]
        public async Task GetValueShouldCallTheValueInitializerWithinTheLockIfTheKeyDoesntExist()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };

            var dictionarySemaphoreSlim = new SemaphoreSlim(1,1);


            // Act
            var ignoreValue = await dictionary.GetValueAsync(
                "bar",
                dictionarySemaphoreSlim,
#pragma warning disable 1998
                async () =>
#pragma warning restore 1998
                {
                    // Assert
                    Assert.IsTrue(dictionarySemaphoreSlim.CurrentCount == 0);
                    return "qak";
                });
        }

        [Test]
        public async Task GetValueShouldStoreNewValuesAfterCallingTheValueInitializerOnce()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };

            var dictionarySemaphoreSlim = new SemaphoreSlim(1, 1);

            // Arrange
#pragma warning disable 1998
            var value = await dictionary.GetValueAsync("bar", dictionarySemaphoreSlim, async () => { return "qak"; });
#pragma warning restore 1998

            // Act
            if (!value.IsNullOrEmpty())
            {
                var value2 = await dictionary.GetValueAsync(
                    "bar",
                    dictionarySemaphoreSlim,
#pragma warning disable 1998
                    async () =>
#pragma warning restore 1998
                    {
                        // Assert
                        Assert.Fail("Value initializer should not have been called a second time.");
                        return null;
                    });
            }
        }


    }
}