// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.Helpers;

internal sealed class AsyncLazy<T> : Lazy<Task<T>>
{
    public AsyncLazy(Func<T> valueFactory, LazyThreadSafetyMode mode)
        : base(() => Task.Run(valueFactory), mode)
    {
    }

    public AsyncLazy(Func<Task<T>> taskFactory, LazyThreadSafetyMode mode)
        : base(() => Task.Factory.StartNew(() => taskFactory(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap(), mode)
    {
    }

    public TaskAwaiter<T> GetAwaiter() => Value.GetAwaiter();
}
