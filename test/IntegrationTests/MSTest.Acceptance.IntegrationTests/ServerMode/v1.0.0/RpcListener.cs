// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

internal sealed class ConsoleRpcListener : TraceListener
{
    public override void Write(string? message) => Console.Write(message ?? string.Empty);

    public override void WriteLine(string? message) => Console.WriteLine(message ?? string.Empty);
}
