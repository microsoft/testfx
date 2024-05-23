// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.Adapter.UnitTests.Examples.DifferentAssembly;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class DummyBaseTestClass
{
    [TestMethod]
    public void BaseTestMethod()
    {
    }
}

[TestClass]
public class DummyDerivedTestClass : DummyBaseTestClass
{
    [TestMethod]
    public void DerivedTestMethod()
    {
    }
}

[TestClass]
public class DummyHidingTestClass : DummyBaseTestClass
{
    [TestMethod]
    public new virtual void BaseTestMethod()
    {
    }

    [TestMethod]
    public virtual void DerivedTestMethod()
    {
    }
}

[TestClass]
public class DummyOverridingTestClass : DummyHidingTestClass
{
    [TestMethod]
    public override void DerivedTestMethod()
    {
    }
}

[TestClass]
public class DummySecondHidingTestClass : DummyOverridingTestClass
{
    [TestMethod]
    public new void BaseTestMethod()
    {
    }

    [TestMethod]
    public new void DerivedTestMethod()
    {
    }
}

public class DummyTestClassWithGenericMethods
{
    public void GenericMethod<T>()
    {
    }
}

public abstract class DummyTestClass
{
    [TestMethod]
    public static void StaticTestMethod()
    {
    }

    [TestMethod]
    public abstract void AbstractTestMethod();

    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
    [TestMethod]
    public async void AsyncMethodWithVoidReturnType() => await Task.FromResult(true);

    [TestMethod]
    public async Task AsyncMethodWithTaskReturnType() => await Task.Delay(TimeSpan.Zero);

    [TestMethod]
    public Task MethodWithTaskReturnType() => Task.Delay(TimeSpan.Zero);

    [TestMethod]
    public int MethodWithIntReturnType() => 0;

    [TestMethod]
    public void MethodWithVoidReturnType()
    {
    }

    [TestMethod]
    internal void InternalTestMethod()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    [TestMethod]
    private void PrivateTestMethod()
#pragma warning restore IDE0051 // Remove unused private members
    {
    }
}

public class TestsWithoutIgnore
{
    public void WithoutIgnore()
    {
    }
}

[TestCategory("category on class")]
public class TestsWithCategory
{
    [TestCategory("category on method")]
    public void WithCategory()
    {
    }
}

public class DoNotParallelizeTests
{
    [DoNotParallelize]
    public void WithDoNotParallelize()
    {
    }
}

public class TestsForMetadata
{
    [TestProperty("foo", "bar")]
    [TestProperty("fooprime", "barprime")]
    public void WithTraits()
    {
    }

    [Owner("mike")]
    public void WithOwner()
    {
    }

    [Priority(1)]
    public void WithPriority()
    {
    }

    [Description("Dummy description")]
    public void WithDescription()
    {
    }

    public void WithoutWorkItems()
    {
    }

    [WorkItem(123)]
    [WorkItem(345)]
    public void WithWorkItems()
    {
    }

    [CssProjectStructure("ProjectStructure123")]
    public void WithCssProjectStructure()
    {
    }

    [CssIteration("234")]
    public void WithCssIteration()
    {
    }
}

public class EmptyClass
{
}

[TestClass]
public class EmptyTestClass
{
}

[TestClass]
public class TestClassWithOneTestMethod
{
    [TestMethod]
    public void TestMethod()
    {
    }
}

[TestClass]
public class DerivedTestClassWithParentFromDifferentAssembly : BaseTestClassFromDifferentAssembly
{
    [TestMethod]
    public void DerivedTestMethod()
    {
    }
}

#if !WINDOWS_UWP && !WIN_UI
public class DeploymentItemTests
{
    public void WithoutDeploymentItem()
    {
    }

    [DeploymentItem(@"C:\temp")]
    public void WithDeploymentItem()
    {
    }
}
#endif

public class TestDisplayName
{
    public void WithoutDisplayName()
    {
    }

    [TestMethod(displayName: "display name")]
    public void WithDisplayName()
    {
    }

    [DataTestMethod(displayName: "data display name")]
    public void WithDataDisplayName()
    {
    }
}
