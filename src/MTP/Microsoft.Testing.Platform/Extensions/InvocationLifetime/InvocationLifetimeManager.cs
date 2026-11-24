// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

internal sealed class InvocationLifetimeManager : IInvocationLifetimeManager
{
    private readonly List<Func<IServiceProvider, IInvocationLifetimeHandler>> _factories = [];

    public bool HasHandlers => _factories.Count > 0;

    public void AddInvocationLifetimeHandler(Func<IServiceProvider, IInvocationLifetimeHandler> factory)
        => _factories.Add(factory ?? throw new ArgumentNullException(nameof(factory)));

    public async Task<IReadOnlyList<IInvocationLifetimeHandler>> BuildAsync(IServiceProvider serviceProvider)
    {
        List<IInvocationLifetimeHandler> handlers = [];
        IInvocationLifetimeHandler? current = null;
        try
        {
            for (int i = 0; i < _factories.Count; i++)
            {
                current = _factories[i](serviceProvider);
                if (!await current.IsEnabledAsync().ConfigureAwait(false))
                {
                    // A disabled handler is never invoked, so it is released right away.
                    IInvocationLifetimeHandler disabled = current;
                    current = null;
                    await DisposeHelper.DisposeAsync(disabled).ConfigureAwait(false);
                    continue;
                }

                handlers.ValidateUniqueExtension(current);
                await current.TryInitializeAsync().ConfigureAwait(false);
                handlers.Add(current);
                current = null;
            }
        }
        catch
        {
            // The handlers already built never reach a runner: release them before surfacing the failure.
            if (current is not null)
            {
                try
                {
                    await DisposeHelper.DisposeAsync(current).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Preserve the build failure and continue releasing the other handlers.
                }
            }

            foreach (IInvocationLifetimeHandler handler in handlers)
            {
                try
                {
                    await DisposeHelper.DisposeAsync(handler).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A failed disposal must not prevent releasing the remaining handlers.
                }
            }

            throw;
        }

        return handlers;
    }
}
