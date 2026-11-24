// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class EnvironmentVariablesTests
{
    [TestMethod]
    public void MutationsRequireCurrentProviderAndDoNotInventMissingVariables()
    {
        (EnvironmentVariables variables, _) = CreateVariables();
        InvalidOperationException setError = Assert.ThrowsExactly<InvalidOperationException>(
            () => variables.SetVariable(new EnvironmentVariable("NAME", "value", false, false)));
        InvalidOperationException removeError = Assert.ThrowsExactly<InvalidOperationException>(() => variables.RemoveVariable("NAME"));
        Assert.Contains("environment variable", setError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("environment variable", removeError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsEmpty(variables.GetAll());
        Assert.IsFalse(variables.TryGetVariable("missing", out OwnedEnvironmentVariable? missing));
        Assert.IsNull(missing);
        variables.CurrentProvider = CreateProvider("first");
        variables.RemoveVariable("missing");
        Assert.IsEmpty(variables.GetAll());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void QueriesMaskOnlySecretValuesWhileLaunchSnapshotPreservesValueOwnerAndFlags(bool secret, bool locked)
    {
        (EnvironmentVariables variables, _) = CreateVariables();
        ITestHostEnvironmentVariableProvider owner = CreateProvider("first");
        variables.CurrentProvider = owner;
        variables.SetVariable(new EnvironmentVariable("NAME", "test-only-value", secret, locked));

        Assert.IsTrue(variables.TryGetVariable("NAME", out OwnedEnvironmentVariable? queried));
        Assert.AreEqual(secret ? "*****" : "test-only-value", queried!.Value);
        Assert.AreEqual("NAME", queried.Variable);
        Assert.AreEqual(secret, queried.IsSecret);
        Assert.AreEqual(locked, queried.IsLocked);
        Assert.AreSame(owner, queried.Owner);
        EnvironmentVariable launchValue = Assert.ContainsSingle(variables.GetAll());
        Assert.AreEqual("test-only-value", launchValue.Value);
        Assert.AreEqual(secret, launchValue.IsSecret);
        Assert.AreEqual(locked, launchValue.IsLocked);
    }

    [TestMethod]
    public void LockedVariablesRejectReplacementAndRemovalWithoutChangingOwnerOrValue()
    {
        (EnvironmentVariables variables, _) = CreateVariables();
        ITestHostEnvironmentVariableProvider owner = CreateProvider("first");
        variables.CurrentProvider = owner;
        variables.SetVariable(new EnvironmentVariable("LOCKED", "original", false, true));
        variables.CurrentProvider = CreateProvider("second");

        InvalidOperationException replacement = Assert.ThrowsExactly<InvalidOperationException>(
            () => variables.SetVariable(new EnvironmentVariable("LOCKED", "replacement", false, false)));
        InvalidOperationException removal = Assert.ThrowsExactly<InvalidOperationException>(() => variables.RemoveVariable("LOCKED"));

        foreach (InvalidOperationException exception in new[] { replacement, removal })
        {
            Assert.Contains("LOCKED", exception.Message);
            Assert.Contains("first display", exception.Message);
            Assert.Contains("second display", exception.Message);
        }

        Assert.IsTrue(variables.TryGetVariable("LOCKED", out OwnedEnvironmentVariable? actual));
        Assert.AreEqual("original", actual!.Value);
        Assert.AreSame(owner, actual.Owner);
        Assert.IsTrue(actual.IsLocked);
        Assert.HasCount(1, variables.GetAll());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnlockedReplacementTransfersOwnershipAndUpdatesFlagsWithoutAddingAnotherIdentity(bool trace)
    {
        (EnvironmentVariables variables, Mock<ILogger> logger) = CreateVariables(trace);
        variables.CurrentProvider = CreateProvider("first");
        variables.SetVariable(new EnvironmentVariable("NAME", "original", false, false));
        ITestHostEnvironmentVariableProvider replacement = CreateProvider("second");
        variables.CurrentProvider = replacement;

        variables.SetVariable(new EnvironmentVariable("NAME", null, true, true));

        Assert.IsTrue(variables.TryGetVariable("NAME", out OwnedEnvironmentVariable? actual));
        Assert.AreEqual("*****", actual!.Value);
        Assert.AreSame(replacement, actual.Owner);
        Assert.IsTrue(actual.IsLocked);
        Assert.IsTrue(actual.IsSecret);
        Assert.IsNull(Assert.ContainsSingle(variables.GetAll()).Value);
        if (trace)
        {
            Assert.Contains(
                invocation => invocation.Method.Name == nameof(ILogger.Log)
                    && invocation.Arguments[1].ToString()!.Contains("second updated environment variable 'NAME' from 'original' to ''.", StringComparison.Ordinal),
                logger.Invocations);
        }
        else
        {
            Assert.DoesNotContain(invocation => invocation.Method.Name == nameof(ILogger.Log), logger.Invocations);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RemoveUnlockedVariableRemovesItFromQueriesAndLaunchSnapshot(bool trace)
    {
        (EnvironmentVariables variables, _) = CreateVariables(trace);
        variables.CurrentProvider = CreateProvider("first");
        variables.SetVariable(new EnvironmentVariable("REMOVE", "old", false, false));
        variables.SetVariable(new EnvironmentVariable("KEEP", "preserved", false, false));
        variables.CurrentProvider = CreateProvider("second");

        variables.RemoveVariable("REMOVE");

        Assert.IsFalse(variables.TryGetVariable("REMOVE", out OwnedEnvironmentVariable? removed));
        Assert.IsNull(removed);
        EnvironmentVariable remaining = Assert.ContainsSingle(variables.GetAll());
        Assert.AreEqual("KEEP", remaining.Variable);
        Assert.AreEqual("preserved", remaining.Value);
    }

    private static ITestHostEnvironmentVariableProvider CreateProvider(string uid)
    {
        var provider = new Mock<ITestHostEnvironmentVariableProvider>();
        provider.SetupGet(value => value.Uid).Returns(uid);
        provider.SetupGet(value => value.DisplayName).Returns(uid + " display");
        return provider.Object;
    }

    private static (EnvironmentVariables Variables, Mock<ILogger> Logger) CreateVariables(bool trace = false)
    {
        var logger = new Mock<ILogger>();
        logger.Setup(value => value.IsEnabled(LogLevel.Trace)).Returns(trace);
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        return (new EnvironmentVariables(factory.Object), logger);
    }
}
