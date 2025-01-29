// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class TestHostResult(string command, int exitCode, string standardOutput, ReadOnlyCollection<string> standardOutputLines, string standardError, ReadOnlyCollection<string> standardErrorLines)
{
    public string Command { get; } = command;

    public int ExitCode { get; } = exitCode;

    public string StandardOutput { get; } = standardOutput;

    public ReadOnlyCollection<string> StandardOutputLines { get; } = standardOutputLines;

    public string StandardError { get; } = standardError;

    public ReadOnlyCollection<string> StandardErrorLines { get; } = standardErrorLines;

    public override string ToString() =>
        $"""
         Command: {Command}
         ====================
         ExitCode: {ExitCode}
         ====================
         StandardOutput:
         {StandardOutput}
         ====================
         StandardError:
         {StandardError}
         """;
}
