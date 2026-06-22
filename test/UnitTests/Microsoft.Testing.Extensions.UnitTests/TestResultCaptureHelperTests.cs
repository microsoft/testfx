// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TestResultCaptureHelperTests
{
    // TestResultCaptureHelper is linked into multiple extension assemblies (HtmlReport, JUnitReport).
    // Pick one explicitly so direct type references stay unambiguous.
    private static readonly Type HelperType =
        typeof(HtmlReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.TestResultCaptureHelper", throwOnError: true)!;

    private static readonly MethodInfo TruncateMethod =
        HelperType.GetMethod("Truncate", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TestResultCaptureHelper.Truncate.");

    private static readonly MethodInfo ClassifyOutcomeMethod =
        HelperType.GetMethod("ClassifyOutcome", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TestResultCaptureHelper.ClassifyOutcome.");

    private static readonly MethodInfo GetClassAndMethodNameMethod =
        HelperType.GetMethod("GetClassAndMethodName", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TestResultCaptureHelper.GetClassAndMethodName.");

    private static readonly MethodInfo TryCaptureCoreMethod =
        HelperType.GetMethod("TryCaptureCore", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TestResultCaptureHelper.TryCaptureCore.");

    private static string? InvokeTruncate(string? value, int maxLength)
        => (string?)TruncateMethod.Invoke(null, [value, maxLength]);

    private static string InvokeClassifyOutcome(TestNodeStateProperty state)
        => (string)ClassifyOutcomeMethod.Invoke(null, [state])!;

    private static (string? ClassName, string? MethodName) InvokeGetClassAndMethodName(TestMethodIdentifierProperty? identifier)
        => ((string?, string?))GetClassAndMethodNameMethod.Invoke(null, [identifier])!;

    // TryCaptureCore returns a nullable internal record struct (TryCaptureResult?); reflection
    // boxes a present value to the struct instance and a missing value to null. Fields are read
    // back through the boxed instance's properties so the test stays decoupled from the type.
    private static object? InvokeTryCaptureCore(TestNode node, bool includeLocation = false)
        => TryCaptureCoreMethod.Invoke(null, [node, includeLocation]);

    private static object? GetMember(object instance, string propertyName)
        => instance.GetType().GetProperty(propertyName)!.GetValue(instance);

    [TestMethod]
    public void Truncate_NullValue_ReturnsNull()
        => Assert.IsNull(InvokeTruncate(null, 100));

    [TestMethod]
    public void Truncate_ValueShorterThanMax_ReturnsValueUnchanged()
        => Assert.AreEqual("abc", InvokeTruncate("abc", 100));

    [TestMethod]
    public void Truncate_ValueExactlyMax_ReturnsValueUnchanged()
        => Assert.AreEqual("abcde", InvokeTruncate("abcde", 5));

    [TestMethod]
    public void Truncate_ValueLongerThanMax_ReturnsTruncatedWithSuffix()
    {
        string? result = InvokeTruncate("abcdefghij", 4);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.StartsWith("abcd", StringComparison.Ordinal));
        Assert.Contains("[truncated, original length: 10]", result);
    }

    [TestMethod]
    public void Truncate_DoesNotSplitSurrogatePair_DropsHighSurrogate()
    {
        // U+1F600 (😀) is encoded as the surrogate pair D83D DE00.
        // Truncating at length 3 would otherwise leave a dangling high surrogate at index 2.
        // With the guard, the high surrogate at index 2 is dropped and the truncation suffix
        // starts immediately after "ab" — so the result must equal "ab" + suffix exactly.
        string value = "ab\uD83D\uDE00cd";
        string? result = InvokeTruncate(value, 3);
        Assert.AreEqual($"ab\n…[truncated, original length: {value.Length}]", result);
    }

    [TestMethod]
    public void ClassifyOutcome_Passed_ReturnsPassed()
        => Assert.AreEqual("passed", InvokeClassifyOutcome(new PassedTestNodeStateProperty()));

    [TestMethod]
    public void ClassifyOutcome_Skipped_ReturnsSkipped()
        => Assert.AreEqual("skipped", InvokeClassifyOutcome(new SkippedTestNodeStateProperty()));

    [TestMethod]
    public void ClassifyOutcome_Timeout_ReturnsTimedOut()
        => Assert.AreEqual("timedOut", InvokeClassifyOutcome(new TimeoutTestNodeStateProperty()));

    [TestMethod]
    public void ClassifyOutcome_Error_ReturnsErrored()
        => Assert.AreEqual("errored", InvokeClassifyOutcome(new ErrorTestNodeStateProperty()));

    [TestMethod]
    public void ClassifyOutcome_Failed_ReturnsFailed()
        => Assert.AreEqual("failed", InvokeClassifyOutcome(new FailedTestNodeStateProperty()));

    [TestMethod]
#pragma warning disable CS0618, MTP0001 // Cancelled* is obsolete but still in the well-known failed set.
    public void ClassifyOutcome_Cancelled_FallsThroughToFailed()
        => Assert.AreEqual("failed", InvokeClassifyOutcome(new CancelledTestNodeStateProperty()));
#pragma warning restore CS0618, MTP0001

    [TestMethod]
    public void GetClassAndMethodName_NullIdentifier_ReturnsNullPair()
        => Assert.AreEqual<(string?, string?)>((null, null), InvokeGetClassAndMethodName(null));

    [TestMethod]
    public void GetClassAndMethodName_WithNamespace_PrependsNamespace()
    {
        var identifier = new TestMethodIdentifierProperty(
            assemblyFullName: "MyAsm",
            @namespace: "My.Ns",
            typeName: "MyType",
            methodName: "MyMethod",
            methodArity: 0,
            parameterTypeFullNames: [],
            returnTypeFullName: "System.Void");

        (string? className, string? methodName) = InvokeGetClassAndMethodName(identifier);
        Assert.AreEqual("My.Ns.MyType", className);
        Assert.AreEqual("MyMethod", methodName);
    }

    [TestMethod]
    public void GetClassAndMethodName_WithoutNamespace_ReturnsBareTypeName()
    {
        var identifier = new TestMethodIdentifierProperty(
            assemblyFullName: "MyAsm",
            @namespace: string.Empty,
            typeName: "MyType",
            methodName: "MyMethod",
            methodArity: 0,
            parameterTypeFullNames: [],
            returnTypeFullName: "System.Void");

        (string? className, string? methodName) = InvokeGetClassAndMethodName(identifier);
        Assert.AreEqual("MyType", className);
        Assert.AreEqual("MyMethod", methodName);
    }

    [TestMethod]
    public void TryCaptureCore_NoState_ReturnsNull()
    {
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new() };
        Assert.IsNull(InvokeTryCaptureCore(node));
    }

    [TestMethod]
    public void TryCaptureCore_DiscoveredState_ReturnsNull()
    {
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) };
        Assert.IsNull(InvokeTryCaptureCore(node));
    }

    [TestMethod]
    public void TryCaptureCore_InProgressState_ReturnsNull()
    {
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(InProgressTestNodeStateProperty.CachedInstance) };
        Assert.IsNull(InvokeTryCaptureCore(node));
    }

    [TestMethod]
    public void TryCaptureCore_TerminalState_ReturnsCoreData()
    {
        var identifier = new TestMethodIdentifierProperty(
            assemblyFullName: "MyAsm",
            @namespace: "My.Ns",
            typeName: "MyType",
            methodName: "MyMethod",
            methodArity: 0,
            parameterTypeFullNames: [],
            returnTypeFullName: "System.Void");
        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(identifier);
        TestNode node = new() { Uid = "the-uid", DisplayName = "The display name", Properties = bag };

        object? result = InvokeTryCaptureCore(node);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<PassedTestNodeStateProperty>(GetMember(result, "State"));
        Assert.AreEqual(TimeSpan.Zero, GetMember(result, "Duration"));
        Assert.AreEqual("My.Ns.MyType", GetMember(result, "ClassName"));
        Assert.AreEqual("MyMethod", GetMember(result, "MethodName"));
    }

    [TestMethod]
    public void TryCaptureCore_FailedStateWithException_PopulatesExceptionDetails()
    {
        var exception = new InvalidOperationException("boom");
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = new(new FailedTestNodeStateProperty(exception)) };

        object? result = InvokeTryCaptureCore(node);

        Assert.IsNotNull(result);
        object? exceptionDetails = GetMember(result, "ExceptionDetails");
        Assert.IsNotNull(exceptionDetails);
        Assert.AreEqual("boom", GetMember(exceptionDetails, "ErrorMessage"));
        Assert.AreEqual(typeof(InvalidOperationException).FullName, GetMember(exceptionDetails, "ExceptionType"));
    }

    [TestMethod]
    public void TryCaptureCore_IncludeLocationTrue_PopulatesLocation()
    {
        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new TestFileLocationProperty("Some.cs", new LinePositionSpan(new LinePosition(1, 0), new LinePosition(2, 0))));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        object? result = InvokeTryCaptureCore(node, includeLocation: true);

        Assert.IsNotNull(result);
        object? properties = GetMember(result, "Properties");
        Assert.IsNotNull(properties);
        object? location = GetMember(properties, "Location");
        Assert.IsNotNull(location);
        Assert.AreEqual("Some.cs", GetMember(location, "FilePath"));
    }

    [TestMethod]
    public void TryCaptureCore_IncludeLocationFalse_DoesNotPopulateLocation()
    {
        var bag = new PropertyBag(PassedTestNodeStateProperty.CachedInstance);
        bag.Add(new TestFileLocationProperty("Some.cs", new LinePositionSpan(new LinePosition(1, 0), new LinePosition(2, 0))));
        TestNode node = new() { Uid = "id", DisplayName = "T", Properties = bag };

        object? result = InvokeTryCaptureCore(node, includeLocation: false);

        Assert.IsNotNull(result);
        object? properties = GetMember(result, "Properties");
        Assert.IsNotNull(properties);
        Assert.IsNull(GetMember(properties, "Location"));
    }
}
