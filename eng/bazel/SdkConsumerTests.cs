// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace TestFx.BazelSdkConsumer;

[TestClass]
public sealed class NativePackageTests
{
    [TestMethod]
    public void NativePackagePass()
        => Assert.AreEqual("Bazel", typeof(Assert).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "TestFx.BuildSystem").Value);

    [TestMethod]
    public void NativePackageFailure()
        => Assert.Fail("Intentional native package consumer failure.");
}
