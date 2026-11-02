// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Manages the registration of a custom output device for Microsoft.Testing.Platform.
/// </summary>
/// <remarks>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// <para>
/// Only one custom output device can be registered. Calling
/// <see cref="SetOutputDevice(Func{IServiceProvider, ICustomOutputDevice})"/>
/// more than once throws <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IOutputDeviceManager
{
    /// <summary>
    /// Sets the custom output device factory. The factory is invoked once when the test host is built.
    /// </summary>
    /// <param name="outputDeviceFactory">A factory that builds the <see cref="ICustomOutputDevice"/> from the service provider.</param>
    /// <remarks>
    /// The factory is not invoked when the dotnet test pipe protocol owns the output.
    /// If the resolved <see cref="ICustomOutputDevice"/> reports itself as disabled via
    /// <see cref="Microsoft.Testing.Platform.Extensions.IExtension.IsEnabledAsync"/>, the platform falls back
    /// to its default terminal output device.
    /// </remarks>
    void SetOutputDevice(Func<IServiceProvider, ICustomOutputDevice> outputDeviceFactory);
}
