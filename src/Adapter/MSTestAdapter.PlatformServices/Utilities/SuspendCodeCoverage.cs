// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

/// <summary>
/// Suspends dynamic code-coverage instrumentation of the modules that are loaded while this object is alive
/// (between construction and disposal) by setting the well-known collector environment variable. The previous
/// value of the environment variable is restored on dispose.
/// </summary>
internal sealed class SuspendCodeCoverage : IDisposable
{
    private const string SuspendCodeCoverageEnvVarName = "__VANGUARD_SUSPEND_INSTRUMENT__";
    private const string SuspendCodeCoverageEnvVarTrueValue = "TRUE";

    private readonly string? _previousEnvironmentValue;

    private bool _isDisposed;

    public SuspendCodeCoverage()
    {
        _previousEnvironmentValue = Environment.GetEnvironmentVariable(SuspendCodeCoverageEnvVarName, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(SuspendCodeCoverageEnvVarName, SuspendCodeCoverageEnvVarTrueValue, EnvironmentVariableTarget.Process);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Environment.SetEnvironmentVariable(SuspendCodeCoverageEnvVarName, _previousEnvironmentValue, EnvironmentVariableTarget.Process);
        _isDisposed = true;
    }
}
#endif
