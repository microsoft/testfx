// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

#pragma warning disable CA1000 // Do not declare static members on generic types

namespace Analyzer.Utilities.PooledObjects;

// HashSet that can be recycled via an object pool
internal sealed class PooledHashSet<T> : HashSet<T>, IDisposable
{
    private readonly ObjectPool<PooledHashSet<T>>? _pool;

    private PooledHashSet(ObjectPool<PooledHashSet<T>>? pool, IEqualityComparer<T>? comparer)
        : base(comparer)
    {
        _pool = pool;
    }

    public void Dispose() => Free(CancellationToken.None);

    public void Free(CancellationToken cancellationToken)
    {
        // Do not free in presence of cancellation.
        // See https://github.com/dotnet/roslyn/issues/46859 for details.
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Clear();
        _pool?.Free(this, cancellationToken);
    }

    public ImmutableHashSet<T> ToImmutableAndFree()
    {
        ImmutableHashSet<T> result;
        if (Count == 0)
        {
            result = ImmutableHashSet<T>.Empty;
        }
        else
        {
            result = this.ToImmutableHashSet(Comparer);
            Clear();
        }

        _pool?.Free(this, CancellationToken.None);
        return result;
    }

    public ImmutableHashSet<T> ToImmutable()
        => Count == 0 ? ImmutableHashSet<T>.Empty : this.ToImmutableHashSet(Comparer);

    // global pool
    private static readonly ObjectPool<PooledHashSet<T>> s_poolInstance = CreatePool();
    private static readonly ConcurrentDictionary<IEqualityComparer<T>, ObjectPool<PooledHashSet<T>>> s_poolInstancesByComparer = new();

    // if someone needs to create a pool;
    public static ObjectPool<PooledHashSet<T>> CreatePool(IEqualityComparer<T>? comparer = null)
    {
        ObjectPool<PooledHashSet<T>>? pool = null;
        pool = new ObjectPool<PooledHashSet<T>>(() => new PooledHashSet<T>(pool, comparer), 128);
        return pool;
    }

    public static PooledHashSet<T> GetInstance(IEqualityComparer<T>? comparer = null)
    {
        ObjectPool<PooledHashSet<T>> pool = comparer == null ?
            s_poolInstance :
            s_poolInstancesByComparer.GetOrAdd(comparer, CreatePool);
        PooledHashSet<T> instance = pool.Allocate();
        Debug.Assert(instance.Count == 0);
        return instance;
    }

    public static PooledHashSet<T> GetInstance(IEnumerable<T> initializer, IEqualityComparer<T>? comparer = null)
    {
        PooledHashSet<T> instance = GetInstance(comparer);
        foreach (T? value in initializer)
        {
            instance.Add(value);
        }

        return instance;
    }
}
