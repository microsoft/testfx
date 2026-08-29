// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    private async Task<NamedPipeServer> CreateTestHostControllerIpcAsync(ExecutableInfo executableInfo, CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? authorizedSecurityIdentities = await ServiceProvider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(
            _testHostsInformation.TestHostLauncher,
            executableInfo.FilePath,
            _logger,
            cancellationToken).ConfigureAwait(false);

        NamedPipeServer testHostControllerIpc = new(
            $"MONITORTOHOST_{Guid.NewGuid():N}",
            HandleRequestAsync,
            _environment,
            _loggerFactory.CreateLogger<NamedPipeServer>(),
            ServiceProvider.GetTask(),
            authorizedSecurityIdentities,
            cancellationToken);
        testHostControllerIpc.RegisterAllSerializers();
        return testHostControllerIpc;
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
