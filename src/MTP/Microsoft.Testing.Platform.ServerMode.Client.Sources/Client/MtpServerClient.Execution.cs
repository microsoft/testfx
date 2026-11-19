// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed partial class MtpServerClient
{
    /// <inheritdoc />
    public Task DiscoverTestsAsync(CancellationToken cancellationToken = default)
        => DiscoverCoreAsync(null, null, cancellationToken);

    /// <inheritdoc />
    public Task DiscoverTestsAsync(IReadOnlyCollection<string> testNodeUids, CancellationToken cancellationToken = default)
        => DiscoverCoreAsync(BuildTestNodes(testNodeUids ?? throw new ArgumentNullException(nameof(testNodeUids))), null, cancellationToken);

    /// <inheritdoc />
    public Task DiscoverTestsWithFilterAsync(string graphFilter, CancellationToken cancellationToken = default)
        => DiscoverCoreAsync(null, graphFilter ?? throw new ArgumentNullException(nameof(graphFilter)), cancellationToken);

    /// <inheritdoc />
    public Task<MtpRunResult> RunTestsAsync(CancellationToken cancellationToken = default)
        => RunCoreAsync(null, null, cancellationToken);

    /// <inheritdoc />
    public Task<MtpRunResult> RunTestsAsync(IReadOnlyCollection<string> testNodeUids, CancellationToken cancellationToken = default)
        => RunCoreAsync(BuildTestNodes(testNodeUids ?? throw new ArgumentNullException(nameof(testNodeUids))), null, cancellationToken);

    /// <inheritdoc />
    public Task<MtpRunResult> RunTestsWithFilterAsync(string graphFilter, CancellationToken cancellationToken = default)
        => RunCoreAsync(null, graphFilter ?? throw new ArgumentNullException(nameof(graphFilter)), cancellationToken);

    private static ICollection<TestNode> BuildTestNodes(IReadOnlyCollection<string> testNodeUids)
        => testNodeUids.Select(uid => new TestNode { Uid = uid, DisplayName = uid }).ToList();

    private async Task DiscoverCoreAsync(ICollection<TestNode>? tests, string? graphFilter, CancellationToken cancellationToken)
    {
        EnsureStarted();
        var args = new DiscoverRequestArgs(Guid.NewGuid(), tests, graphFilter);
        await _connection.SendRequestAsync(JsonRpcMethods.TestingDiscoverTests, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MtpRunResult> RunCoreAsync(ICollection<TestNode>? tests, string? graphFilter, CancellationToken cancellationToken)
    {
        EnsureStarted();
        var args = new RunRequestArgs(Guid.NewGuid(), tests, graphFilter);
        ResponseMessage response = await _connection.SendRequestAsync(JsonRpcMethods.TestingRunTests, args, cancellationToken).ConfigureAwait(false);

        IDictionary<string, object?> resultDict = AsResultDictionary(response.Result) ?? new Dictionary<string, object?>();
        RunResponseArgs runResponse = SerializerUtilities.Deserialize<RunResponseArgs>(resultDict);
        MtpAttachment[] artifacts = runResponse.Artifacts
            .Select(artifact => new MtpAttachment(artifact.Uri, artifact.Producer, artifact.Type, artifact.DisplayName, artifact.Description))
            .ToArray();

        return new MtpRunResult(artifacts);
    }

    private async Task<object?> OnServerRequestAsync(RequestMessage request, CancellationToken cancellationToken)
    {
        Func<string, IDictionary<string, object?>?, CancellationToken, Task<IDictionary<string, object?>?>>? handler =
            Volatile.Read(ref _serverRequestHandler);
        if (handler is null)
        {
            return null;
        }

        IDictionary<string, object?>? result = await handler(request.Method, request.Params as IDictionary<string, object?>, cancellationToken).ConfigureAwait(false);

        // The response serializer is registered for the concrete Dictionary<string, object?> the formatter
        // writes, so normalize any other IDictionary implementation to that exact type. This keeps the
        // connection's "always answers" guarantee: a non-Dictionary result would otherwise throw on
        // serialize and the server would wait forever for a response that never comes.
        if (result is null or Dictionary<string, object?>)
        {
            return result;
        }

        var normalized = new Dictionary<string, object?>(result);
        return normalized;
    }
}
