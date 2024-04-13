// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestingPlatformExplorer.FunctionalTestingFramework;

namespace TestingPlatformExplorer.UnitTests;

public class UnitTestsRegistration
{
    public static Func<(TestOutCome, string)>[] GetActions()
    {
        return new Func<(TestOutCome, string)>[]
        {
            () => UnitTests.TestMethod1(),
            () => UnitTests.TestMethod2(),
            () => UnitTests.TestMethod3(),
        };
    }
}

public enum TestOutCome
{
    Passed,
    Failed,
    Skipped
}

public class UnitTests
{
    public static (TestOutCome, string) TestMethod1() => Assert.AreEqual(1, 1);

    public static (TestOutCome, string) TestMethod2() => Assert.AreEqual(1, 2);

    [Skip]
    public static (TestOutCome, string) TestMethod3() => Assert.AreEqual(1, 1);
}
