// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WIN_UI
using System.Diagnostics;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

public class TraceListenerManagerTests : TestContainer
{
    public void AddShouldAddTraceListenerToListOfTraceListeners()
    {
        var stringWriter = new StringWriter();
        var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);
        var traceListener = new TraceListenerWrapper(stringWriter);
        int originalCount = Trace.Listeners.Count;

        traceListenerManager.Add(traceListener);
        int newCount = Trace.Listeners.Count;

        Verify(originalCount + 1 == newCount);
        Verify(Trace.Listeners.Contains(traceListener));
    }

    public void RemoveShouldRemoveTraceListenerFromListOfTraceListeners()
    {
        var stringWriter = new StringWriter();
        var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);
        var traceListener = new TraceListenerWrapper(stringWriter);
        int originalCount = Trace.Listeners.Count;

        traceListenerManager.Add(traceListener);
        int countAfterAdding = Trace.Listeners.Count;

        traceListenerManager.Remove(traceListener);
        int countAfterRemoving = Trace.Listeners.Count;

        Verify(originalCount + 1 == countAfterAdding);
        Verify(countAfterAdding - 1 == countAfterRemoving);
        Verify(!Trace.Listeners.Contains(traceListener));
    }

    public void DisposeShouldCallDisposeOnCorrespondingTraceListener()
    {
        var stringWriter = new StringWriter();
        var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);

        StringWriter writer = new(new StringBuilder("DummyTrace"));
        var traceListener = new TraceListenerWrapper(writer);
        traceListenerManager.Add(traceListener);
        traceListenerManager.Dispose(traceListener);

        // Trying to write after closing textWriter should throw exception
        void ShouldThrowException() => writer.WriteLine("Try to write something");
        Exception ex = VerifyThrows(ShouldThrowException);
        Verify(ex is ObjectDisposedException);
    }
}

#endif
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
