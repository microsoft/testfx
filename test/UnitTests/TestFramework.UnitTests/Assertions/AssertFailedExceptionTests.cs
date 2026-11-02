// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class AssertFailedExceptionTests : TestContainer
{
    public void ExpectedText_DefaultsToNull()
    {
        var exception = new AssertFailedException("test message");

        exception.ExpectedText.Should().BeNull();
    }

    public void ActualText_DefaultsToNull()
    {
        var exception = new AssertFailedException("test message");

        exception.ActualText.Should().BeNull();
    }

    public void ExpectedAndActualText_CanBeSet()
    {
        var exception = new AssertFailedException("test message")
        {
            ExpectedText = "42",
            ActualText = "37",
        };

        exception.ExpectedText.Should().Be("42");
        exception.ActualText.Should().Be("37");
    }

    public void ThrowAssertFailed_WithStructuredMessage_PopulatesExpectedAndActualTextAndDataEntries()
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithEvidence(evidence);
        message.WithExpectedAndActual("42", "37");

        AssertFailedException? caught = null;
        try
        {
            Assert.ThrowAssertFailed(message);
        }
        catch (AssertFailedException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.ExpectedText.Should().Be("42");
        caught.ActualText.Should().Be("37");
        caught.Data["assert.expected"].Should().Be("42");
        caught.Data["assert.actual"].Should().Be("37");
    }

    public void ThrowAssertFailed_WithStructuredMessage_NullExpectedAndActual_DoesNotPopulateDataEntries()
    {
        StructuredAssertionMessage message = new("Condition failed.");

        AssertFailedException? caught = null;
        try
        {
            Assert.ThrowAssertFailed(message);
        }
        catch (AssertFailedException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.ExpectedText.Should().BeNull();
        caught.ActualText.Should().BeNull();
        caught.Data.Contains("assert.expected").Should().BeFalse();
        caught.Data.Contains("assert.actual").Should().BeFalse();
    }

    public void ThrowAssertFailed_StringMessage_RemainsHardWithinScope()
    {
        bool continued = false;
        Action action = () =>
        {
            using (Assert.Scope())
            {
                Assert.ThrowAssertFailed("Assert.Custom", "hard failure");
                continued = true;
            }
        };
        AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Be("Assert.Custom failed. hard failure");
        failure.ExpectedText.Should().BeNull();
        failure.ActualText.Should().BeNull();
        failure.Data.Contains("assert.expected").Should().BeFalse();
        failure.Data.Contains("assert.actual").Should().BeFalse();
        continued.Should().BeFalse();
    }

    public void FrameworkExceptions_ConstructorsPreserveMessageCauseAndCategory()
    {
        var cause = new InvalidOperationException("inner contract");
        (Exception Error, string Message, Exception? Cause, bool IsAssertion)[] cases =
        [
            (new AssertFailedException(), "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException' was thrown.", null, true),
            (new AssertInconclusiveException(), "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.", null, true),
            (new AssertInconclusiveException("inconclusive contract", cause), "inconclusive contract", cause, true),
            (new MSTestException(), "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.MSTestException' was thrown.", null, false),
            (new MSTestException("framework contract"), "framework contract", null, false),
            (new MSTestException("framework contract", cause), "framework contract", cause, false),
        ];
        foreach ((Exception error, string message, Exception? inner, bool isAssertion) in cases)
        {
            Action action = () => throw error;
            Exception caught = action.Should().Throw<Exception>().Which;
            caught.Should().BeSameAs(error);
            caught.Message.Should().Be(message);
            caught.InnerException.Should().BeSameAs(inner);
            (caught is UnitTestAssertException).Should().Be(isAssertion);
        }
    }

#pragma warning disable SYSLIB0050, SYSLIB0051 // Explicitly test legacy constructor payloads, without invoking any formatter or serializer.
    public void FrameworkExceptions_LegacySerializationConstructors_PreserveExplicitPayload()
    {
        var cause = new InvalidOperationException("serialized inner contract");
        Func<System.Runtime.Serialization.SerializationInfo, Exception>[] constructors =
        [
            info => new DeserializedAssertFailedException(info),
            info => new DeserializedAssertInconclusiveException(info),
            info => new MSTestException(info, default),
        ];
        foreach (Func<System.Runtime.Serialization.SerializationInfo, Exception> construct in constructors)
        {
            var info = new System.Runtime.Serialization.SerializationInfo(typeof(Exception), new System.Runtime.Serialization.FormatterConverter());
            info.AddValue("ClassName", "Explicit.Legacy.Exception");
            info.AddValue("Message", "serialized contract");
            info.AddValue("Data", new System.Collections.Hashtable { ["contract"] = 37 }, typeof(System.Collections.IDictionary));
            info.AddValue("InnerException", cause, typeof(Exception));
            info.AddValue("HelpURL", "urn:testfx:legacy-contract");
            info.AddValue("StackTraceString", "task-owned stack");
            info.AddValue("RemoteStackTraceString", null, typeof(string));
            info.AddValue("RemoteStackIndex", 0);
            info.AddValue("ExceptionMethod", null, typeof(string));
            info.AddValue("HResult", -2146233088);
            info.AddValue("Source", "legacy source");
            info.AddValue("WatsonBuckets", null, typeof(byte[]));

            Exception exception = construct(info);
            exception.Message.Should().Be("serialized contract");
            exception.InnerException.Should().BeSameAs(cause);
            exception.Data["contract"].Should().Be(37);
            exception.HelpLink.Should().Be("urn:testfx:legacy-contract");
            exception.HResult.Should().Be(-2146233088);
            exception.Source.Should().Be("legacy source");
            exception.StackTrace.Should().Be("task-owned stack");
        }
    }

    [Serializable]
    private sealed class DeserializedAssertFailedException(System.Runtime.Serialization.SerializationInfo info)
        : AssertFailedException(info, default);

    [Serializable]
    private sealed class DeserializedAssertInconclusiveException(System.Runtime.Serialization.SerializationInfo info)
        : AssertInconclusiveException(info, default);
#pragma warning restore SYSLIB0050, SYSLIB0051
}
