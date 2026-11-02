// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class EnvironmentVariablesConfigurationProviderTests
{
    [TestMethod]
    [DataRow("MYSQLCONNSTR_Main", "MySql.Data.MySqlClient")]
    [DataRow("sqlazureconnstr_Main", "System.Data.SqlClient")]
    [DataRow("SQLCONNSTR_Main", "System.Data.SqlClient")]
    [DataRow("CUSTOMCONNSTR_Main", null)]
    public async Task LoadAsync_ConnectionStringPrefixesProduceIndependentProviderNames(string name, string? expectedProvider)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariables()).Returns(new Hashtable { [name] = "Server=sample;Database=tests" });
        EnvironmentVariablesConfigurationProvider provider = new(environment.Object);

        await provider.LoadAsync();

        Assert.IsTrue(provider.TryGet("ConnectionStrings:Main", out string? value));
        Assert.AreEqual("Server=sample;Database=tests", value);
        Assert.AreEqual(expectedProvider is not null, provider.TryGet("ConnectionStrings:Main_ProviderName", out string? providerName));
        Assert.AreEqual(expectedProvider, providerName);
        Assert.IsFalse(provider.TryGet(name, out string? raw));
        Assert.IsNull(raw);
        environment.Verify(e => e.GetEnvironmentVariables(), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_NormalizedPrefixFiltersKeysAndPreservesNullValues()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariables()).Returns(new Hashtable
        {
            ["Application__Database__User"] = "Ada",
            ["Application__Ready"] = null,
            ["Other__Database__User"] = "ignored",
        });
        EnvironmentVariablesConfigurationProvider provider = new(environment.Object, "application__");

        await provider.LoadAsync();

        Assert.IsTrue(provider.TryGetScalar("DATABASE:USER", out string? user));
        Assert.AreEqual("Ada", user);
        Assert.IsTrue(provider.TryGet("Ready", out string? ready));
        Assert.IsNull(ready);
        Assert.IsFalse(provider.TryGet("Other:Database:User", out string? ignored));
        Assert.IsNull(ignored);
        Assert.AreSequenceEqual(["Database", "Ready"], provider.GetChildKeys(null).OrderBy(key => key, StringComparer.Ordinal));
        Assert.AreSequenceEqual(["User"], provider.GetChildKeys("Database"));
    }

    [TestMethod]
    public void TryGet_NullAndEmptyKeysRejectInvalidLookup()
    {
        EnvironmentVariablesConfigurationProvider provider = new(new Mock<IEnvironment>(MockBehavior.Strict).Object);

        Assert.AreEqual("key", Assert.ThrowsExactly<ArgumentNullException>(() => provider.TryGet(null!, out _)).ParamName);
        Assert.AreEqual("key", Assert.ThrowsExactly<ArgumentException>(() => provider.TryGet(string.Empty, out _)).ParamName);
    }
}
