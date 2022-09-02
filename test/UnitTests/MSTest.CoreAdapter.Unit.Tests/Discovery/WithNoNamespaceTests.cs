// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;
using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute]
public class WithNoNamespaceTests
{
    [TestMethodV1]
    public void Simple()
    {
        Assert.IsTrue(true);
    }
}
