// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MtpTestNodeHierarchyPublisher(IMessageBus messageBus, IDataProducer dataProducer, SessionUid sessionUid)
{
    private readonly Dictionary<TestNodeUid, Task> _publishedGroups = [];
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif

    public async Task PublishForTestElementAsync(UnitTestElement element)
    {
        TestNodeUid assemblyUid = MSTestTestNodeConverter.GetAssemblyTestNodeUid(element);
        await PublishGroupAsync(MSTestTestNodeConverter.ToAssemblyTestNode(element), parentUid: null).ConfigureAwait(false);
        await PublishGroupAsync(MSTestTestNodeConverter.ToClassTestNode(element), assemblyUid).ConfigureAwait(false);
    }

    private Task PublishGroupAsync(TestNode testNode, TestNodeUid? parentUid)
    {
        lock (_syncRoot)
        {
            if (!_publishedGroups.TryGetValue(testNode.Uid, out Task? publication))
            {
                publication = messageBus.PublishAsync(dataProducer, new TestNodeUpdateMessage(sessionUid, testNode, parentUid));
                _publishedGroups.Add(testNode.Uid, publication);
            }

            return publication;
        }
    }
}
