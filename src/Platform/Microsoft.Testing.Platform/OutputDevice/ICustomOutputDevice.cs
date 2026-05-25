// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents a custom output device that can be registered via
/// <see cref="IOutputDeviceManager.SetOutputDevice(System.Func{System.IServiceProvider, ICustomOutputDevice})"/>
/// to replace the default terminal output of Microsoft.Testing.Platform.
/// </summary>
/// <remarks>
/// When server mode (JSON-RPC) is active, the platform still routes server output through its
/// built-in server output device in parallel with the custom output device.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ICustomOutputDevice : IExtension
{
    /// <summary>
    /// Displays the platform banner.
    /// </summary>
    /// <param name="bannerMessage">An optional banner message. If <see langword="null"/>, the implementation may render its own banner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken);

    /// <summary>
    /// Called before the test session starts.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after the test session ends.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Displays the output data asynchronously.
    /// </summary>
    /// <param name="producer">The data producer.</param>
    /// <param name="data">The output data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken);
}
