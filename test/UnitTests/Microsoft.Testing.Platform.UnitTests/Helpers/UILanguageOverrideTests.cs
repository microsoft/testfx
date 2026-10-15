// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class UILanguageOverrideTests
{
    [TestMethod]
    [DataRow("fr-FR", "de-DE", "1033", "fr-FR", DisplayName = "Platform language has highest priority")]
    [DataRow(null, "de-DE", "1033", "de-DE", DisplayName = "CLI language precedes Visual Studio")]
    [DataRow("/invalid/", "de-DE", "1033", "de-DE", DisplayName = "Invalid platform language falls back to CLI")]
    [DataRow(null, null, "1033", "en-US", DisplayName = "Visual Studio LCID fallback")]
    [DataRow(null, "/invalid/", "1033", "en-US", DisplayName = "Invalid CLI language falls back to LCID")]
    [DataRow("/invalid/", "/invalid/", "1033", "en-US", DisplayName = "Both invalid names fall back to LCID")]
    public void LanguageSelection_UsesPrecedenceAndSkipsInvalidHigherPriorityValues(string? platform, string? cli, string? visualStudio, string expected)
    {
        IEnvironment environment = CreateEnvironment(platform, cli, visualStudio);

        CultureInfo? result = SelectLanguage(environment);

        Assert.AreEqual(expected, result?.Name);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "All language options absent")]
    [DataRow("not-an-integer", DisplayName = "Noninteger Visual Studio LCID")]
    [DataRow("-1", DisplayName = "Negative Visual Studio LCID")]
    [DataRow("999999", DisplayName = "Unknown Visual Studio LCID")]
    public void LanguageSelection_AbsentOrInvalidOptions_DoNotChooseAnOverride(string? visualStudio)
    {
        IEnvironment environment = CreateEnvironment("/invalid/", "/invalid/", visualStudio);

        Assert.IsNull(SelectLanguage(environment));
        Assert.AreEqual("fr-FR", SelectLanguage(CreateEnvironment("/invalid/", "fr-FR", visualStudio))?.Name);
    }

    // Exercise the decision independently of the process-global default-culture setter.
    // This keeps existing assembly scheduling unchanged and avoids uncoordinated global observers.
    private static CultureInfo? SelectLanguage(IEnvironment environment)
        => (CultureInfo?)typeof(UILanguageOverride)
            .GetMethod("GetOverriddenUILanguage", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [environment]);

    private static IEnvironment CreateEnvironment(string? platform, string? cli, string? visualStudio)
    {
        Dictionary<string, string?> values = new()
        {
            ["TESTINGPLATFORM_UI_LANGUAGE"] = platform,
            ["DOTNET_CLI_UI_LANGUAGE"] = cli,
            ["VSLANG"] = visualStudio,
            ["TF_BUILD"] = null,
        };
        Mock<IEnvironment> environment = new(MockBehavior.Strict);
        environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(key => values.TryGetValue(key, out string? value) ? value : null);
        return environment.Object;
    }
}
