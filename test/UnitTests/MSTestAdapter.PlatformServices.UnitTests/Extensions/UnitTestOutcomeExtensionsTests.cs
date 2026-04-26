// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class UnitTestOutcomeExtensionsTests : TestContainer
{
    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Failed.GetMoreImportantOutcome(UnitTestOutcome.Inconclusive);
        resultOutcome.Should().Be(UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Passed.GetMoreImportantOutcome(UnitTestOutcome.Inconclusive);
        resultOutcome.Should().Be(UnitTestOutcome.Inconclusive);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Passed.GetMoreImportantOutcome(UnitTestOutcome.Failed);
        resultOutcome.Should().Be(UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Failed.GetMoreImportantOutcome(UnitTestOutcome.Failed);
        resultOutcome.Should().Be(UnitTestOutcome.Failed);
    }
}
