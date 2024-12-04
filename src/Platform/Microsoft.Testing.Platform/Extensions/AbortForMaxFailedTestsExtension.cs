// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions;

internal sealed class AbortForMaxFailedTestsExtension : IDataConsumer
{
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly int? _maxFailedTests;

    private int _failCount;

    public AbortForMaxFailedTestsExtension(ICommandLineOptions commandLineOptions, ITestApplicationCancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        if (commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.MaxFailedTestsOptionKey, out string[]? args) &&
            int.TryParse(args[0], out int maxFailedTests) &&
            maxFailedTests > 0)
        {
            _maxFailedTests = maxFailedTests;
        }
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    /// <inheritdoc />
    public string Uid { get; } = nameof(AbortForMaxFailedTestsExtension);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = nameof(AbortForMaxFailedTestsExtension);

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.AbortForMaxFailedTestsDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_maxFailedTests.HasValue);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var node = (TestNodeUpdateMessage)value;

        // If we are called, the extension is enabled, which means _maxFailedTests.HasValue was true. So null suppression is safe.
        int maxFailed = _maxFailedTests!.Value;
        if (node.TestNode.Properties.Single<TestNodeStateProperty>() is FailedTestNodeStateProperty)
        {
            Interlocked.Increment(ref _failCount);
        }

        if (_failCount > maxFailed)
        {
            _cancellationTokenSource.Cancel();
        }

        return Task.CompletedTask;
    }
}
