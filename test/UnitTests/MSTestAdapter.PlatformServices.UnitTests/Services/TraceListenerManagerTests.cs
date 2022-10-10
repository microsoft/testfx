﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

public class TraceListenerManagerTests : TestContainer
{
#if !WIN_UI
    public void AddShouldAddTraceListenerToListOfTraceListeners()
    {
        var stringWriter = new StringWriter();
        var traceListenerManager = new TraceListenerManager(stringWriter, stringWriter);
        var traceListener = new TraceListenerWrapper(stringWriter);
        var originalCount = Trace.Listeners.Count;

        traceListenerManager.Add(traceListener);
        var newCount = Trace.Listeners.Count;

        Verify(originalCount + 1 == newCount);
        Verify(Trace.Listeners.Contains(traceListener));
    }

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
        var ex = VerifyThrows(ShouldThrowException);
        Verify(ex is ObjectDisposedException);
    }
#endif
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

