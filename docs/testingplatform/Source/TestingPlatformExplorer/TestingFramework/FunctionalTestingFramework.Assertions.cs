// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestingPlatformExplorer.UnitTests;

namespace TestingPlatformExplorer.FunctionalTestingFramework;

public static class Assert
{
    public static (TestOutCome OutCome, string Reason) AreEqual<T>(T expected, T actual)
    {
        return expected is null
            ? throw new ArgumentNullException(nameof(expected))
            : actual is null
            ? throw new ArgumentNullException(nameof(expected))
            : !expected.Equals(actual)
            ? ((TestOutCome OutCome, string Reason))(TestOutCome.Failed, $"Expected: {expected}, Actual: {actual}")
            : ((TestOutCome OutCome, string Reason))(TestOutCome.Passed, string.Empty);
    }
}
