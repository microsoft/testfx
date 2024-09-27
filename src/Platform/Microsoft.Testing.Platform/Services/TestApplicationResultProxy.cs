// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Services;

internal class TestApplicationResultProxy : ITestApplicationProcessExitCode
{
    private ITestApplicationProcessExitCode? _testApplicationProcessExitCode;

    public bool HasTestAdapterTestSessionFailure
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.HasTestAdapterTestSessionFailure;

    public string? TestAdapterTestSessionFailureErrorMessage
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.TestAdapterTestSessionFailureErrorMessage;

    public Type[] DataTypesConsumed
         => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.DataTypesConsumed;

    public string Uid
         => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.Uid;

    public string Version
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.Version;

    public string DisplayName
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.DisplayName;

    public string Description
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.Description;

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.ConsumeAsync(dataProducer, value, cancellationToken);

    public int GetProcessExitCode()
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.GetProcessExitCode();

    public Statistics GetStatistics()
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.GetStatistics();

    public Task<bool> IsEnabledAsync()
        => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.IsEnabledAsync();

    public Task SetTestAdapterTestSessionFailureAsync(string errorMessage)
         => _testApplicationProcessExitCode is null ?
            throw new InvalidOperationException(Resources.PlatformResources.TestApplicationResultNotReady) :
            _testApplicationProcessExitCode.SetTestAdapterTestSessionFailureAsync(errorMessage);

    public void SetTestApplicationProcessExitCode(ITestApplicationProcessExitCode testApplicationProcessExitCode)
    {
        Guard.NotNull(testApplicationProcessExitCode);
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
    }
}
