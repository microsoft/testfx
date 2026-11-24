// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.Helpers;

[TestClass]
public sealed class ObjectPoolTests
{
    [TestMethod]
    public void Allocate_BoundedPoolReusesEverySlotInOrderAndDropsOverflow()
    {
        int created = 0;
        ObjectPool<Lease> pool = new(() => new Lease(++created), 3);
        Lease first = pool.Allocate();
        Lease second = pool.Allocate();
        Lease third = pool.Allocate();
        Lease overflow = pool.Allocate();
        Assert.AreSequenceEqual([1, 2, 3, 4], new[] { first.Id, second.Id, third.Id, overflow.Id });
        pool.Free(first, CancellationToken.None);
        pool.Free(second, CancellationToken.None);
        pool.Free(third, CancellationToken.None);
        pool.Free(overflow, CancellationToken.None);

        Assert.AreSame(first, pool.Allocate());
        Assert.AreSame(second, pool.Allocate());
        Assert.AreSame(third, pool.Allocate());
        Assert.AreEqual(5, pool.Allocate().Id);
        Assert.AreEqual(5, created);
    }

    [TestMethod]
    public void Free_CanceledReturnDoesNotReplaceRetainedItem()
    {
        int created = 0;
        ObjectPool<Lease> pool = new(() => new Lease(++created), 1);
        Lease retained = pool.Allocate();
        Lease canceled = pool.Allocate();
        pool.Free(retained, CancellationToken.None);
        pool.Free(canceled, new CancellationToken(canceled: true));

        Assert.AreSame(retained, pool.Allocate());
        Assert.AreEqual(3, pool.Allocate().Id);
        Assert.AreEqual(3, created);
    }

    [TestMethod]
    public void Allocate_DefaultPoolReusesReturnedInstanceWithoutCallingFactory()
    {
        int created = 0;
        ObjectPool<Lease> pool = new(() => new Lease(++created));
        Lease lease = pool.Allocate();
        pool.Free(lease, CancellationToken.None);

        Assert.AreSame(lease, pool.Allocate());
        Assert.AreEqual(1, created);
        Assert.AreEqual(1, lease.Id);
    }

    private sealed class Lease(int id)
    {
        public int Id { get; } = id;
    }
}
