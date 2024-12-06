// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class PassiveNodeDataConsumer : IDataConsumer, IDisposable
{
    private const string FileType = "file";
    private readonly PassiveNode? _passiveNode;

    public PassiveNodeDataConsumer(PassiveNode? passiveNode)
        => _passiveNode = passiveNode;

    public Type[] DataTypesConsumed
        => [typeof(SessionFileArtifact), typeof(TestNodeFileArtifact), typeof(FileArtifact)];

    public string Uid
        => nameof(PassiveNodeDataConsumer);

    public string Version
        => AppVersion.DefaultSemVer;

    public string DisplayName
        => nameof(PassiveNodeDataConsumer);

    public string Description
        => "Push information as passive node";

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_passiveNode is not null);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (_passiveNode is null)
        {
            return;
        }

        switch (value)
        {
            case TestNodeFileArtifact testNodeFileArtifact:
                {
                    RunTestAttachment runTestAttachment = new(testNodeFileArtifact.FileInfo.FullName, dataProducer.Uid, FileType, testNodeFileArtifact.DisplayName, testNodeFileArtifact.Description);
                    await _passiveNode.SendAttachmentsAsync(new TestsAttachments([runTestAttachment]), cancellationToken);
                    break;
                }

            case SessionFileArtifact sessionFileArtifact:
                {
                    RunTestAttachment runTestAttachment = new(sessionFileArtifact.FileInfo.FullName, dataProducer.Uid, FileType, sessionFileArtifact.DisplayName, sessionFileArtifact.Description);
                    await _passiveNode.SendAttachmentsAsync(new TestsAttachments([runTestAttachment]), cancellationToken);
                    break;
                }

            case FileArtifact fileArtifact:
                {
                    RunTestAttachment runTestAttachment = new(fileArtifact.FileInfo.FullName, dataProducer.Uid, FileType, fileArtifact.DisplayName, fileArtifact.Description);
                    await _passiveNode.SendAttachmentsAsync(new TestsAttachments([runTestAttachment]), cancellationToken);
                    break;
                }

            default:
                break;
        }
    }

    public void Dispose()
        => _passiveNode?.Dispose();
}
