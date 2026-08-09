// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    private async Task<NamedPipeServer> CreateTestHostControllerIpcAsync(ExecutableInfo executableInfo, CancellationToken cancellationToken)
    {
        NamedPipeServer testHostControllerIpc = new(
            $"MONITORTOHOST_{Guid.NewGuid():N}",
            HandleRequestAsync,
            _environment,
            _loggerFactory.CreateLogger<NamedPipeServer>(),
            ServiceProvider.GetTask(),
            await GetAuthorizedSecurityIdentitiesAsync(_testHostsInformation.TestHostLauncher, executableInfo.FilePath, cancellationToken).ConfigureAwait(false),
            cancellationToken);
        testHostControllerIpc.RegisterAllSerializers();
        return testHostControllerIpc;
    }

    /// <summary>
    /// Asks the registered launcher, when it implements
    /// <see cref="ITestHostControllerConnectionAuthorizer"/>, for the security identities that must
    /// additionally be authorized on the controller-to-host connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs before the connection is created, which is the only point where its access control can
    /// still be composed: it has to be listening before the host is launched, so the launcher cannot
    /// contribute this from <see cref="ITestHostLauncher.LaunchTestHostAsync"/>.
    /// </para>
    /// <para>
    /// Every returned value is validated against the platform's least-privilege policy: only the identity of
    /// a single sandboxed application may be authorized, never a user, a group, <c>Everyone</c>, or an
    /// identity shared by every sandboxed application on the machine. An extension that asks for anything
    /// else fails the run instead of silently getting a weaker connection, so a mistake in an extension
    /// cannot degrade into an over-permissive access control list.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<string>?> GetAuthorizedSecurityIdentitiesAsync(
        ITestHostLauncher? testHostLauncher,
        string testHostFileName,
        CancellationToken cancellationToken)
    {
        if (testHostLauncher is not ITestHostControllerConnectionAuthorizer connectionAuthorizer)
        {
            return null;
        }

        IReadOnlyList<string> extensionResult = await connectionAuthorizer.GetAuthorizedSecurityIdentitiesAsync(testHostFileName, cancellationToken).ConfigureAwait(false);
        if (extensionResult is null || extensionResult.Count == 0)
        {
            return null;
        }

        // Snapshot immediately. Everything below — validation, logging, and ultimately the security
        // descriptor — must operate on one fixed set of values; the sequence itself is extension-supplied
        // and re-enumerating it is not guaranteed to yield what was validated.
        string[] securityIdentities = [.. extensionResult];

        if (!NamedPipeServerSecurity.IsSupported)
        {
            // Sandboxed-application identities and connection access control lists are expressible only on
            // Windows. Anywhere else the request is meaningless, so it is ignored rather than failing an
            // otherwise valid run.
            await _logger.LogDebugAsync($"'{testHostLauncher.Uid}' requested {securityIdentities.Length} connection authorization(s), ignored on this operating system.").ConfigureAwait(false);
            return null;
        }

        foreach (string securityIdentity in securityIdentities.Where(static securityIdentity =>
            !NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity(securityIdentity)))
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.TestHostControllerConnectionInvalidAuthorizedSecurityIdentityErrorMessage,
                testHostLauncher.DisplayName,
                testHostLauncher.Uid,
                securityIdentity ?? "<null>",
                NamedPipeServerSecurity.AllApplicationPackagesSid));
        }

        await _logger.LogDebugAsync($"'{testHostLauncher.Uid}' authorized the following security identity/identities on the test host controller connection: {string.Join(", ", securityIdentities)}").ConfigureAwait(false);
        return securityIdentities;
    }

    private Task<IResponse> HandleRequestAsync(IRequest request)
    {
        try
        {
            switch (request)
            {
                case TestHostCompletedRequest testHostCompletedRequest:
                    _testHostCompletedReceived = true;
                    _testHostExitCodeReceived = testHostCompletedRequest.ExitCode;
                    _testHostUnfilteredExitCodeReceived = testHostCompletedRequest.UnfilteredExitCode;
                    return Task.FromResult<IResponse>(VoidResponse.CachedInstance);

                case TestHostProcessPIDRequest testHostProcessPIDRequest:
                    _testHostPID = testHostProcessPIDRequest.PID;
                    _waitForPid.Set();
                    return Task.FromResult<IResponse>(VoidResponse.CachedInstance);

                default:
                    throw new NotSupportedException($"Request '{request}' not supported");
            }
        }
        catch (Exception ex)
        {
            _environment.FailFast($"[TestHostControllersTestHost] Unhandled exception:\n{ex}", ex);
            throw;
        }
    }
}
