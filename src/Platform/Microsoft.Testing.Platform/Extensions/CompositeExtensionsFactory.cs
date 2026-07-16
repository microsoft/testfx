// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Represents a factory for creating composite extensions.
/// </summary>
/// <typeparam name="TExtension">The type of the extension.</typeparam>
/// <remarks>
/// This helper type is used to create a composite extension that is composed of multiple extensions without having to
/// handle either the communication between the extensions or the lifetime of the extensions instances.
/// </remarks>
public class CompositeExtensionFactory<TExtension> : ICompositeExtensionFactory, ICloneable
    where TExtension : class, IExtension
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new();
#else
    private readonly object _syncLock = new();
#endif

    private readonly Func<IServiceProvider, TExtension>? _factoryWithServiceProvider;
    private readonly Func<TExtension>? _factory;
    private TExtension? _instance;

    // This raw string literal's value embeds the source file's line endings (CRLF on Windows, LF on Linux),
    // so its compile-time const value is not deterministic across platforms and must not be tracked by the
    // internal API analyzers.
#pragma warning disable RS0051 // Add internal types and members to the declared API
    internal const /* for testing */ string ValidateCompositionErrorMessage =
"""
You cannot compose extensions that belong to different areas.
Valid composition are:
TestHostControllers: ITestHostProcessLifetimeHandler, ITestHostEnvironmentVariableProvider
TestHost: IDataConsumer, ITestApplicationLifetime
""";
#pragma warning restore RS0051 // Add internal types and members to the declared API

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeExtensionFactory{TExtension}"/> class.
    /// </summary>
    /// <param name="factory">The factory function that creates the extension with a service provider.</param>
    public CompositeExtensionFactory(Func<IServiceProvider, TExtension> factory)
        => _factoryWithServiceProvider = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeExtensionFactory{TExtension}"/> class.
    /// </summary>
    /// <param name="factory">The factory function that creates the extension.</param>
    public CompositeExtensionFactory(Func<TExtension> factory)
        => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <inheritdoc/>
    object ICloneable.Clone()
        => _factory is not null
            ? new CompositeExtensionFactory<TExtension>(_factory)
            : new CompositeExtensionFactory<TExtension>(_factoryWithServiceProvider!);

    /// <inheritdoc/>
    object ICompositeExtensionFactory.GetInstance(IServiceProvider? serviceProvider)
    {
        lock (_syncLock)
        {
            if (Volatile.Read(ref _instance) is null)
            {
                try
                {
                    if (_factoryWithServiceProvider is not null)
                    {
                        Volatile.Write(ref _instance, _factoryWithServiceProvider(serviceProvider!));
                    }

                    if (_factory is not null)
                    {
                        Volatile.Write(ref _instance, _factory());
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    // Preserve the original exception as InnerException so the underlying failure (e.g. a
                    // dependency the factory tried to resolve or construct) is never lost, while making the
                    // outer message actionable by identifying which composite extension failed to build.
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, PlatformResources.CompositeExtensionFactoryInstantiationFailedErrorMessage, typeof(TExtension)),
                        ex);
                }

                if (_instance is null)
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, PlatformResources.CompositeExtensionFactoryReturnedNullInstanceErrorMessage, typeof(TExtension)));
                }
            }
        }

        ValidateComposition();

        return _instance!;
    }

    private void ValidateComposition()
    {
        if (ContainsTestHostExtension() && ContainsTestHostControllerExtension())
        {
            throw new InvalidOperationException(ValidateCompositionErrorMessage);
        }
    }

    private bool ContainsTestHostExtension() => _instance is ITestSessionLifetimeHandler;

    private bool ContainsTestHostControllerExtension() => _instance is ITestHostProcessLifetimeHandler or ITestHostEnvironmentVariableProvider;
}
