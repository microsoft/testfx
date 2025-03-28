// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryExecutionFilter(IServiceProvider serviceProvider)
    : TestNodeUidListFilter(GetRetryableTests(serviceProvider))
{
    private readonly ICommandLineOptions _commandLineOptions = serviceProvider.GetCommandLineOptions();

    private static TestNodeUid[] GetRetryableTests(IServiceProvider serviceProvider)
    {
        RetryLifecycleCallbacks retryLifecycleCallbacks = serviceProvider.GetRequiredService<RetryLifecycleCallbacks>();

        return retryLifecycleCallbacks.FailedTestsIDToRetry?
            .Select(x => new TestNodeUid(x)).ToArray() ?? [];
    }

    public string Uid => nameof(RetryExecutionFilter);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName));
}
