// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.UnitTests.Services
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using TestUtilities;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    [TestClass]
    public class TraceListenerManagerTests
    {
        [TestMethod]
        public void AddShouldAddTraceListenerToListOfTraceListeners()
        {
            var stringWriter = new StringWriter();
            var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);
            var traceListener = new TraceListenerWrapper(stringWriter);
            var originalCount = Trace.Listeners.Count;

            traceListenerManager.Add(traceListener);
            var newCount = Trace.Listeners.Count;

            Assert.AreEqual(originalCount + 1, newCount);
            Assert.IsTrue(Trace.Listeners.Contains(traceListener));
        }

        [TestMethod]
        public void RemoveShouldRemoveTraceListenerFromListOfTraceListeners()
        {
            var stringWriter = new StringWriter();
            var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);
            var traceListener = new TraceListenerWrapper(stringWriter);
            var originalCount = Trace.Listeners.Count;

            traceListenerManager.Add(traceListener);
            var countAfterAdding = Trace.Listeners.Count;

            traceListenerManager.Remove(traceListener);
            var countAfterRemoving = Trace.Listeners.Count;

            Assert.AreEqual(originalCount + 1, countAfterAdding);
            Assert.AreEqual(countAfterAdding - 1, countAfterRemoving);
            Assert.IsFalse(Trace.Listeners.Contains(traceListener));
        }

        [TestMethod]
        public void DisposeShouldCallDisposeOnCorrespondingTraceListener()
        {
            var stringWriter = new StringWriter();
            var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);

            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            var traceListener = new TraceListenerWrapper(writer);
            traceListenerManager.Add(traceListener);
            traceListenerManager.Dispose(traceListener);

            // Trying to write after closing textWriter should throw exception
            Action shouldThrowException = () => writer.WriteLine("Try to write something");
            ActionUtility.ActionShouldThrowExceptionOfType(shouldThrowException, typeof(ObjectDisposedException));
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
