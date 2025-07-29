// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WIN_UI

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

public class TraceListenerTests : TestContainer
{
    public void GetWriterShouldReturnInitializedWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        TextWriter? returnedWriter = traceListener.GetWriter();
        returnedWriter?.ToString().Should().Be("DummyTrace");
    }

    public void DisposeShouldDisposeCorrespondingTextWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        traceListener.Dispose();

        // Trying to write after disposing textWriter should throw exception
        void ShouldThrowException() => writer.WriteLine("Try to write something");
        ShouldThrowException.Should().Throw<ObjectDisposedException>();
    }
}

#endif
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

