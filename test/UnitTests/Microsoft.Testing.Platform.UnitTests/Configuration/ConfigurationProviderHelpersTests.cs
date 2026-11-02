// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationProviderHelpersTests
{
    [TestMethod]
    public void GetChildKeys_NullParentPath_ReturnsTopLevelSegments()
    {
        string[] keys = ["Logging:Console:Level", "ConnectionStrings:Default", "Standalone"];

        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(keys, parentPath: null);

        Assert.AreSequenceEqual(["Logging", "ConnectionStrings", "Standalone"], childKeys, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void GetChildKeys_NestedParentPath_ReturnsImmediateChildSegments()
    {
        string[] keys = ["Logging:Console:Enabled", "Logging:Console:Formatter:Name", "Logging:Console:Formatter:Options:Color"];

        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(keys, "Logging:Console");

        Assert.AreSequenceEqual(["Enabled", "Formatter"], childKeys, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void GetChildKeys_RepeatedChildren_ReturnsDistinctSegments()
    {
        string[] keys = ["Logging:Console:Enabled", "Logging:Console:Level", "Logging:Console", "Logging:Console", "Logging:File:Path"];

        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(keys, "Logging");

        Assert.AreSequenceEqual(["Console", "File"], childKeys, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void GetChildKeys_UnrelatedKeys_RequiresParentPrefixAndDelimiter()
    {
        string[] keys = ["Logging:Level", "Other:Level", "Other:Logging:Level", "LoggingExtra:Level", "Log:Level"];

        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(keys, "Logging");

        Assert.AreEqual("Level", Assert.ContainsSingle(childKeys));
    }

    [TestMethod]
    [DataRow("", null)]
    [DataRow("Logging", "Logging")]
    [DataRow("Logging:", "Logging")]
    public void GetChildKeys_NoChildSegment_ReturnsEmpty(string key, string? parentPath)
    {
        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys([key], parentPath);

        Assert.IsEmpty(childKeys);
    }

    [TestMethod]
    public void GetChildKeys_ParentPathWithDifferentCasing_MatchesIgnoringCase()
    {
        string[] keys = ["LOGGING:Console:Enabled", "logging:File:Path", "LoGgInG:Level"];

        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(keys, "Logging");

        Assert.AreSequenceEqual(["Console", "File", "Level"], childKeys, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    [DataRow(null, "Logging")]
    [DataRow("Logging", "Console")]
    public void GetChildKeys_ChildrenWithDifferentCasing_DeduplicatesIgnoringCase(string? parentPath, string expectedChild)
    {
        string[] keys = ["Logging:Console:Enabled", "logging:console:Level", "LOGGING:CONSOLE", "Logging:Console"];

        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(keys, parentPath);

        Assert.AreEqual(expectedChild, Assert.ContainsSingle(childKeys));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("Logging")]
    public void GetChildKeys_EmptyKeys_ReturnsEmpty(string? parentPath)
    {
        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys([], parentPath);

        Assert.IsEmpty(childKeys);
    }

    [TestMethod]
    public void GetChildKeys_SingleUndelimitedKeyWithNullParent_ReturnsEntireKey()
    {
        IEnumerable<string> childKeys = ConfigurationProviderHelpers.GetChildKeys(["Standalone"], parentPath: null);

        Assert.AreEqual("Standalone", Assert.ContainsSingle(childKeys));
    }
}
