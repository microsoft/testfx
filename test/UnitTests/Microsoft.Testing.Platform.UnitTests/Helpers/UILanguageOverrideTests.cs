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

    [TestMethod]
    [DoNotParallelize]
    public void SetCultureSpecifiedByUser_AppliesSelectedLanguageAndPreservesGranularChildOverrides()
    {
        CultureInfo? previous = CultureInfo.DefaultThreadCurrentUICulture;
        Dictionary<string, string?> variables = new()
        {
            ["TESTINGPLATFORM_UI_LANGUAGE"] = "fr-FR",
            ["DOTNET_CLI_UI_LANGUAGE"] = "de-DE",
            ["VSLANG"] = null,
            ["PreferredUILang"] = null,
        };
        List<(string Name, string? Value)> writes = [];
        Mock<IEnvironment> environment = new(MockBehavior.Strict);
        environment.Setup(e => e.GetEnvironmentVariable(Moq.It.IsAny<string>()))
            .Returns<string>(name => variables.TryGetValue(name, out string? value) ? value : null);
        environment.Setup(e => e.SetEnvironmentVariable(Moq.It.IsAny<string>(), Moq.It.IsAny<string?>()))
            .Callback<string, string?>((name, value) => writes.Add((name, value)));
        try
        {
            UILanguageOverride.SetCultureSpecifiedByUser(environment.Object);

            Assert.AreEqual("fr-FR", CultureInfo.DefaultThreadCurrentUICulture?.Name);
            Assert.AreSequenceEqual<(string, string?)>([("VSLANG", "1036"), ("PreferredUILang", "fr-FR")], writes);
            environment.Verify(e => e.SetEnvironmentVariable("TESTINGPLATFORM_UI_LANGUAGE", Moq.It.IsAny<string?>()), Times.Never);
            environment.Verify(e => e.SetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", Moq.It.IsAny<string?>()), Times.Never);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previous;
        }
    }
}
