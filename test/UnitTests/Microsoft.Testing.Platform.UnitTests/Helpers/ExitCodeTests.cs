// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.Helpers;

[TestClass]
public class ExitCodeTests
{
    [TestMethod]
    public void IsKnownExitCodeTestForRange()
    {
        var exitCodeFields = typeof(ExitCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (int)f.GetValue(null)!)
            .ToHashSet();

        // When adding/removing exit codes, this need to be updated.
        Assert.HasCount(13, exitCodeFields);

        for (int i = -100; i <= 100; i++)
        {
            if (exitCodeFields.Remove(i))
            {
                Assert.IsTrue(ExitCodes.IsKnownExitCode(i), $"Exit code {i} was expected to be known.");
            }
            else
            {
                Assert.IsFalse(ExitCodes.IsKnownExitCode(i), $"Exit code {i} was expected to not be known.");
            }
        }

        // If exitCodeFields is not empty. That means there is a valid known exit code that's not tested by the loop above.
        Assert.IsEmpty(exitCodeFields, $"The following exit codes were not tested by the loop above: {string.Join(", ", exitCodeFields)}.");
    }
}
