// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A Microsoft.Testing.Platform-native <see cref="IUnitTestElementSink"/> that publishes discovered tests directly
/// as <see cref="TestNodeUpdateMessage"/>s on the platform message bus, without materializing a VSTest
/// <c>TestCase</c> or going through the VSTest bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MtpUnitTestElementSink : IUnitTestElementSink
{
    private readonly IMessageBus _messageBus;
    private readonly IDataProducer _dataProducer;
    private readonly SessionUid _sessionUid;
    private readonly bool _isTrxEnabled;

    public MtpUnitTestElementSink(IMessageBus messageBus, IDataProducer dataProducer, SessionUid sessionUid, bool isTrxEnabled)
    {
        _messageBus = messageBus;
        _dataProducer = dataProducer;
        _sessionUid = sessionUid;
        _isTrxEnabled = isTrxEnabled;
    }

    public void SendTestElement(UnitTestElement testElement)
    {
        TestNode testNode = MSTestTestNodeConverter.ToDiscoveredTestNode(testElement, _isTrxEnabled);
        _messageBus.PublishAsync(_dataProducer, new TestNodeUpdateMessage(_sessionUid, testNode)).GetAwaiter().GetResult();
    }
}
#endif
