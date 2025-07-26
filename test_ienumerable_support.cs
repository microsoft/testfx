// Test file to verify IEnumerable support in CollectionAssert methods
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IEnumerableTest
{
    public class TestIEnumerableSupport
    {
        public static void TestContainsWithIEnumerable()
        {
            // Test with LINQ result (IEnumerable<int>)
            IEnumerable<int> numbers = Enumerable.Range(1, 5);
            CollectionAssert.Contains(numbers, 3);
            
            // Test with array
            string[] strings = { "a", "b", "c" };
            CollectionAssert.Contains(strings, "b");
            
            Console.WriteLine("Contains tests passed!");
        }
        
        public static void TestDoesNotContainWithIEnumerable()
        {
            // Test with LINQ result
            IEnumerable<int> evenNumbers = Enumerable.Range(1, 10).Where(x => x % 2 == 0);
            CollectionAssert.DoesNotContain(evenNumbers, 3);
            
            Console.WriteLine("DoesNotContain tests passed!");
        }
        
        public static void TestAllItemsAreNotNullWithIEnumerable()
        {
            // Test with LINQ result
            IEnumerable<string> strings = new[] { "a", "b", "c" }.Where(x => x != null);
            CollectionAssert.AllItemsAreNotNull(strings);
            
            Console.WriteLine("AllItemsAreNotNull tests passed!");
        }
        
        public static void TestAllItemsAreUniqueWithIEnumerable()
        {
            // Test with LINQ result
            IEnumerable<int> uniqueNumbers = new[] { 1, 2, 3, 4, 5 }.Where(x => x < 10);
            CollectionAssert.AllItemsAreUnique(uniqueNumbers);
            
            Console.WriteLine("AllItemsAreUnique tests passed!");
        }
        
        public static void TestIsSubsetOfWithIEnumerable()
        {
            // Test with LINQ results
            IEnumerable<int> subset = new[] { 1, 2, 3 }.Where(x => x < 4);
            IEnumerable<int> superset = Enumerable.Range(1, 10);
            CollectionAssert.IsSubsetOf(subset, superset);
            
            Console.WriteLine("IsSubsetOf tests passed!");
        }
        
        public static void TestAllItemsAreInstancesOfTypeWithIEnumerable()
        {
            // Test with LINQ result
            IEnumerable<string> strings = new[] { "a", "b", "c" }.Where(x => x.Length > 0);
            CollectionAssert.AllItemsAreInstancesOfType(strings, typeof(string));
            
            Console.WriteLine("AllItemsAreInstancesOfType tests passed!");
        }
        
        public static void TestAreEqualWithIEnumerable()
        {
            // Test with LINQ results
            IEnumerable<int> expected = new[] { 1, 2, 3 }.Where(x => x > 0);
            IEnumerable<int> actual = Enumerable.Range(1, 3);
            CollectionAssert.AreEqual(expected, actual);
            
            Console.WriteLine("AreEqual tests passed!");
        }
        
        public static void TestAreNotEqualWithIEnumerable()
        {
            // Test with LINQ results
            IEnumerable<int> notExpected = new[] { 1, 2, 3 }.Where(x => x > 0);
            IEnumerable<int> actual = Enumerable.Range(2, 3); // [2, 3, 4]
            CollectionAssert.AreNotEqual(notExpected, actual);
            
            Console.WriteLine("AreNotEqual tests passed!");
        }
        
        public static void TestAreEquivalentWithIEnumerable()
        {
            // Test with LINQ results (order doesn't matter for AreEquivalent)
            IEnumerable<int> expected = new[] { 3, 1, 2 }.Where(x => x > 0);
            IEnumerable<int> actual = Enumerable.Range(1, 3); // [1, 2, 3]
            CollectionAssert.AreEquivalent(expected, actual);
            
            Console.WriteLine("AreEquivalent tests passed!");
        }
        
        public static void TestAreNotEquivalentWithIEnumerable()
        {
            // Test with LINQ results
            IEnumerable<int> notExpected = new[] { 1, 2, 3 }.Where(x => x > 0);
            IEnumerable<int> actual = new[] { 1, 2, 3, 4 }.Where(x => x > 0); // different element count
            CollectionAssert.AreNotEquivalent(notExpected, actual);
            
            Console.WriteLine("AreNotEquivalent tests passed!");
        }
        
        public static void Main()
        {
            TestContainsWithIEnumerable();
            TestDoesNotContainWithIEnumerable();
            TestAllItemsAreNotNullWithIEnumerable();
            TestAllItemsAreUniqueWithIEnumerable();
            TestIsSubsetOfWithIEnumerable();
            TestAllItemsAreInstancesOfTypeWithIEnumerable();
            TestAreEqualWithIEnumerable();
            TestAreNotEqualWithIEnumerable();
            TestAreEquivalentWithIEnumerable();
            TestAreNotEquivalentWithIEnumerable();
            
            Console.WriteLine("All IEnumerable tests passed!");
        }
    }
}