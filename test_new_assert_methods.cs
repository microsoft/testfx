using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestNewAssertMethods
{
    [TestClass]
    public class NewAssertMethodsTests
    {
        [TestMethod]
        public void AreEqual_Collections_ShouldPass()
        {
            var expected = new[] { 1, 2, 3 };
            var actual = new[] { 1, 2, 3 };
            
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AreEqual_Collections_WithIEnumerable()
        {
            var numbers = new[] { 1, 2, 3, 4, 5, 6 };
            var expected = numbers.Where(x => x % 2 == 0); // 2, 4, 6
            var actual = new[] { 2, 4, 6 };
            
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AreEquivalent_Collections_ShouldPass()
        {
            var expected = new[] { 1, 2, 3 };
            var actual = new[] { 3, 1, 2 }; // Different order
            
            Assert.AreEquivalent(expected, actual);
        }

        [TestMethod]
        public void AreEquivalent_Collections_WithIEnumerable()
        {
            var numbers = new[] { 1, 2, 3, 4, 5, 6 };
            var expected = numbers.Where(x => x % 2 == 0); // 2, 4, 6
            var actual = new[] { 6, 2, 4 }; // Different order
            
            Assert.AreEquivalent(expected, actual);
        }

        [TestMethod]
        public void IsSubsetOf_ShouldPass()
        {
            var subset = new[] { 1, 2 };
            var superset = new[] { 1, 2, 3, 4, 5 };
            
            Assert.IsSubsetOf(subset, superset);
        }

        [TestMethod]
        public void IsSubsetOf_WithIEnumerable()
        {
            var numbers = new[] { 1, 2, 3, 4, 5, 6 };
            var subset = numbers.Where(x => x % 2 == 0); // 2, 4, 6
            var superset = numbers; // 1, 2, 3, 4, 5, 6
            
            Assert.IsSubsetOf(subset, superset);
        }

        [TestMethod]
        public void AllItemsAreNotNull_ShouldPass()
        {
            var collection = new[] { "a", "b", "c" };
            
            Assert.AllItemsAreNotNull(collection);
        }

        [TestMethod]
        public void AllItemsAreNotNull_WithIEnumerable()
        {
            var strings = new[] { "a", "b", "c", "d" };
            var filtered = strings.Where(x => x != "d"); // "a", "b", "c"
            
            Assert.AllItemsAreNotNull(filtered);
        }

        [TestMethod]
        public void AllItemsAreUnique_ShouldPass()
        {
            var collection = new[] { 1, 2, 3, 4 };
            
            Assert.AllItemsAreUnique(collection);
        }

        [TestMethod]
        public void AllItemsAreUnique_WithIEnumerable()
        {
            var numbers = new[] { 1, 2, 3, 4, 5, 6 };
            var evens = numbers.Where(x => x % 2 == 0); // 2, 4, 6 (all unique)
            
            Assert.AllItemsAreUnique(evens);
        }

        [TestMethod]
        public void AllItemsAreInstancesOfType_ShouldPass()
        {
            var collection = new object[] { "a", "b", "c" };
            
            Assert.AllItemsAreInstancesOfType(collection, typeof(string));
        }

        [TestMethod]
        public void AllItemsAreInstancesOfType_WithIEnumerable()
        {
            var items = new object[] { "a", "b", "c", 123 };
            var strings = items.Where(x => x is string); // "a", "b", "c"
            
            Assert.AllItemsAreInstancesOfType(strings, typeof(string));
        }
    }
}