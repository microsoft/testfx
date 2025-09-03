// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public interface IProcessHandle
{
    int Id { get; }

    string ProcessName { get; }

    int ExitCode { get; }

    TextWriter StandardInput { get; }

    TextReader StandardOutput { get; }

    void Dispose();

    void Kill();

    Task<int> StopAsync(CancellationToken cancellationToken = default);

    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

    Task WriteInputAsync(string input);
}
