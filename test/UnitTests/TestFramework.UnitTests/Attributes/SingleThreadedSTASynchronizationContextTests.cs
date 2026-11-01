// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public sealed class SingleThreadedSTASynchronizationContextTests : TestContainer
{
    public void PostAndSend_UseOneStaThreadPreserveStateAndAllowReentrantSend()
    {
#if !NETFRAMEWORK
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Action create = () => _ = new SingleThreadedSTASynchronizationContext();
            create.Should().ThrowExactly<NotSupportedException>()
                .WithMessage("SingleThreadedSTASynchronizationContext is only supported on Windows.");
            return;
        }
#endif

        int callerThread = Environment.CurrentManagedThreadId;
        object postState = new();
        object sendState = new();
        object nestedState = new();
        var original = new InvalidOperationException("reentrant callback contract");
        Exception? caught = null;
        var observations = new List<(string Stage, object? State, int Thread, ApartmentState Apartment, bool Background, SynchronizationContext? Context)>();
        var context = new SingleThreadedSTASynchronizationContext();
        try
        {
            context.Post(state => Record("post", state), postState);
            context.Send(
                state =>
                {
                    Record("send", state);
                    context.Send(nested => Record("nested", nested), nestedState);
                    try
                    {
                        context.Send(_ => throw original, null);
                    }
                    catch (Exception error)
                    {
                        caught = error;
                    }
                }, sendState);

            // Send's completion signal is the barrier: all three observations are published
            // before this thread reads them; no sleep, scheduling race or elapsed-time oracle.
            observations.Select(item => item.Stage).Should().Equal("post", "send", "nested");
            observations.Select(item => item.State).Should().Equal(postState, sendState, nestedState);
            observations.Select(item => item.Thread).Distinct().Should().ContainSingle()
                .Which.Should().NotBe(callerThread);
            observations.Select(item => item.Apartment).Should().OnlyContain(value => value == ApartmentState.STA);
            observations.Select(item => item.Background).Should().OnlyContain(value => value);
            observations.Select(item => item.Context).Should().OnlyContain(value => ReferenceEquals(value, context));
            caught.Should().BeSameAs(original);

            context.Complete();
            Action postAfterComplete = () => context.Post(_ => { }, null);
            Action sendAfterComplete = () => context.Send(_ => { }, null);
            postAfterComplete.Should().ThrowExactly<InvalidOperationException>();
            sendAfterComplete.Should().ThrowExactly<InvalidOperationException>();
        }
        finally
        {
            context.Complete();
            context.Dispose();
        }

        Action postAfterDispose = () => context.Post(_ => { }, null);
        postAfterDispose.Should().ThrowExactly<ObjectDisposedException>();

        void Record(string stage, object? state)
            => observations.Add((stage, state, Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState(),
                Thread.CurrentThread.IsBackground, SynchronizationContext.Current));
    }
}
