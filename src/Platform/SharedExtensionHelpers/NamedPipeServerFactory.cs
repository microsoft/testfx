// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions;

internal static class NamedPipeServerFactory
{
    [UnsupportedOSPlatform("browser")]
    public static NamedPipeServerEndpoint CreateEndpoint()
        => new(NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N")).Name);

    [UnsupportedOSPlatform("browser")]
    public static NamedPipeServer CreateAndBind(
        NamedPipeServerEndpoint endpoint,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var server = new NamedPipeServer(
            new PipeNameDescription(endpoint.PipeName),
            callback,
            environment,
            logger,
            task,
            maxNumberOfServerInstances: 1,
            serviceProvider.GetTestHostControllerAuthorizedSecurityIdentities(),
            cancellationToken);
        endpoint.PipeName = server.PipeName.Name;

        return server;
    }
}
