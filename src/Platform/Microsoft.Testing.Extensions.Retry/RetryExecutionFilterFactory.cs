// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryExecutionFilterFactory : ITestExecutionFilterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private RetryLifecycleCallbacks? _retryFailedTestsLifecycleCallbacks;

    public RetryExecutionFilterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = serviceProvider.GetCommandLineOptions();
    }

    public string Uid => nameof(RetryExecutionFilterFactory);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName));

    public async Task<(bool, ITestExecutionFilter?)> TryCreateAsync()
    {
        _retryFailedTestsLifecycleCallbacks = _serviceProvider.GetRequiredService<RetryLifecycleCallbacks>();
        if (_retryFailedTestsLifecycleCallbacks.FailedTestsIDToRetry?.Length > 0)
        {
            return (true, new TestNodeUidListFilter([.. _retryFailedTestsLifecycleCallbacks.FailedTestsIDToRetry.Select(x => new TestNodeUid(x))]));
        }
        else
        {
            ConsoleTestExecutionFilterFactory consoleTestExecutionFilterFactory = new(_commandLineOptions);
            return await consoleTestExecutionFilterFactory.TryCreateAsync().ConfigureAwait(false);
        }
    }
}
