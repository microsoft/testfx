// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class JsonConfigurationProviderSchemaTests
{
    // ---------------------------------------------------------------------
    // Provider that has not been loaded yet
    // ---------------------------------------------------------------------
    [TestMethod]
    public void UnloadedProvider_EnumerationAndSectionLookups_ReturnEmpty()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(new Mock<IFileSystem>().Object);

        Assert.IsEmpty(provider.EnumerateCommandLineOptions());
        Assert.IsEmpty(provider.EnumerateCommandLineOptionDefaults());
        Assert.IsEmpty(provider.GetSection("anySection"));
    }

    [TestMethod]
    public void UnloadedProvider_KeyLookups_ReturnNothing()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(new Mock<IFileSystem>().Object);

        Assert.IsEmpty(provider.GetChildKeys(null));
        Assert.IsFalse(provider.TryGet("a", out string? value));
        Assert.IsNull(value);
        Assert.IsFalse(((IHierarchicalConfigurationProvider)provider).TryGetScalar("a", out string? scalar));
        Assert.IsNull(scalar);
    }

    [TestMethod]
    public void UnloadedProvider_NormalizeScalars_IsNoOp()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(new Mock<IFileSystem>().Object);

        provider.NormalizeCommandLineOptionScalars(Options(new CommandLineOption("opt", "d", ArgumentArity.ExactlyOne, isHidden: false)));

        Assert.IsEmpty(provider.EnumerateCommandLineOptions());
    }

    // ---------------------------------------------------------------------
    // Loaded provider lookups
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task LoadedProvider_GetChildKeys_ListsTopLevelKeys()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"alpha\": \"1\", \"beta\": {\"x\": \"2\"}}");

        string[] keys = [.. provider.GetChildKeys(null)];

        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
    }

    [TestMethod]
    public async Task LoadedProvider_TryGetScalar_OnlyReturnsNonNullScalars()
    {
        IHierarchicalConfigurationProvider provider = await LoadAsync("{\"a\": \"v\", \"empty\": {}}");

        Assert.IsTrue(provider.TryGetScalar("a", out string? value));
        Assert.AreEqual("v", value);
        Assert.IsFalse(provider.TryGetScalar("empty", out string? emptyValue));
        Assert.IsNull(emptyValue);
        Assert.IsFalse(provider.TryGetScalar("missing", out string? missingValue));
        Assert.IsNull(missingValue);
    }

    // ---------------------------------------------------------------------
    // Section container shape
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task Section_EmptyArray_IsRejectedAndEmptyObjectIsAccepted()
    {
        JsonConfigurationSource.JsonConfigurationProvider emptyArray = await LoadAsync("{\"commandLineOptions\": [], \"sec\": []}");
        JsonConfigurationSource.JsonConfigurationProvider emptyObject = await LoadAsync("{\"commandLineOptions\": {}, \"sec\": {}}");

        _ = Assert.ThrowsExactly<FormatException>(() => emptyArray.EnumerateCommandLineOptions());
        _ = Assert.ThrowsExactly<FormatException>(() => emptyArray.GetSection("sec"));
        Assert.IsEmpty(emptyObject.EnumerateCommandLineOptions());
        Assert.IsEmpty(emptyObject.GetSection("sec"));
    }

    [TestMethod]
    public async Task Section_NonEmptyArrayWithLeadingWhitespace_IsRejected()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\":   [ 1 ], \"sec\":\n\t[ 1 ]}");

        _ = Assert.ThrowsExactly<FormatException>(() => provider.EnumerateCommandLineOptions());
        _ = Assert.ThrowsExactly<FormatException>(() => provider.GetSection("sec"));
    }

    [TestMethod]
    public async Task Section_Scalar_IsRejected()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": \"oops\", \"sec\": \"oops\"}");

        _ = Assert.ThrowsExactly<FormatException>(() => provider.EnumerateCommandLineOptions());
        _ = Assert.ThrowsExactly<FormatException>(() => provider.GetSection("sec"));
    }

    [TestMethod]
    public async Task Section_Absent_ReturnsEmptyEvenWhenOtherContentExists()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"other\": \"x\", \"nested\": {\"a\": \"b\"}}");

        Assert.IsEmpty(provider.EnumerateCommandLineOptions());
        Assert.IsEmpty(provider.GetSection("sec"));
    }

    [TestMethod]
    public async Task Section_EntriesOutsideSection_AreIgnored()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync(
            "{\"other\": \"x\", \"commandLineOptions\": {\"a\": \"v\"}, \"sec\": {\"k\": \"1\"}, \"zzz\": \"y\"}");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(provider.EnumerateCommandLineOptions());
        Assert.AreEqual("a", entry.OptionName);
        Assert.AreSequenceEqual(["v"], entry.Arguments.ToArray());
        KeyValuePair<string, string?> section = Assert.ContainsSingle(provider.GetSection("sec"));
        Assert.AreEqual("k", section.Key);
        Assert.AreEqual("1", section.Value);
    }

    [TestMethod]
    public async Task Section_EntryWithEmptyName_IsRejectedAsNested()
    {
        // {"sec": {"": {"x": "v"}}} flattens to the key "sec::x".
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync(
            "{\"sec\": {\"\": {\"x\": \"v\"}}, \"commandLineOptions\": {\"\": {\"x\": \"v\"}}}");

        _ = Assert.ThrowsExactly<FormatException>(() => provider.GetSection("sec"));
        _ = Assert.ThrowsExactly<FormatException>(() => provider.EnumerateCommandLineOptions());
    }

    // ---------------------------------------------------------------------
    // Option entry shapes
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task Options_EmptyArrayEntry_IsSkippedWhileSiblingsAreKept()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": {\"a\": [], \"b\": \"v\"}}");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(provider.EnumerateCommandLineOptions());
        Assert.AreEqual("b", entry.OptionName);
    }

    [TestMethod]
    public async Task Options_ArrayContainingEmptyObject_IsRejected()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": {\"foo\": [{}]}}");

        _ = Assert.ThrowsExactly<FormatException>(() => provider.EnumerateCommandLineOptions());
    }

    [TestMethod]
    public async Task Options_ScalarAndIndexedFormsOfSameOption_AreRejected()
    {
        // A property literally named "foo:0" lands on the same flattened key as the array form of "foo".
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": {\"foo\": \"a\", \"foo:0\": \"b\"}}");

        _ = Assert.ThrowsExactly<FormatException>(() => provider.EnumerateCommandLineOptions());
    }

    [TestMethod]
    public async Task Options_IndexedEntriesWithGapOrNotStartingAtZero_AreRejected()
    {
        JsonConfigurationSource.JsonConfigurationProvider gap = await LoadAsync("{\"commandLineOptions\": {\"foo\": {\"0\": \"a\", \"2\": \"b\"}}}");
        JsonConfigurationSource.JsonConfigurationProvider offset = await LoadAsync("{\"commandLineOptions\": {\"foo\": {\"1\": \"a\"}}}");

        _ = Assert.ThrowsExactly<FormatException>(() => gap.EnumerateCommandLineOptions());
        _ = Assert.ThrowsExactly<FormatException>(() => offset.EnumerateCommandLineOptions());
    }

    // ---------------------------------------------------------------------
    // Scalar normalization
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task Normalize_ArgumentBearingScalar_MovesToIndexedKey()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": {\"a\": \"x\", \"b\": \"y\"}}");

        provider.NormalizeCommandLineOptionScalars(Options(
            new CommandLineOption("a", "d", ArgumentArity.ExactlyOne, isHidden: false),
            new CommandLineOption("b", "d", ArgumentArity.OneOrMore, isHidden: false)));

        Assert.IsTrue(provider.TryGet("commandLineOptions:a:0", out string? a));
        Assert.AreEqual("x", a);
        Assert.IsTrue(provider.TryGet("commandLineOptions:b:0", out string? b));
        Assert.AreEqual("y", b);
        Assert.IsFalse(((IHierarchicalConfigurationProvider)provider).TryGetScalar("commandLineOptions:a", out _));
        Assert.IsFalse(((IHierarchicalConfigurationProvider)provider).TryGetScalar("commandLineOptions:b", out _));
    }

    [TestMethod]
    public async Task Normalize_OptionalArgumentScalar_StaysScalar()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": {\"a\": \"x\"}}");

        provider.NormalizeCommandLineOptionScalars(Options(new CommandLineOption("a", "d", ArgumentArity.ZeroOrOne, isHidden: false)));

        Assert.IsTrue(provider.TryGet("commandLineOptions:a", out string? a));
        Assert.AreEqual("x", a);
        Assert.IsFalse(provider.TryGet("commandLineOptions:a:0", out _));
    }

    [TestMethod]
    public async Task Normalize_EmptyArrayPlaceholder_IsNotPromotedToIndexedSlot()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync("{\"commandLineOptions\": {\"a\": [], \"b\": \"y\"}}");

        provider.NormalizeCommandLineOptionScalars(Options(
            new CommandLineOption("a", "d", ArgumentArity.ExactlyOne, isHidden: false),
            new CommandLineOption("b", "d", ArgumentArity.ExactlyOne, isHidden: false)));

        Assert.IsFalse(provider.TryGet("commandLineOptions:a:0", out _));
        Assert.IsTrue(provider.TryGet("commandLineOptions:b:0", out string? b));
        Assert.AreEqual("y", b);
    }

    [TestMethod]
    public async Task Normalize_UnknownOptionAndNestedKeys_AreLeftUntouched()
    {
        JsonConfigurationSource.JsonConfigurationProvider provider = await LoadAsync(
            "{\"commandLineOptions\": {\"unknown\": \"x\", \"a\": [\"p\"], \"\": {\"x\": \"v\"}}, \"other\": \"z\"}");

        provider.NormalizeCommandLineOptionScalars(Options(new CommandLineOption("a", "d", ArgumentArity.ExactlyOne, isHidden: false)));

        Assert.IsTrue(provider.TryGet("commandLineOptions:unknown", out string? unknown));
        Assert.AreEqual("x", unknown);
        Assert.IsTrue(provider.TryGet("commandLineOptions:a:0", out string? a));
        Assert.AreEqual("p", a);
        Assert.IsTrue(provider.TryGet("commandLineOptions::x", out string? nested));
        Assert.AreEqual("v", nested);
        Assert.IsFalse(provider.TryGet("commandLineOptions::x:0", out _));
        Assert.IsTrue(provider.TryGet("other", out string? other));
        Assert.AreEqual("z", other);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------
    private static IReadOnlyDictionary<string, CommandLineOption> Options(params CommandLineOption[] options)
    {
        Dictionary<string, CommandLineOption> dictionary = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (CommandLineOption option in options)
        {
            dictionary[option.Name] = option;
        }

        return dictionary;
    }

    private static JsonConfigurationSource.JsonConfigurationProvider CreateProvider(IFileSystem fileSystem, ILogger? logger = null)
    {
        Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(m => m.TryGetCurrentTestApplicationFullPath()).Returns(Path.Combine("dir", "My.Tests.dll"));
        return new JsonConfigurationSource.JsonConfigurationProvider(module.Object, fileSystem, CommandLineParseResult.Empty, logger);
    }

    private static async Task<JsonConfigurationSource.JsonConfigurationProvider> LoadAsync(string json)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(Encoding.UTF8.GetBytes(json)));
        JsonConfigurationSource.JsonConfigurationProvider provider = CreateProvider(fileSystem.Object);
        await provider.LoadAsync();
        return provider;
    }
}
