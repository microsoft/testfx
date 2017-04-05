// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;

    using System;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using TestUtilities;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class TraceListenerTests
    {
        [TestMethod]
        public void GetWriterShouldReturnInitialisedWriter()
        {
            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            var traceListener = new TraceListenerWrapper(writer);
            var returnedWriter = traceListener.GetWriter();
            Assert.AreEqual(returnedWriter.ToString(), "DummyTrace");
        }

        [TestMethod]
        public void CloseShouldCloseCorrespondingTextWriter()
        {
            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            var traceListener = new TraceListenerWrapper(writer);
            traceListener.Close();

            // Tring to write after closing textWriter should throw exception
            Action shouldThrowException = () => writer.WriteLine("Try to write something");
            ActionUtility.ActionShouldThrowExceptionOfType(shouldThrowException, typeof(ObjectDisposedException));
        }

        [TestMethod]
        public void DisposeShouldDisposeCorrespondingTextWriter()
        {
            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            var traceListener = new TraceListenerWrapper(writer);
            traceListener.Dispose();

            // Tring to write after disposing textWriter should throw exception
            Action shouldThrowException = () => writer.WriteLine("Try to write something");
            ActionUtility.ActionShouldThrowExceptionOfType(shouldThrowException, typeof(ObjectDisposedException));
        }
    }
}
