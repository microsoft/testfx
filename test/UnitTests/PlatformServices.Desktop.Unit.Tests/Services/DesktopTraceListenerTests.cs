// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using System;
    using System.IO;
    using System.Text;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestUtilities;

    [TestClass]
    public class DesktopTraceListenerTests
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

            //Tring to write after closing textWriter should throw exception
            Action ShouldThrowException = () => writer.WriteLine("Try to write something");
            ActionUtility.ActionShouldThrowExceptionOfType(ShouldThrowException, typeof(ObjectDisposedException));
        }

        [TestMethod]
        public void DisposeShouldDisposeCorrespondingTextWriter()
        {
            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            var traceListener = new TraceListenerWrapper(writer);
            traceListener.Dispose();

            //Tring to write after disposing textWriter should throw exception
            Action ShouldThrowException = () => writer.WriteLine("Try to write something");
            ActionUtility.ActionShouldThrowExceptionOfType(ShouldThrowException, typeof(ObjectDisposedException));
        }
    }
}
