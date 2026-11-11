// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerHashingTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void Combine_AllArities_MatchSeededXxHash32(int arity)
    {
        Type type = ProductReflection.Type(false, "Microsoft.Testing.Platform.RoslynHashCode");
        uint seed = (uint)type.GetField("s_seed", ProductReflection.AllMembers)!.GetValue(null)!;
        MethodInfo method = type.GetMethods().Single(candidate => candidate.Name == "Combine" && candidate.GetGenericArguments().Length == arity)
            .MakeGenericMethod(Enumerable.Repeat(typeof(int?), arity).ToArray());
        for (int omitted = -1; omitted <= arity; omitted++)
        {
            int?[] values = Enumerable.Range(0, arity)
                .Select(index => omitted == arity || index == omitted ? (int?)null : (index + 1) * -17)
                .ToArray();
            int expected = XxHash32(values.Select(value => value ?? 0).ToArray(), seed);
            Assert.AreEqual(expected, ProductReflection.Invoke(method, null, values.Cast<object?>().ToArray()),
                $"Arity {arity}, omitted position {omitted}");
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(12)]
    public void IncrementalHash_QueueAndBlockBoundaries_MatchSeededXxHash32(int count)
    {
        Type type = ProductReflection.Type(false, "Microsoft.Testing.Platform.RoslynHashCode");
        uint seed = (uint)type.GetField("s_seed", ProductReflection.AllMembers)!.GetValue(null)!;
        object hash = Activator.CreateInstance(type)!;
        MethodInfo add = type.GetMethods().Single(method => method.Name == "Add" && method.GetParameters().Length == 1).MakeGenericMethod(typeof(int));
        int[] values = Enumerable.Range(0, count).Select(index => (index + 1) * 29).ToArray();
        foreach (int value in values)
        {
            ProductReflection.Invoke(add, hash, value);
        }

        Assert.AreEqual(XxHash32(values, seed), ProductReflection.Call(type, hash, "ToHashCode", []));
    }

    [TestMethod]
    public void IncrementalHash_NullAndExplicitComparers_UseTheRequestedHashValues()
    {
        Type type = ProductReflection.Type(false, "Microsoft.Testing.Platform.RoslynHashCode");
        uint seed = (uint)type.GetField("s_seed", ProductReflection.AllMembers)!.GetValue(null)!;
        object hash = Activator.CreateInstance(type)!;
        MethodInfo add = type.GetMethods().Single(method => method.Name == "Add" && method.GetParameters().Length == 2).MakeGenericMethod(typeof(string));
        ProductReflection.Invoke(add, hash, null, new FixedHashComparer());
        ProductReflection.Invoke(add, hash, "with comparer", new FixedHashComparer());
        ProductReflection.Invoke(add, hash, "without comparer", null);
        MethodInfo addNullable = type.GetMethods().Single(method => method.Name == "Add" && method.GetParameters().Length == 1).MakeGenericMethod(typeof(int?));
        ProductReflection.Invoke(addNullable, hash, [null]);
        ProductReflection.Invoke(addNullable, hash, 43);
        Assert.AreEqual(XxHash32([0, 173, "without comparer".GetHashCode(), 0, 43], seed), ProductReflection.Call(type, hash, "ToHashCode", []));
    }

    [TestMethod]
    public void MutableHash_ObjectEqualityAndHashCode_RejectAccidentalObjectUse()
    {
        Type type = ProductReflection.Type(false, "Microsoft.Testing.Platform.RoslynHashCode");
        object hash = Activator.CreateInstance(type)!;
        Assert.AreEqual(
            "HashCode is a mutable struct and should not be compared with other HashCodes. Use ToHashCode to retrieve the computed hash code.",
            Assert.ThrowsExactly<NotSupportedException>(() => hash.GetHashCode()).Message);
        Assert.AreEqual(
            "HashCode is a mutable struct and should not be compared with other HashCodes.",
            Assert.ThrowsExactly<NotSupportedException>(() => hash.Equals(null)).Message);
    }

    [TestMethod]
    public void ObjectPool_CapacityAndCancellation_NeverReturnOneLeaseTwice()
    {
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.PooledObjects.ObjectPool`1").MakeGenericType(typeof(PooledValue));
        Type factoryType = type.GetNestedType("Factory", ProductReflection.AllMembers)!.MakeGenericType(typeof(PooledValue));
        int created = 0;
        Func<PooledValue> factory = () => new PooledValue(++created);
        var factoryDelegate = Delegate.CreateDelegate(factoryType, factory.Target, factory.Method);
        object pool = ProductReflection.Create(type, [factoryType, typeof(int)], factoryDelegate, 2);
        object Allocate() => ProductReflection.Call(type, pool, "Allocate", [])!;
        void Free(object value, CancellationToken token = default)
            => ProductReflection.Call(type, pool, "Free", [typeof(PooledValue), typeof(CancellationToken)], value, token);
        object first = Allocate();
        object second = Allocate();
        object overflow = Allocate();
        Assert.AreEqual(3, created);
        Free(first);
        Free(second);
        Free(overflow);
        Assert.AreSame(first, Allocate());
        Assert.AreSame(second, Allocate());
        Assert.AreEqual(4, ((PooledValue)Allocate()).Id);
        Free(overflow, new CancellationToken(canceled: true));
        Assert.AreEqual(5, ((PooledValue)Allocate()).Id);
        object defaultSized = ProductReflection.Create(type, [factoryType], factoryDelegate);
        object defaultLease = ProductReflection.Call(type, defaultSized, "Allocate", [])!;
        Assert.AreEqual(6, ((PooledValue)defaultLease).Id);
        ProductReflection.Call(type, defaultSized, "Free", [typeof(PooledValue), typeof(CancellationToken)], defaultLease, CancellationToken.None);
        Assert.AreSame(defaultLease, ProductReflection.Call(type, defaultSized, "Allocate", []));
    }

    [TestMethod]
    public void PooledHashSet_CancellationRetainsContents_ThenNormalFreeClearsTheLease()
    {
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.PooledObjects.PooledHashSet`1").MakeGenericType(typeof(PooledValue));
        object lease = ProductReflection.Call(type, null, "GetInstance", [typeof(IEqualityComparer<PooledValue>)], EqualityComparer<PooledValue>.Default)!;
        var set = (ISet<PooledValue>)lease;
        bool returned = false;
        try
        {
            set.Add(new PooledValue(17));
            ProductReflection.Call(type, lease, "Free", [typeof(CancellationToken)], new CancellationToken(canceled: true));
            Assert.AreSequenceEqual(new[] { new PooledValue(17) }, set.ToArray());
            ProductReflection.Call(type, lease, "Free", [typeof(CancellationToken)], CancellationToken.None);
            returned = true;
            Assert.IsEmpty(set);
            object next = ProductReflection.Call(type, null, "GetInstance", [typeof(IEqualityComparer<PooledValue>)], EqualityComparer<PooledValue>.Default)!;
            returned = false;
            object previous = lease;
            lease = next;
            Assert.AreSame(previous, next);
            Assert.IsEmpty((ISet<PooledValue>)next);
        }
        finally
        {
            if (!returned)
            {
                ((IDisposable)lease).Dispose();
            }
        }
    }

    [TestMethod]
    public void PooledHashSet_WithoutAnOwningPool_FreeStillClearsItsContents()
    {
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.PooledObjects.PooledHashSet`1").MakeGenericType(typeof(PooledValue));
        Type pool = ProductReflection.Type(false, "Analyzer.Utilities.PooledObjects.ObjectPool`1").MakeGenericType(type);
        using var lease = (IDisposable)ProductReflection.Create(type, [pool, typeof(IEqualityComparer<PooledValue>)], null, null);
        var values = (ISet<PooledValue>)lease;
        values.Add(new PooledValue(17));
        values.Add(new PooledValue(29));
        ProductReflection.Call(type, lease, "Free", [typeof(CancellationToken)], CancellationToken.None);
        Assert.IsEmpty(values);
        values.Add(new PooledValue(43));
        Assert.AreSequenceEqual(new[] { new PooledValue(43) }, values.ToArray());
    }

    [TestMethod]
    [DataRow(1UL, 0, 1UL)]
    [DataRow(1UL, 63, 0x8000000000000000UL)]
    [DataRow(0x8000000000000000UL, 1, 1UL)]
    [DataRow(0x8000000000000001UL, 1, 3UL)]
    public void HashRotation_Unsigned64BitBoundaries_RotateWithoutTruncation(ulong value, int offset, ulong expected)
    {
        Type type = ProductReflection.Type(false, "Microsoft.Testing.Platform.RoslynHashCode+BitOperations");
        Assert.AreEqual(expected, ProductReflection.Call(type, null, "RotateLeft", [typeof(ulong), typeof(int)], value, offset));
    }

    private static int XxHash32(int[] values, uint seed)
    {
        // Independent word-oriented xxHash32 reference. The product's per-process seed is read, never changed.
        const uint prime1 = 2654435761;
        const uint prime2 = 2246822519;
        const uint prime3 = 3266489917;
        const uint prime4 = 668265263;
        const uint prime5 = 374761393;
        static uint Rotate(uint value, int bits) => (value << bits) | (value >> (32 - bits));
        static uint Round(uint accumulator, uint word) => Rotate(unchecked(accumulator + (word * prime2)), 13) * prime1;
        int index = 0;
        uint hash;
        unchecked
        {
            if (values.Length >= 4)
            {
                uint[] state = [seed + prime1 + prime2, seed + prime2, seed, seed - prime1];
                for (; index + 4 <= values.Length; index += 4)
                {
                    for (int lane = 0; lane < 4; lane++)
                    {
                        state[lane] = Round(state[lane], (uint)values[index + lane]);
                    }
                }

                hash = Rotate(state[0], 1) + Rotate(state[1], 7) + Rotate(state[2], 12) + Rotate(state[3], 18);
            }
            else
            {
                hash = seed + prime5;
            }

            hash += (uint)values.Length * 4;
            for (; index < values.Length; index++)
            {
                hash = Rotate(hash + ((uint)values[index] * prime3), 17) * prime4;
            }

            hash ^= hash >> 15;
            hash *= prime2;
            hash ^= hash >> 13;
            hash *= prime3;
            hash ^= hash >> 16;
            return (int)hash;
        }
    }

    private sealed class FixedHashComparer : IEqualityComparer<string>
    {
        public bool Equals(string? left, string? right) => StringComparer.Ordinal.Equals(left, right);

        public int GetHashCode(string value) => 173;
    }

    private sealed record PooledValue(int Id);
}
