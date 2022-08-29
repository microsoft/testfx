// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestProjectForDiscovery;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SampleFrameworkExtensions;

[TestCategory("ba")]
public class AttributeTestBaseClass
{
    [Owner("base")]
    [TestCategory("base")]
    public virtual void DummyVTestMethod1()
    {
    }

    public void DummyBTestMethod2()
    {
    }
}

[TestCategory("a")]
public class AttributeTestClass : AttributeTestBaseClass
{
    [Owner("derived")]
    [TestCategory("derived")]
    public override void DummyVTestMethod1()
    {
    }

    [Owner("derived")]
    public void DummyTestMethod2()
    {
    }
}

[TestCategory("ac")]
public class AttributeTestClassWithCustomAttributes : AttributeTestBaseClass
{
    [Duration("superfast")]
    public override void DummyVTestMethod1()
    {
    }

    [CategoryArray("foo", "foo2")]
    public void DummyTestMethod2()
    {
    }
}
