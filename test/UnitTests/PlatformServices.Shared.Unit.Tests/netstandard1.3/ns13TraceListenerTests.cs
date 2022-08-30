// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using TestUtilities;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

[TestClass]
public class TraceListenerTests
{
    [TestMethod]
    public void GetWriterShouldReturnInitialisedWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        var returnedWriter = traceListener.GetWriter();
        Assert.AreEqual("DummyTrace", returnedWriter.ToString());
    }

    [TestMethod]
    public void DisposeShouldDisposeCorrespondingTextWriter()
    {
        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        traceListener.Dispose();

        // Trying to write after disposing textWriter should throw exception
        void shouldThrowException() => writer.WriteLine("Try to write something");
        ActionUtility.ActionShouldThrowExceptionOfType(shouldThrowException, typeof(ObjectDisposedException));
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

