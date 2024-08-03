// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.IPC;

internal class DotnetTestDataConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    public Type[] DataTypesConsumed => new[]
    {
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(TestNodeFileArtifact),
        typeof(FileArtifact),
        typeof(TestRequestExecutionTimeInfo),
    };

    public string Uid => nameof(DotnetTestDataConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(DotnetTestDataConsumer);

    public string Description => "Send back to the dotnet test informations";

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;
}
