// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderCommandLineProviderTests
{
    [TestMethod]
    public void Constructor_ExposesStableMetadata()
    {
        VideoRecorderCommandLineProvider provider = new();

        Assert.AreEqual("VideoRecorderCommandLineProvider", provider.Uid);
        AssertVersionMatchesAssembly(provider.Version, typeof(VideoRecorderCommandLineProvider).Assembly);
        Assert.AreEqual(VideoRecorderResources.ExtensionDisplayName, provider.DisplayName);
        Assert.AreEqual(VideoRecorderResources.CommandLineProviderDescription, provider.Description);
    }

    [TestMethod]
    public void GetCommandLineOptions_ReturnsExpectedVisibleOptions()
    {
        VideoRecorderCommandLineProvider provider = new();

        CommandLineOption[] options = [.. provider.GetCommandLineOptions()];

        Assert.HasCount(6, options);
        AssertOption(options[0], VideoRecorderCommandLineProvider.EnableOptionName, VideoRecorderResources.OptionDescriptionCaptureVideo, ArgumentArity.ZeroOrOne);
        AssertOption(options[1], VideoRecorderCommandLineProvider.SourceOptionName, VideoRecorderResources.OptionDescriptionSource, ArgumentArity.ExactlyOne);
        AssertOption(options[2], VideoRecorderCommandLineProvider.GranularityOptionName, VideoRecorderResources.OptionDescriptionGranularity, ArgumentArity.ExactlyOne);
        AssertOption(options[3], VideoRecorderCommandLineProvider.ArgsOptionName, VideoRecorderResources.OptionDescriptionArgs, ArgumentArity.ExactlyOne);
        AssertOption(options[4], VideoRecorderCommandLineProvider.MaxDurationOptionName, VideoRecorderResources.OptionDescriptionMaxDuration, ArgumentArity.ExactlyOne);
        AssertOption(options[5], VideoRecorderCommandLineProvider.ChaptersOptionName, VideoRecorderResources.OptionDescriptionChapters, ArgumentArity.ExactlyOne);
    }

    [TestMethod]
    [DataRow(VideoRecorderCommandLineProvider.ModeAlways)]
    [DataRow(VideoRecorderCommandLineProvider.ModeOnFailure)]
    [DataRow("ALWAYS")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForAllowedEnableValuesAsync(string value)
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.EnableOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenEnableHasNoArgumentsAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.EnableOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, []).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenEnableValueIsNotRecognizedAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.EnableOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["sometimes"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.InvalidOptionValue,
                "sometimes",
                VideoRecorderCommandLineProvider.EnableOptionName,
                $"'{VideoRecorderCommandLineProvider.ModeOnFailure}', '{VideoRecorderCommandLineProvider.ModeAlways}'"),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(VideoRecorderCommandLineProvider.SourceScreen)]
    [DataRow(VideoRecorderCommandLineProvider.SourceWindow)]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForAllowedSourceValuesAsync(string value)
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.SourceOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenSourceValueIsNotRecognizedAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.SourceOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["monitor"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.InvalidOptionValue,
                "monitor",
                VideoRecorderCommandLineProvider.SourceOptionName,
                $"'{VideoRecorderCommandLineProvider.SourceScreen}', '{VideoRecorderCommandLineProvider.SourceWindow}'"),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(VideoRecorderCommandLineProvider.GranularityTest)]
    [DataRow(VideoRecorderCommandLineProvider.GranularitySession)]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForAllowedGranularityValuesAsync(string value)
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.GranularityOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenGranularityValueIsNotRecognizedAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.GranularityOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["method"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.InvalidOptionValue,
                "method",
                VideoRecorderCommandLineProvider.GranularityOptionName,
                $"'{VideoRecorderCommandLineProvider.GranularityTest}', '{VideoRecorderCommandLineProvider.GranularitySession}'"),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(VideoRecorderCommandLineProvider.ChaptersOn)]
    [DataRow(VideoRecorderCommandLineProvider.ChaptersOff)]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForAllowedChaptersValuesAsync(string value)
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.ChaptersOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenChaptersValueIsNotRecognizedAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.ChaptersOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.InvalidOptionValue,
                "maybe",
                VideoRecorderCommandLineProvider.ChaptersOptionName,
                $"'{VideoRecorderCommandLineProvider.ChaptersOn}', '{VideoRecorderCommandLineProvider.ChaptersOff}'"),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("30")]
    [DataRow("1")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenMaxDurationIsPositiveIntegerAsync(string value)
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.MaxDurationOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-5")]
    [DataRow("abc")]
    [DataRow("1.5")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenMaxDurationIsNotPositiveIntegerAsync(string value)
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.MaxDurationOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.InvalidOptionPositiveInteger,
                value,
                VideoRecorderCommandLineProvider.MaxDurationOptionName),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForUnrecognizedOptionAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == VideoRecorderCommandLineProvider.ArgsOptionName);

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["--any-ffmpeg-args"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(VideoRecorderCommandLineProvider.SourceOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.GranularityOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.ArgsOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.MaxDurationOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.ChaptersOptionName)]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSubOptionIsUsedWithoutEnableAsync(string subOption)
    {
        VideoRecorderCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [subOption] = ["screen"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.SubOptionsRequireEnable, VideoRecorderCommandLineProvider.EnableOptionName),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(VideoRecorderCommandLineProvider.SourceOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.GranularityOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.ArgsOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.MaxDurationOptionName)]
    [DataRow(VideoRecorderCommandLineProvider.ChaptersOptionName)]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenSubOptionIsUsedWithEnableAsync(string subOption)
    {
        VideoRecorderCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
            [subOption] = ["screen"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenOnlyEnableIsUsedAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenNoOptionsAreUsedAsync()
    {
        VideoRecorderCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    private static void AssertOption(CommandLineOption option, string name, string description, ArgumentArity arity)
    {
        Assert.AreEqual(name, option.Name);
        Assert.AreEqual(description, option.Description);
        Assert.AreEqual(arity, option.Arity);
        Assert.IsFalse(option.IsHidden);
    }

    private static void AssertVersionMatchesAssembly(string reportedVersion, Assembly extensionAssembly)
    {
        string expectedVersion = extensionAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? extensionAssembly.GetName().Version?.ToString()
            ?? string.Empty;
        int plusIndex = expectedVersion.LastIndexOf('+');
        if (plusIndex >= 0)
        {
            expectedVersion = expectedVersion[..plusIndex];
        }

        Assert.AreEqual(expectedVersion, reportedVersion);
    }
}
