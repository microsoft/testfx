// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Extensions.DependencyInjection;

internal sealed class MicrosoftExtensionsTestClassInstanceLease(object instance, IServiceScope scope) : ITestClassInstanceLease
{
    public object Instance { get; } = instance;

    public bool RequiresCleanup => true;

    public async Task DisposeAsync()
    {
        List<Exception>? exceptions = null;

        try
        {
#if NET6_0_OR_GREATER
            if (Instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
#endif
        }
        catch (Exception exception)
        {
            (exceptions ??= []).Add(exception);
        }

        try
        {
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception exception)
        {
            (exceptions ??= []).Add(exception);
        }

        try
        {
            if (scope is IAsyncDisposable asyncScope)
            {
                await asyncScope.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                scope.Dispose();
            }
        }
        catch (Exception exception)
        {
            (exceptions ??= []).Add(exception);
        }

        if (exceptions is [Exception singleException])
        {
            ExceptionDispatchInfo.Capture(singleException).Throw();
        }

        if (exceptions is not null)
        {
            throw new AggregateException(exceptions);
        }
    }
}
