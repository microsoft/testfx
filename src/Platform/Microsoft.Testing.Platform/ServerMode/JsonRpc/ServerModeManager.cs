// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed partial class ServerModeManager
{
    internal static IMessageHandlerFactory Build(IServiceProvider serviceProvider)
    {
        ICommandLineOptions commandLineService = serviceProvider.GetCommandLineOptions();

        int? clientPort;
        clientPort = commandLineService.TryGetOptionArgumentList(PlatformCommandLineProvider.ClientPortOptionKey, out string[]? clientPortArgs)
            ? int.Parse(clientPortArgs[0], CultureInfo.InvariantCulture)
            : throw new InvalidOperationException(PlatformResources.MissingClientPortFoJsonRpc);

        string clientHostName = commandLineService.TryGetOptionArgumentList(PlatformCommandLineProvider.ClientHostOptionKey, out string[]? clientHostArgs)
            ? clientHostArgs[0]
            : "localhost";

        return new MessageHandlerFactory(clientHostName, clientPort.Value, serviceProvider.GetOutputDevice());
    }
}
