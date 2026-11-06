// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerCompatibilityTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerIndex")]
    [DataRow(true, DisplayName = "CodeFixIndex")]
    public void Index_OffsetsEqualityAndFormatting_PreserveOrigin(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "System.Index");
        object start = ProductReflection.Create(type, [typeof(int), typeof(bool)], 3, false);
        object end = ProductReflection.Create(type, [typeof(int), typeof(bool)], 3, true);
        Assert.AreEqual(3, ProductReflection.Get<int>(start, "Value"));
        Assert.AreEqual(3, ProductReflection.Get<int>(end, "Value"));
        Assert.IsFalse(ProductReflection.Get<bool>(start, "IsFromEnd"));
        Assert.IsTrue(ProductReflection.Get<bool>(end, "IsFromEnd"));
        Assert.AreEqual(3, ProductReflection.Call(type, start, "GetOffset", [typeof(int)], 11));
        Assert.AreEqual(8, ProductReflection.Call(type, end, "GetOffset", [typeof(int)], 11));
        Assert.AreEqual(-1, ProductReflection.Call(type, end, "GetOffset", [typeof(int)], 2));
        Assert.AreEqual("3", start.ToString());
        Assert.AreEqual("^3", end.ToString());
        Assert.AreEqual(3, start.GetHashCode());
        Assert.AreEqual(~3, end.GetHashCode());
        Assert.IsFalse(start.Equals(end));
        Assert.IsFalse(start.Equals(3));
        Assert.IsFalse(start.Equals(null));
        object same = ProductReflection.Call(type, null, "FromStart", [typeof(int)], 3)!;
        Assert.IsTrue((bool)ProductReflection.Call(type, start, "Equals", [type], same)!);
        Assert.IsFalse((bool)ProductReflection.Call(type, start, "Equals", [type], end)!);
        Assert.IsTrue(start.Equals(same));
        Assert.AreEqual("^0", type.GetProperty("End")!.GetValue(null)!.ToString());
        Assert.AreEqual("0", type.GetProperty("Start")!.GetValue(null)!.ToString());
        Assert.AreEqual("^2", ProductReflection.Call(type, null, "FromEnd", [typeof(int)], 2)!.ToString());
        Assert.AreEqual("^0", ProductReflection.Call(type, null, "FromEnd", [typeof(int)], 0)!.ToString());
        Assert.AreEqual("0", ProductReflection.Call(type, null, "FromStart", [typeof(int)], 0)!.ToString());
        Assert.AreEqual("7", ProductReflection.Call(type, null, "op_Implicit", [typeof(int)], 7)!.ToString());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerNegativeIndex")]
    [DataRow(true, DisplayName = "CodeFixNegativeIndex")]
    public void Index_NegativeValues_RejectConstructionAndFactories(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "System.Index");
        foreach (int value in new[] { -1, -2, int.MinValue })
        {
            Assert.AreEqual("value", Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => ProductReflection.Create(type, [typeof(int), typeof(bool)], value, false)).ParamName);
            foreach (string factory in new[] { "FromStart", "FromEnd", "op_Implicit" })
            {
                Assert.AreEqual("value", Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                    () => ProductReflection.Call(type, null, factory, [typeof(int)], value)).ParamName);
            }
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerRange")]
    [DataRow(true, DisplayName = "CodeFixRange")]
    public void Range_MixedOrigins_ResolvesExactBoundsAndRejectsReversedRanges(bool codeFix)
    {
        Type index = ProductReflection.Type(codeFix, "System.Index");
        Type range = ProductReflection.Type(codeFix, "System.Range");
        object Index(int value, bool fromEnd = false) => ProductReflection.Create(index, [typeof(int), typeof(bool)], value, fromEnd);
        object Range(object start, object end) => ProductReflection.Create(range, [index, index], start, end);
        object middle = Range(Index(2), Index(1, true));
        Assert.AreEqual((2, 7), ProductReflection.Call(range, middle, "GetOffsetAndLength", [typeof(int)], 10));
        Assert.AreEqual("2..^1", middle.ToString());
        Assert.AreEqual(-68, middle.GetHashCode());
        Assert.IsTrue(middle.Equals(Range(Index(2), Index(1, true))));
        Assert.IsTrue((bool)ProductReflection.Call(range, middle, "Equals", [range], Range(Index(2), Index(1, true)))!);
        Assert.IsFalse((bool)ProductReflection.Call(range, middle, "Equals", [range], Range(Index(3), Index(1, true)))!);
        Assert.IsFalse(middle.Equals(Range(Index(3), Index(1, true))));
        Assert.IsFalse(middle.Equals(Range(Index(2), Index(0, true))));
        Assert.IsFalse(middle.Equals(null));
        Assert.IsFalse(middle.Equals("2..^1"));
        Assert.AreEqual((0, 0), ProductReflection.Call(range, range.GetProperty("All")!.GetValue(null), "GetOffsetAndLength", [typeof(int)], 0));
        Assert.AreEqual((0, 10), ProductReflection.Call(range, range.GetProperty("All")!.GetValue(null), "GetOffsetAndLength", [typeof(int)], 10));
        Assert.AreEqual((2, 8), ProductReflection.Call(range, ProductReflection.Call(range, null, "StartAt", [index], Index(2)), "GetOffsetAndLength", [typeof(int)], 10));
        Assert.AreEqual((0, 8), ProductReflection.Call(range, ProductReflection.Call(range, null, "EndAt", [index], Index(2, true)), "GetOffsetAndLength", [typeof(int)], 10));
        foreach (object invalid in new[] { Range(Index(2), Index(1)), Range(Index(0), Index(11)), Range(Index(0), Index(12)), Range(Index(11, true), Index(0, true)), Range(Index(int.MaxValue), Index(0, true)) })
        {
            Assert.AreEqual("length", Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => ProductReflection.Call(range, invalid, "GetOffsetAndLength", [typeof(int)], 10)).ParamName);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerEnsure")]
    [DataRow(true, DisplayName = "CodeFixEnsure")]
    public void Ensure_EmptyAndWhitespaceInputs_PreserveArgumentIdentity(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "Polyfills.Ensure");
        Assert.AreEqual("input", Assert.ThrowsExactly<ArgumentNullException>(
            () => ProductReflection.Call(type, null, "NotNullOrWhiteSpace", [typeof(string), typeof(string)], null, "input")).ParamName);
        foreach (string value in new[] { string.Empty, " \t" })
        {
            ArgumentException error = Assert.ThrowsExactly<ArgumentException>(
                () => ProductReflection.Call(type, null, "NotNullOrWhiteSpace", [typeof(string), typeof(string)], value, "input"));
            Assert.AreEqual("input", error.ParamName);
            StringAssert.StartsWith(error.Message, "Argument cannot be whitespace.");
        }

        Assert.AreEqual(" value ", ProductReflection.Call(type, null, "NotNullOrWhiteSpace", [typeof(string), typeof(string)], " value ", "input"));
        Assert.AreEqual("text", Assert.ThrowsExactly<ArgumentException>(
            () => ProductReflection.Call(type, null, "NotEmpty", [typeof(string), typeof(string)], string.Empty, "text")).ParamName);
        ProductReflection.Call(type, null, "NotEmpty", [typeof(string), typeof(string)], null, "text");
        ProductReflection.Call(type, null, "NotEmpty", [typeof(string), typeof(string)], " ", "text");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerCollections")]
    [DataRow(true, DisplayName = "CodeFixCollections")]
    public void Ensure_CollectionsAndLazySequences_DistinguishEmptyFromNull(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "Polyfills.Ensure");
        MethodInfo method = type.GetMethods().Single(candidate => candidate.Name == "NotEmpty" && candidate.IsGenericMethod).MakeGenericMethod(typeof(int));
        foreach (IEnumerable<int> empty in new IEnumerable<int>[] { [], new NonGenericCollection([]), new ReadOnlyValues([]), YieldValues(0) })
        {
            ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => ProductReflection.Invoke(method, null, empty, "numbers"));
            Assert.AreEqual("numbers", error.ParamName);
            StringAssert.StartsWith(error.Message, "Argument cannot be empty.");
        }

        foreach (IEnumerable<int>? value in new IEnumerable<int>?[] { null, [17], new NonGenericCollection([17]), new ReadOnlyValues([17]), YieldValues(1) })
        {
            ProductReflection.Invoke(method, null, value, "numbers");
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerMetadata")]
    [DataRow(true, DisplayName = "CodeFixMetadata")]
    public void CompatibilityAttributes_Constructors_PreserveCompilerContracts(bool codeFix)
    {
        AssertAttribute(codeFix, "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute", ["expression"], "ParameterName", "expression");
        AssertAttribute(codeFix, "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute", ["RequiredMembers"], "FeatureName", "RequiredMembers",
            ("IsOptional", true));
        AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis.ExperimentalAttribute", ["EXP001"], "DiagnosticId", "EXP001",
            ("UrlFormat", "https://example.invalid/{0}"));
        AssertAttribute(codeFix, "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute", [-7], "Priority", -7);
        foreach (string name in new[] { "MaybeNullWhenAttribute", "NotNullWhenAttribute", "DoesNotReturnIfAttribute" })
        {
            AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis." + name, [false], name == "DoesNotReturnIfAttribute" ? "ParameterValue" : "ReturnValue", false);
            AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis." + name, [true], name == "DoesNotReturnIfAttribute" ? "ParameterValue" : "ReturnValue", true);
        }

        AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute", ["other"], "ParameterName", "other");
        foreach (string name in new[] { "MemberNotNullAttribute", "InterpolatedStringHandlerArgumentAttribute" })
        {
            string fullName = name == "MemberNotNullAttribute" ? "System.Diagnostics.CodeAnalysis." + name : "System.Runtime.CompilerServices." + name;
            string property = name == "MemberNotNullAttribute" ? "Members" : "Arguments";
            AssertAttribute(codeFix, fullName, ["first"], property, new[] { "first" });
            AssertAttribute(codeFix, fullName, [new[] { "first", "second" }], property, new[] { "first", "second" });
        }

        AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute", [true, "first"], "Members", new[] { "first" });
        AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute", [false, new[] { "first", "second" }], "ReturnValue", false);
        AssertAttribute(codeFix, "System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute", [], expectedProperty: null, expectedValue: null);
        Type memberTypes = ProductReflection.Type(codeFix, "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes");
        AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute", [Enum.ToObject(memberTypes, 3)], "MemberTypes", Enum.ToObject(memberTypes, 3));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerPlatformMetadata")]
    [DataRow(true, DisplayName = "CodeFixPlatformMetadata")]
    public void PlatformAndSuppressionAttributes_Constructors_PreserveOptionalMessages(bool codeFix)
    {
        foreach (string name in new[] { "TargetPlatformAttribute", "SupportedOSPlatformAttribute", "UnsupportedOSPlatformAttribute", "ObsoletedOSPlatformAttribute", "SupportedOSPlatformGuardAttribute", "UnsupportedOSPlatformGuardAttribute" })
        {
            AssertAttribute(codeFix, "System.Runtime.Versioning." + name, ["windows10.0"], "PlatformName", "windows10.0");
        }

        AssertAttribute(codeFix, "System.Runtime.Versioning.UnsupportedOSPlatformAttribute", ["linux", "unsupported"], "Message", "unsupported");
        AssertAttribute(codeFix, "System.Runtime.Versioning.ObsoletedOSPlatformAttribute", ["linux", "use replacement"], "Message", "use replacement", ("Url", "https://example.invalid/platform"));
        AssertAttribute(codeFix, "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute", ["Usage", "CA0000"], "Category", "Usage",
            ("Scope", "member"), ("Target", "~M:Example.Check"), ("MessageId", "argument"), ("Justification", "verified contract"));
        object suppression = ProductReflection.Create(
            ProductReflection.Type(codeFix, "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute"),
            [typeof(string), typeof(string)], "Usage", "CA0000");
        Assert.AreEqual("CA0000", ProductReflection.Get<string>(suppression, "CheckId"));
        Assert.IsNull(ProductReflection.Get<string?>(suppression, "Justification"));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerBackwardCompatibility")]
    [DataRow(true, DisplayName = "CodeFixBackwardCompatibility")]
    public async Task BackwardCompatibility_TextStreamsAndXml_KeepLegacySemantics(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "Polyfills.Polyfill");
        Assert.IsTrue((bool)ProductReflection.Call(type, null, "Contains", [typeof(string), typeof(char)], "cart", 'a')!);
        Assert.IsFalse((bool)ProductReflection.Call(type, null, "Contains", [typeof(string), typeof(char)], "cart", 'z')!);
        Assert.IsTrue((bool)ProductReflection.Call(type, null, "Contains", [typeof(string), typeof(string), typeof(StringComparison)], "Cart", "AR", StringComparison.OrdinalIgnoreCase)!);
        Assert.IsFalse((bool)ProductReflection.Call(type, null, "Contains", [typeof(string), typeof(string), typeof(StringComparison)], "Cart", "AR", StringComparison.Ordinal)!);
        var builder = new StringBuilder("prefix:");
        ProductReflection.Call(type, null, "AppendJoin", [typeof(StringBuilder), typeof(string), typeof(string[])], builder, "|", new[] { "one", "two" });
        MethodInfo appendGeneric = type.GetMethods().Single(method => method.Name == "AppendJoin" && method.IsGenericMethod).MakeGenericMethod(typeof(int));
        ProductReflection.Invoke(appendGeneric, null, builder, "/", new[] { 17, 29 });
        Assert.AreEqual("prefix:one|two17/29", builder.ToString());
        MethodInfo deconstruct = type.GetMethods().Single(method => method.Name == "Deconstruct").MakeGenericMethod(typeof(string), typeof(int));
        object?[] arguments = [new KeyValuePair<string, int>("answer", 42), null, null];
        ProductReflection.Invoke(deconstruct, null, arguments);
        Assert.AreEqual("answer", arguments[1]);
        Assert.AreEqual(42, arguments[2]);
        using var source = new MemoryStream([1, 3, 5, 7]);
        using var destination = new MemoryStream();
        await (Task)ProductReflection.Call(type, null, "CopyToAsync", [typeof(Stream), typeof(Stream), typeof(CancellationToken)], source, destination, CancellationToken.None)!;
        CollectionAssert.AreEqual(new byte[] { 1, 3, 5, 7 }, destination.ToArray());
        using var xmlStream = new MemoryStream();
        var document = new XDocument(new XElement("root", new XAttribute("value", "a&b")));
        await (Task)ProductReflection.Call(type, null, "SaveAsync", [typeof(XDocument), typeof(Stream), typeof(SaveOptions), typeof(CancellationToken)], document, xmlStream, SaveOptions.DisableFormatting, CancellationToken.None)!;
        xmlStream.Position = 0;
        Assert.AreEqual("<root value=\"a&amp;b\" />", XDocument.Load(xmlStream).ToString(SaveOptions.DisableFormatting));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var untouched = new MemoryStream();
        Assert.ThrowsExactly<OperationCanceledException>(() => ProductReflection.Call(type, null, "SaveAsync",
            [typeof(XDocument), typeof(Stream), typeof(SaveOptions), typeof(CancellationToken)], document, untouched, SaveOptions.None, canceled.Token));
        Assert.AreEqual(0L, untouched.Length);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerUnreachableException")]
    [DataRow(true, DisplayName = "CodeFixUnreachableException")]
    public void UnreachableException_Constructors_PreserveDefaultAndInnerException(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "System.Diagnostics.UnreachableException");
        var defaultException = (Exception)ProductReflection.Create(type, []);
        Assert.AreEqual("The program executed an instruction that was thought to be unreachable.", defaultException.Message);
        var custom = (Exception)ProductReflection.Create(type, [typeof(string)], "unreachable branch");
        Assert.AreEqual("unreachable branch", custom.Message);
        var inner = new InvalidOperationException("cause");
        var wrapped = (Exception)ProductReflection.Create(type, [typeof(string), typeof(Exception)], "context", inner);
        Assert.AreEqual("context", wrapped.Message);
        Assert.AreSame(inner, wrapped.InnerException);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerPlatformChecks")]
    [DataRow(true, DisplayName = "CodeFixPlatformChecks")]
    public void PlatformChecks_MatchRuntimePlatformIdentifiers(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "Polyfills.Polyfill");
        foreach ((string method, string platform) in new[] { ("IsWasi", "WASI"), ("IsAndroid", "ANDROID"), ("IsIOS", "IOS"), ("IsTvOS", "TVOS"), ("IsBrowser", "BROWSER") })
        {
            Assert.AreEqual(RuntimeInformation.IsOSPlatform(OSPlatform.Create(platform)), ProductReflection.Call(type, null, method, []));
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AnalyzerWaitCancellation")]
    [DataRow(true, DisplayName = "CodeFixWaitCancellation")]
    public async Task WaitForExit_CurrentProcessCancellation_CompletesAndReleasesItsWait(bool codeFix)
    {
        Type type = ProductReflection.Type(codeFix, "Polyfills.Polyfill");
        using var process = Process.GetCurrentProcess();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var alreadyCanceled = (Task)ProductReflection.Call(type, null, "WaitForExitAsync",
            [typeof(Process), typeof(CancellationToken)], process, canceled.Token)!;
        OperationCanceledException immediate = await Assert.ThrowsAsync<OperationCanceledException>(() => alreadyCanceled);
        Assert.AreEqual(canceled.Token, immediate.CancellationToken);

        using var cancellation = new CancellationTokenSource();
        var waiting = (Task)ProductReflection.Call(type, null, "WaitForExitAsync",
            [typeof(Process), typeof(CancellationToken)], process, cancellation.Token)!;
        try
        {
            // This process cannot exit while the test is executing; no sleep or timing assumption is involved.
            Assert.IsFalse(waiting.IsCompleted);
            cancellation.Cancel();
            OperationCanceledException deferred = await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
            Assert.AreEqual(cancellation.Token, deferred.CancellationToken);
            Assert.IsTrue(waiting.IsCanceled);
            Assert.IsFalse(process.HasExited);
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await waiting;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
        }

        using var unstarted = new Process();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => (Task)ProductReflection.Call(type, null, "WaitForExitAsync",
            [typeof(Process), typeof(CancellationToken)], unstarted, CancellationToken.None)!);
    }

    private static void AssertAttribute(bool codeFix, string name, object[] arguments, string? expectedProperty, object? expectedValue, params (string Name, object Value)[] namedProperties)
    {
        Type attributeType = ProductReflection.Type(codeFix, name);
        ConstructorInfo constructor = attributeType.GetConstructor(arguments.Select(argument => argument.GetType()).ToArray())!;
        object attribute = ProductReflection.Invoke(constructor, null, arguments)!;
        foreach ((string property, object value) in namedProperties)
        {
            attributeType.GetProperty(property)!.SetValue(attribute, value);
        }

        if (expectedProperty is not null)
        {
            object? actual = attributeType.GetProperty(expectedProperty)!.GetValue(attribute);
            if (expectedValue is string[] strings)
            {
                CollectionAssert.AreEqual(strings, (string[])actual!);
            }
            else
            {
                Assert.AreEqual(expectedValue, actual);
            }
        }
        else
        {
            Assert.AreEqual(name, attribute.GetType().FullName);
        }

        foreach ((string property, object value) in namedProperties)
        {
            Assert.AreEqual(value, attributeType.GetProperty(property)!.GetValue(attribute));
        }
    }

    private static IEnumerable<int> YieldValues(int count)
    {
        for (int index = 0; index < count; index++)
        {
            yield return 17;
        }
    }

    private sealed class NonGenericCollection(int[] values) : ICollection, IEnumerable<int>
    {
        public int Count => values.Length;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public void CopyTo(Array array, int index) => values.CopyTo(array, index);

        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReadOnlyValues(int[] values) : IReadOnlyCollection<int>
    {
        public int Count => values.Length;

        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
