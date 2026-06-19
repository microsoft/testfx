// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// A no-op <see cref="IPlatformOutputDevice"/> used when the host should not produce any
/// console output of its own — currently when running under the dotnet test pipe protocol,
/// where the SDK's TerminalTestReporter is solely responsible for reporting progress and results.
/// </summary>
/// <remarks>
/// Returning a real (no-op) instance instead of <see langword="null"/> keeps every downstream
/// consumer of <see cref="IPlatformOutputDevice"/> / <see cref="ProxyOutputDevice"/> simple
/// and null-free. <see cref="IsEnabledAsync"/> returns <see langword="false"/> so
/// <c>RegisterAsServiceOrConsumerOrBothAsync</c> skips it as a data consumer.
/// </remarks>
internal sealed class NopPlatformOutputDevice : IPlatformOutputDevice
{
    public string Uid => nameof(NopPlatformOutputDevice);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(NopPlatformOutputDevice);

    public string Description => "Output device that discards all output. Used when the test host should not produce any console output (e.g. dotnet test pipe protocol).";

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken) => Task.CompletedTask;
}
