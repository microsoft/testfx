// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WIN_UI

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class TraceListenerTests : TestContainer
{
    public void GetWriterShouldReturnInitializedWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        TextWriter? returnedWriter = traceListener.GetWriter();
        (returnedWriter?.ToString() == "DummyTrace").Should().BeTrue();
    }

    public void DisposeShouldDisposeCorrespondingTextWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        traceListener.Dispose();

        // Trying to write after disposing textWriter should throw exception
        Action shouldThrowException = () => writer.WriteLine("Try to write something");
        shouldThrowException.Should().Throw<ObjectDisposedException>();
    }
}

#endif
