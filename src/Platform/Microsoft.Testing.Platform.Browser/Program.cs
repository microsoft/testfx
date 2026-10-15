// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class Program
{
    private const string WorkerArgument = "--internal-browser-worker";

    public static async Task<int> Main(string[] args)
        => args is [WorkerArgument, .. string[] workerArguments]
            ? await BrowserWorker.RunAsync(workerArguments).ConfigureAwait(false)
            : await BrowserSupervisor.RunAsync(args).ConfigureAwait(false);
}
