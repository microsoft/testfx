// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class DotnetMuxerResult(string args, int exitCode, string standardOutput, ReadOnlyCollection<string> standardOutputLines,
    string standardError, ReadOnlyCollection<string> standardErrorLines)
{
    public string Args { get; } = args;

    public int ExitCode { get; } = exitCode;

    public string StandardOutput { get; } = standardOutput;

    public ReadOnlyCollection<string> StandardOutputLines { get; } = standardOutputLines;

    public string StandardError { get; } = standardError;

    public ReadOnlyCollection<string> StandardErrorLines { get; } = standardErrorLines;

    public override string ToString()
    {
        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Args: {Args}");
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"ExitCode: {ExitCode}");
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"StandardOutput: {StandardOutput}");
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"StandardError: {StandardError}");

        return stringBuilder.ToString();
    }
}
