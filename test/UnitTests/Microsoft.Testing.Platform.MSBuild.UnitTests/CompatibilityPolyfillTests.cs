// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class CompatibilityPolyfillTests
{
    [TestMethod]
    [DataRow(0, false, 0, 0, "0")]
    [DataRow(0, true, 0, 0, "^0")]
    [DataRow(0, true, 10, 10, "^0")]
    [DataRow(3, false, 10, 3, "3")]
    [DataRow(3, true, 10, 7, "^3")]
    [DataRow(10, true, 10, 0, "^10")]
    [DataRow(11, true, 10, -1, "^11")]
    [DataRow(11, false, 10, 11, "11")]
    public void Index_OffsetsAndFormatting_PreserveOrigin(int value, bool fromEnd, int length, int expectedOffset, string expectedText)
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("System.Index");
        object index = CreateIndex(type, value, fromEnd);
        object equal = CreateIndex(type, value, fromEnd);
        object differentValue = CreateIndex(type, value + 1, fromEnd);
        object differentOrigin = CreateIndex(type, value, !fromEnd);

        Assert.AreEqual(value, type.GetProperty("Value")!.GetValue(index));
        Assert.AreEqual(fromEnd, type.GetProperty("IsFromEnd")!.GetValue(index));
        Assert.AreEqual(expectedOffset, Invoke(type.GetMethod("GetOffset")!, index, length));
        Assert.AreEqual(expectedText, index.ToString());
        Assert.IsTrue((bool)Invoke(type.GetMethod(nameof(Equals), [type])!, index, equal)!);
        Assert.IsTrue(index.Equals(equal));
        Assert.IsFalse(index.Equals(differentValue));
        Assert.IsFalse(index.Equals(differentOrigin));
        Assert.IsFalse(index.Equals(null));
        Assert.IsFalse(index.Equals(expectedText));
        Assert.AreEqual(index.GetHashCode(), equal.GetHashCode());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void Index_NegativeValues_AreRejectedByConstructorAndFactories(int value)
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("System.Index");
        Action[] create =
        [
            () => CreateIndex(type, value, false),
            () => CreateIndex(type, value, true),
            () => Invoke(type.GetMethod("FromStart")!, null, value),
            () => Invoke(type.GetMethod("FromEnd")!, null, value),
        ];
        foreach (Action action in create)
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(action);
            Assert.AreEqual("value", exception.ParamName);
        }
    }

    [TestMethod]
    public void Index_FactoriesAndConversion_PreserveStartAndEndSemantics()
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("System.Index");
        object start = type.GetProperty("Start")!.GetValue(null)!;
        object end = type.GetProperty("End")!.GetValue(null)!;
        object fromStart = Invoke(type.GetMethod("FromStart")!, null, 3)!;
        object fromEnd = Invoke(type.GetMethod("FromEnd")!, null, 3)!;
        object converted = Invoke(type.GetMethod("op_Implicit")!, null, 3)!;

        Assert.AreEqual(0, Invoke(type.GetMethod("GetOffset")!, start, 10));
        Assert.AreEqual(10, Invoke(type.GetMethod("GetOffset")!, end, 10));
        Assert.AreEqual(3, Invoke(type.GetMethod("GetOffset")!, fromStart, 10));
        Assert.AreEqual(7, Invoke(type.GetMethod("GetOffset")!, fromEnd, 10));
        Assert.IsTrue(converted.Equals(fromStart));
        Assert.IsFalse(converted.Equals(fromEnd));
    }

    [TestMethod]
    [DataRow(0, false, 0, true, 10, 0, 10)]
    [DataRow(0, false, 0, true, 0, 0, 0)]
    [DataRow(2, false, 7, false, 10, 2, 5)]
    [DataRow(2, false, 3, true, 10, 2, 5)]
    [DataRow(3, true, 0, true, 10, 7, 3)]
    [DataRow(10, false, 0, true, 10, 10, 0)]
    public void Range_OffsetsAndLength_RespectBothBounds(int start, bool startFromEnd, int end, bool endFromEnd, int length, int expectedOffset, int expectedLength)
    {
        using CompatibilityAssembly assembly = new();
        Type indexType = assembly.GetRequiredType("System.Index");
        Type rangeType = assembly.GetRequiredType("System.Range");
        object startIndex = CreateIndex(indexType, start, startFromEnd);
        object endIndex = CreateIndex(indexType, end, endFromEnd);
        object range = Invoke(rangeType.GetConstructor([indexType, indexType])!, null, startIndex, endIndex)!;
        object equal = Invoke(rangeType.GetConstructor([indexType, indexType])!, null, CreateIndex(indexType, start, startFromEnd), CreateIndex(indexType, end, endFromEnd))!;
        object result = Invoke(rangeType.GetMethod("GetOffsetAndLength")!, range, length)!;

        Assert.AreEqual((expectedOffset, expectedLength), result);
        Assert.IsTrue((bool)Invoke(rangeType.GetMethod(nameof(Equals), [rangeType])!, range, equal)!);
        Assert.IsTrue(range.Equals(equal));
        Assert.AreEqual(range.GetHashCode(), equal.GetHashCode());
        Assert.IsFalse(range.Equals(null));
        Assert.IsFalse(range.Equals("not a range"));
    }

    [TestMethod]
    [DataRow(0, false, 11, false, 10)]
    [DataRow(8, false, 7, false, 10)]
    [DataRow(11, false, 0, true, 10)]
    [DataRow(11, true, 0, true, 10)]
    public void Range_OutOfBoundsOrReversed_ReportsLength(int start, bool startFromEnd, int end, bool endFromEnd, int length)
    {
        using CompatibilityAssembly assembly = new();
        Type indexType = assembly.GetRequiredType("System.Index");
        Type rangeType = assembly.GetRequiredType("System.Range");
        object range = Invoke(rangeType.GetConstructor([indexType, indexType])!, null, CreateIndex(indexType, start, startFromEnd), CreateIndex(indexType, end, endFromEnd))!;

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Invoke(rangeType.GetMethod("GetOffsetAndLength")!, range, length));

        Assert.AreEqual("length", exception.ParamName);
    }

    [TestMethod]
    public void Range_Factories_PreserveEndpointsAndValueEquality()
    {
        using CompatibilityAssembly assembly = new();
        Type indexType = assembly.GetRequiredType("System.Index");
        Type rangeType = assembly.GetRequiredType("System.Range");
        object startAt = Invoke(rangeType.GetMethod("StartAt")!, null, CreateIndex(indexType, 3, true))!;
        object endAt = Invoke(rangeType.GetMethod("EndAt")!, null, CreateIndex(indexType, 4, false))!;
        object all = rangeType.GetProperty("All")!.GetValue(null)!;

        Assert.AreEqual((7, 3), Invoke(rangeType.GetMethod("GetOffsetAndLength")!, startAt, 10));
        Assert.AreEqual((0, 4), Invoke(rangeType.GetMethod("GetOffsetAndLength")!, endAt, 10));
        Assert.AreEqual((0, 10), Invoke(rangeType.GetMethod("GetOffsetAndLength")!, all, 10));
        Assert.AreEqual("^3..^0", startAt.ToString());
        Assert.AreEqual("0..4", endAt.ToString());
        Assert.AreEqual("0..^0", all.ToString());
        Assert.IsFalse(startAt.Equals(endAt));
        Assert.IsFalse(endAt.Equals(all));
    }

    [TestMethod]
    public void BackwardCompatibility_ContainsAndAppendJoin_PreserveLegacyContracts()
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("Polyfills.Polyfill");
        MethodInfo containsCharacter = type.GetMethod("Contains", [typeof(string), typeof(char)])!;
        MethodInfo containsText = type.GetMethod("Contains", [typeof(string), typeof(string), typeof(StringComparison)])!;

        Assert.IsTrue((bool)Invoke(containsCharacter, null, "Alpha Beta", 'B')!);
        Assert.IsFalse((bool)Invoke(containsCharacter, null, "Alpha Beta", 'Z')!);
        Assert.IsTrue((bool)Invoke(containsText, null, "Alpha Beta", "ALPHA", StringComparison.OrdinalIgnoreCase)!);
        Assert.IsFalse((bool)Invoke(containsText, null, "Alpha Beta", "ALPHA", StringComparison.Ordinal)!);
        Assert.IsTrue((bool)Invoke(containsText, null, "Alpha Beta", string.Empty, StringComparison.Ordinal)!);

        StringBuilder strings = new("prefix:");
        MethodInfo appendStrings = type.GetMethods().Single(method => method.Name == "AppendJoin" && !method.IsGenericMethod && method.GetParameters().Length == 3);
        string[] values = ["left", string.Empty, "right"];
        Assert.AreSame(strings, Invoke(appendStrings, null, strings, ":", values));
        Assert.AreEqual("prefix:left::right", strings.ToString());

        StringBuilder numbers = new("prefix:");
        MethodInfo appendNumbers = type.GetMethods().Single(method => method.Name == "AppendJoin" && method.IsGenericMethodDefinition && method.GetParameters().Length == 3).MakeGenericMethod(typeof(int));
        int[] numberValues = [2, 0, -1];
        Assert.AreSame(numbers, Invoke(appendNumbers, null, numbers, "/", numberValues));
        Assert.AreEqual("prefix:2/0/-1", numbers.ToString());
    }

    [TestMethod]
    public void BackwardCompatibility_Deconstruct_PreservesBothComponents()
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("Polyfills.Polyfill");
        MethodInfo deconstruct = type.GetMethods().Single(method => method.Name == "Deconstruct" && method.IsGenericMethodDefinition).MakeGenericMethod(typeof(string), typeof(int));
        object?[] arguments = [new KeyValuePair<string, int>("answer", 42), null, null];

        Invoke(deconstruct, null, arguments);

        Assert.AreEqual("answer", arguments[1]);
        Assert.AreEqual(42, arguments[2]);
    }

    [TestMethod]
    public async Task BackwardCompatibility_CopyToAsync_TransfersFromCurrentPositionAndHonorsCancellation()
    {
        using CompatibilityAssembly assembly = new();
        MethodInfo copy = assembly.GetRequiredType("Polyfills.Polyfill").GetMethod("CopyToAsync", [typeof(Stream), typeof(Stream), typeof(CancellationToken)])!;
        using MemoryStream source = new([0, 255, 1, 13, 10, 128], writable: false);
        source.Position = 2;
        using MemoryStream destination = new();

        await (Task)Invoke(copy, null, source, destination, CancellationToken.None)!;

        Assert.AreSequenceEqual<byte>([1, 13, 10, 128], destination.ToArray());
        Assert.AreEqual(6L, source.Position);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using MemoryStream untouched = new([42, 43]);
        await Assert.ThrowsAsync<OperationCanceledException>(() => (Task)Invoke(copy, null, source, untouched, cancellation.Token)!);
        Assert.AreSequenceEqual<byte>([42, 43], untouched.ToArray());
    }

    [TestMethod]
    public async Task BackwardCompatibility_SaveAsync_PreservesXmlAndHonorsCancellation()
    {
        using CompatibilityAssembly assembly = new();
        MethodInfo save = assembly.GetRequiredType("Polyfills.Polyfill").GetMethod("SaveAsync", [typeof(XDocument), typeof(Stream), typeof(SaveOptions), typeof(CancellationToken)])!;
        var document = XDocument.Parse("<root><item id=\"7\">value</item></root>");
        using MemoryStream destination = new();

        await (Task)Invoke(save, null, document, destination, SaveOptions.DisableFormatting, CancellationToken.None)!;

        Assert.AreEqual(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><root><item id=\"7\">value</item></root>",
            Encoding.UTF8.GetString(destination.ToArray()).TrimStart('\uFEFF'));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using MemoryStream untouched = new();
        Assert.ThrowsExactly<OperationCanceledException>(() => Invoke(save, null, document, untouched, SaveOptions.DisableFormatting, cancellation.Token));
        Assert.AreEqual(0L, untouched.Length);
    }

    [TestMethod]
    [DataRow("System.Runtime.CompilerServices.CallerArgumentExpressionAttribute", "ParameterName", "argument", true)]
    [DataRow("System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute", "FeatureName", "RequiredMembers", false)]
    [DataRow("System.Diagnostics.CodeAnalysis.ExperimentalAttribute", "DiagnosticId", "TEST001", false)]
    [DataRow("System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute", "ParameterName", "result", false)]
    public void CompatibilityAttributes_StringPayloads_PreserveCompilerMetadata(string fullName, string propertyName, string payload, bool inherited)
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType(fullName);
        object attribute = Invoke(type.GetConstructor([typeof(string)])!, null, payload)!;
        AttributeUsageAttribute usage = type.GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.AreEqual(payload, type.GetProperty(propertyName)!.GetValue(attribute));
        Assert.IsTrue(typeof(Attribute).IsAssignableFrom(type));
        Assert.AreEqual(inherited, usage.Inherited);
        Assert.IsTrue(type.IsSealed);
        if (fullName.EndsWith("CompilerFeatureRequiredAttribute", StringComparison.Ordinal))
        {
            Assert.AreEqual("RefStructs", type.GetField("RefStructs")!.GetRawConstantValue());
            Assert.AreEqual("RequiredMembers", type.GetField("RequiredMembers")!.GetRawConstantValue());
            Assert.IsFalse((bool)type.GetProperty("IsOptional")!.GetValue(attribute)!);
            type.GetProperty("IsOptional")!.SetValue(attribute, true);
            Assert.IsTrue((bool)type.GetProperty("IsOptional")!.GetValue(attribute)!);
            Assert.IsTrue(usage.AllowMultiple);
        }
        else if (fullName.EndsWith("ExperimentalAttribute", StringComparison.Ordinal))
        {
            Assert.IsNull(type.GetProperty("UrlFormat")!.GetValue(attribute));
            type.GetProperty("UrlFormat")!.SetValue(attribute, "docs/{0}");
            Assert.AreEqual("docs/{0}", type.GetProperty("UrlFormat")!.GetValue(attribute));
        }
    }

    [TestMethod]
    [DataRow("System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute", "ReturnValue", false)]
    [DataRow("System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute", "ReturnValue", true)]
    [DataRow("System.Diagnostics.CodeAnalysis.NotNullWhenAttribute", "ReturnValue", false)]
    [DataRow("System.Diagnostics.CodeAnalysis.NotNullWhenAttribute", "ReturnValue", true)]
    [DataRow("System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute", "ParameterValue", false)]
    [DataRow("System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute", "ParameterValue", true)]
    public void CompatibilityAttributes_ConditionalNullability_PreservesBothBooleanStates(string fullName, string propertyName, bool condition)
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType(fullName);
        object attribute = Invoke(type.GetConstructor([typeof(bool)])!, null, condition)!;
        AttributeUsageAttribute usage = type.GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.AreEqual(condition, type.GetProperty(propertyName)!.GetValue(attribute));
        Assert.AreEqual(AttributeTargets.Parameter, usage.ValidOn);
        Assert.IsFalse(usage.Inherited);
    }

    [TestMethod]
    public void CompatibilityAttributes_MemberLists_PreserveOverloadsAndConditions()
    {
        using CompatibilityAssembly assembly = new();
        Type memberType = assembly.GetRequiredType("System.Diagnostics.CodeAnalysis.MemberNotNullAttribute");
        object single = Invoke(memberType.GetConstructor([typeof(string)])!, null, "_first")!;
        string[] names = ["_first", "_second"];
        object multiple = Invoke(memberType.GetConstructor([typeof(string[])])!, null, (object)names)!;

        Assert.AreSequenceEqual<string>(["_first"], (string[])memberType.GetProperty("Members")!.GetValue(single)!);
        Assert.AreSequenceEqual<string>(["_first", "_second"], (string[])memberType.GetProperty("Members")!.GetValue(multiple)!);
        Type conditionalType = assembly.GetRequiredType("System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute");
        foreach (bool condition in new[] { false, true })
        {
            object conditionalSingle = Invoke(conditionalType.GetConstructor([typeof(bool), typeof(string)])!, null, condition, "_first")!;
            object conditionalMultiple = Invoke(conditionalType.GetConstructor([typeof(bool), typeof(string[])])!, null, condition, names)!;
            Assert.AreEqual(condition, conditionalType.GetProperty("ReturnValue")!.GetValue(conditionalSingle));
            Assert.AreEqual(condition, conditionalType.GetProperty("ReturnValue")!.GetValue(conditionalMultiple));
            Assert.AreSequenceEqual<string>(["_first"], (string[])conditionalType.GetProperty("Members")!.GetValue(conditionalSingle)!);
            Assert.AreSequenceEqual<string>(["_first", "_second"], (string[])conditionalType.GetProperty("Members")!.GetValue(conditionalMultiple)!);
        }
    }

    [TestMethod]
    public void CompatibilityAttributes_InterpolatedArgumentsAndPriority_PreserveBindingInputs()
    {
        using CompatibilityAssembly assembly = new();
        Type argumentType = assembly.GetRequiredType("System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute");
        object single = Invoke(argumentType.GetConstructor([typeof(string)])!, null, "receiver")!;
        string[] arguments = ["receiver", "provider"];
        object multiple = Invoke(argumentType.GetConstructor([typeof(string[])])!, null, (object)arguments)!;
        Assert.AreSequenceEqual<string>(["receiver"], (string[])argumentType.GetProperty("Arguments")!.GetValue(single)!);
        Assert.AreSequenceEqual<string>(["receiver", "provider"], (string[])argumentType.GetProperty("Arguments")!.GetValue(multiple)!);

        Type marker = assembly.GetRequiredType("System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute");
        Assert.IsInstanceOfType<Attribute>(Activator.CreateInstance(marker));
        Assert.AreEqual(AttributeTargets.Class | AttributeTargets.Struct, marker.GetCustomAttribute<AttributeUsageAttribute>()!.ValidOn);
        Type priorityType = assembly.GetRequiredType("System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute");
        object priority = Invoke(priorityType.GetConstructor([typeof(int)])!, null, -7)!;
        Assert.AreEqual(-7, priorityType.GetProperty("Priority")!.GetValue(priority));
    }

    [TestMethod]
    [DataRow("TargetPlatformAttribute")]
    [DataRow("SupportedOSPlatformAttribute")]
    [DataRow("UnsupportedOSPlatformAttribute")]
    [DataRow("ObsoletedOSPlatformAttribute")]
    [DataRow("SupportedOSPlatformGuardAttribute")]
    [DataRow("UnsupportedOSPlatformGuardAttribute")]
    public void CompatibilityAttributes_PlatformAnnotations_PreserveVersionAndOptionalMessage(string name)
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType($"System.Runtime.Versioning.{name}");
        object attribute = Invoke(type.GetConstructor([typeof(string)])!, null, "windows10.0.19041.0")!;
        Assert.AreEqual("windows10.0.19041.0", type.GetProperty("PlatformName")!.GetValue(attribute));
        Assert.IsFalse(type.GetCustomAttribute<AttributeUsageAttribute>()!.Inherited);
        ConstructorInfo? withMessage = type.GetConstructor([typeof(string), typeof(string)]);
        if (withMessage is not null)
        {
            object explained = Invoke(withMessage, null, "linux6.0", "not available")!;
            Assert.AreEqual("linux6.0", type.GetProperty("PlatformName")!.GetValue(explained));
            Assert.AreEqual("not available", type.GetProperty("Message")!.GetValue(explained));
            Assert.IsNull(type.GetProperty("Message")!.GetValue(attribute));
        }

        PropertyInfo? url = type.GetProperty("Url");
        if (url is not null)
        {
            Assert.IsNull(url.GetValue(attribute));
            url.SetValue(attribute, "docs/platform");
            Assert.AreEqual("docs/platform", url.GetValue(attribute));
        }
    }

    [TestMethod]
    public void CompatibilityAttributes_SuppressionAndDynamicMembers_PreserveAllMetadata()
    {
        using CompatibilityAssembly assembly = new();
        Type suppressionType = assembly.GetRequiredType("System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute");
        object suppression = Invoke(suppressionType.GetConstructor([typeof(string), typeof(string)])!, null, "Trimming", "IL2026")!;
        Assert.AreEqual("Trimming", suppressionType.GetProperty("Category")!.GetValue(suppression));
        Assert.AreEqual("IL2026", suppressionType.GetProperty("CheckId")!.GetValue(suppression));
        foreach (string property in new[] { "Scope", "Target", "MessageId", "Justification" })
        {
            PropertyInfo member = suppressionType.GetProperty(property)!;
            Assert.IsNull(member.GetValue(suppression));
            member.SetValue(suppression, property + "-value");
            Assert.AreEqual(property + "-value", member.GetValue(suppression));
        }

        Type flagsType = assembly.GetRequiredType("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes");
        object flags = Enum.Parse(flagsType, "PublicMethods, PublicFields");
        Type dynamicType = assembly.GetRequiredType("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute");
        object dynamicMembers = Invoke(dynamicType.GetConstructor([flagsType])!, null, flags)!;
        Assert.AreEqual(40, Convert.ToInt32(dynamicType.GetProperty("MemberTypes")!.GetValue(dynamicMembers), CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void CompatibilityException_PreservesMessagesAndInnerException()
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("System.Diagnostics.UnreachableException");
        var defaultException = (Exception)Invoke(type.GetConstructor(Type.EmptyTypes)!, null)!;
        var messageException = (Exception)Invoke(type.GetConstructor([typeof(string)])!, null, "custom unreachable")!;
        InvalidOperationException cause = new("original cause");
        var nestedException = (Exception)Invoke(type.GetConstructor([typeof(string), typeof(Exception)])!, null, "nested unreachable", cause)!;

        Assert.AreEqual("The program executed an instruction that was thought to be unreachable.", defaultException.Message);
        Assert.AreEqual("custom unreachable", messageException.Message);
        Assert.IsNull(messageException.InnerException);
        Assert.AreEqual("nested unreachable", nestedException.Message);
        Assert.AreSame(cause, nestedException.InnerException);
    }

    [TestMethod]
    public void OperatingSystemCompatibility_UsesActualRuntimePlatform()
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("Polyfills.Polyfill");
        foreach (string platform in new[] { "Wasi", "Android", "IOS", "TvOS", "Browser" })
        {
            MethodInfo method = type.GetMethod("Is" + platform)!;
            Assert.AreEqual(RuntimeInformation.IsOSPlatform(OSPlatform.Create(platform.ToUpperInvariant())), Invoke(method, null));
        }
    }

    [TestMethod]
    public void Ensure_NotEmpty_ValidatesNullEmptyAndNonemptyCollectionShapes()
    {
        using CompatibilityAssembly assembly = new();
        MethodInfo method = assembly.GetRequiredType("Polyfills.Ensure").GetMethods()
            .Single(candidate => candidate.Name == "NotEmpty" && candidate.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(int));
        List<int> genericValues = [7];
        IEnumerable<int>?[] accepted = [null, genericValues, new LegacyCollection([7]), new ReadOnlyCollection([7]), YieldValue()];
        foreach (IEnumerable<int>? values in accepted)
        {
            Invoke(method, null, values, "items");
        }

        List<int> emptyValues = [];
        IEnumerable<int>[] empty = [emptyValues, new LegacyCollection([]), new ReadOnlyCollection([]), YieldEmpty()];
        foreach (IEnumerable<int> values in empty)
        {
            ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Invoke(method, null, values, "items"));
            Assert.AreEqual("items", exception.ParamName);
            Assert.StartsWith("Argument cannot be empty.", exception.Message);
        }
    }

    [TestMethod]
    public void Ensure_StringGuards_PreserveInputAndDistinguishEmptyFromWhitespace()
    {
        using CompatibilityAssembly assembly = new();
        Type type = assembly.GetRequiredType("Polyfills.Ensure");
        MethodInfo notEmpty = type.GetMethod("NotEmpty", [typeof(string), typeof(string)])!;
        MethodInfo notNullOrWhitespace = type.GetMethod("NotNullOrWhiteSpace")!;
        Invoke(notEmpty, null, null, "text");
        Invoke(notEmpty, null, " ", "text");
        ArgumentException empty = Assert.ThrowsExactly<ArgumentException>(() => Invoke(notEmpty, null, string.Empty, "text"));
        Assert.AreEqual("text", empty.ParamName);
        ArgumentNullException missing = Assert.ThrowsExactly<ArgumentNullException>(() => Invoke(notNullOrWhitespace, null, null, "text"));
        Assert.AreEqual("text", missing.ParamName);
        foreach (string value in new[] { string.Empty, " \t " })
        {
            ArgumentException whitespace = Assert.ThrowsExactly<ArgumentException>(() => Invoke(notNullOrWhitespace, null, value, "text"));
            Assert.AreEqual("text", whitespace.ParamName);
        }

        Assert.AreEqual(" padded ", Invoke(notNullOrWhitespace, null, " padded ", "text"));
    }

    private static IEnumerable<int> YieldValue()
    {
        yield return 7;
    }

    private static IEnumerable<int> YieldEmpty()
    {
        yield break;
    }

    private static object CreateIndex(Type type, int value, bool fromEnd)
        => Invoke(type.GetConstructor([typeof(int), typeof(bool)])!, null, value, fromEnd)!;

    private static object? Invoke(MethodBase member, object? target, params object?[] arguments)
    {
        try
        {
            return member is ConstructorInfo constructor
                ? constructor.Invoke(arguments)
                : ((MethodInfo)member).Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private sealed class CompatibilityAssembly : IDisposable
    {
        private readonly AssemblyLoadContext _context = new(nameof(CompatibilityPolyfillTests), isCollectible: true);
        private readonly Assembly _assembly;

        public CompatibilityAssembly()
        {
            _assembly = _context.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "LegacyMSBuild", "Microsoft.Testing.Platform.MSBuild.dll"));
            Assert.AreEqual(".NETStandard,Version=v2.0", _assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName);
        }

        public Type GetRequiredType(string fullName)
        {
            Type type = _assembly.GetType(fullName, throwOnError: true)!;
            Assert.AreSame(_assembly, type.Assembly, "Compatibility tests must not exercise a type forwarded to the host BCL.");
            return type;
        }

        public void Dispose() => _context.Unload();
    }

    private sealed class LegacyCollection(int[] values) : System.Collections.ICollection, IEnumerable<int>
    {
        public int Count => values.Length;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public void CopyTo(Array array, int index) => values.CopyTo(array, index);

        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)values).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReadOnlyCollection(int[] values) : IReadOnlyCollection<int>
    {
        public int Count => values.Length;

        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)values).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
