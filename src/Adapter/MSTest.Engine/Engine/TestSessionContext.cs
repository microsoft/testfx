// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Framework;

internal sealed class TestSessionContext : ITestSessionContext
{
    private readonly SessionUid _sessionUid;
    private readonly Func<IData, Task> _publishDataAsync;

    public TestSessionContext(IConfiguration configuration, ITestFixtureManager _, ITestArgumentsManager _2,
        SessionUid sessionUid, Func<IData, Task> publishDataAsync, CancellationToken cancellationToken)
    {
        Configuration = configuration;

        _sessionUid = sessionUid;
        _publishDataAsync = publishDataAsync;
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    public IConfiguration Configuration { get; }

    public async Task AddTestAttachmentAsync(FileInfo file, string displayName, string? description = null)
        => await _publishDataAsync(new SessionFileArtifact(_sessionUid, file, displayName, description)).ConfigureAwait(false);
}
