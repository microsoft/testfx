// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

public class TraceListenerTests : TestContainer
{
#if !WIN_UI
    public void GetWriterShouldReturnInitializedWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        var returnedWriter = traceListener.GetWriter();
        Verify("DummyTrace" == returnedWriter.ToString());
    }

    public void DisposeShouldDisposeCorrespondingTextWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        traceListener.Dispose();

        // Trying to write after disposing textWriter should throw exception
        void shouldThrowException() => writer.WriteLine("Try to write something");
        var ex = VerifyThrows(shouldThrowException);
        Verify(ex is ObjectDisposedException);
    }
#endif
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

