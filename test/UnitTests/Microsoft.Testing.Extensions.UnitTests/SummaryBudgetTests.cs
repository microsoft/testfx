// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class SummaryBudgetTests
{
    // GitHubActionsFailureDetails.GitHubStepSummaryLimit == 1 MiB; the thresholds below mirror
    // DetailBudgetShare (0.4), CondenseShare (0.6), and StopListingShare (0.9) of that limit.
    private const long DetailBudgetLength = 419_430;
    private const long CondenseLength = 629_145;
    private const long StopListingLength = 943_718;
    private const int ProjectOverheadReserve = 8_000;
    private const long MaxTotalDetailsLength = DetailBudgetLength - ProjectOverheadReserve;

    [TestMethod]
    public void ForProject_WithNothingWrittenYet_StartsInFullStage()
    {
        var budget = SummaryBudget.ForProject(0);

        Assert.AreEqual(SummaryStage.Full, budget.Stage);
        Assert.AreEqual(MaxTotalDetailsLength, budget.DetailBytesAvailable);
    }

    [TestMethod]
    public void ForProject_CapsDetailAllowance_AtMaxTotalDetailsLength()
    {
        // Even with nothing written yet, a single project's detail allowance never exceeds
        // MaxTotalDetailsLength (DetailBudgetLength - ProjectOverheadReserve).
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: 0);

        Assert.AreEqual(MaxTotalDetailsLength, budget.DetailBytesAvailable);
    }

    [TestMethod]
    public void ForProject_WhenAlreadyWrittenBytesExceedsDetailBudget_HasNoDetailAllowance()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: DetailBudgetLength);

        Assert.AreEqual(0, budget.DetailBytesAvailable);
        Assert.AreEqual(SummaryStage.NoDetails, budget.Stage);
    }

    [TestMethod]
    public void ForProject_WhenAlreadyWrittenBytesReachesCondenseLength_IsCondensedStage()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: CondenseLength);

        Assert.AreEqual(SummaryStage.Condensed, budget.Stage);
    }

    [TestMethod]
    public void ForProject_WhenAlreadyWrittenBytesReachesStopListingLength_IsUnlistedStage()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: StopListingLength);

        Assert.AreEqual(SummaryStage.Unlisted, budget.Stage);
    }

    [TestMethod]
    public void ForProject_NegativeRemainingAllowance_ClampsToZero_NotNegative()
    {
        // alreadyWrittenBytes far beyond the detail budget would make the raw computation negative;
        // the constructor clamps it to zero rather than storing a negative allowance.
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: DetailBudgetLength * 10);

        Assert.AreEqual(0, budget.DetailBytesAvailable);
    }

    [TestMethod]
    public void Consume_IncreasesConsumedBytes_AndCanCrossStageThresholds()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: 0);
        Assert.AreEqual(SummaryStage.Full, budget.Stage);

        budget.Consume((int)CondenseLength);

        Assert.AreEqual(SummaryStage.Condensed, budget.Stage);
    }

    [TestMethod]
    public void TryReserveDetails_WhenEnoughAllowance_ReservesAndReturnsTrue()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: 0);
        int available = budget.DetailBytesAvailable;

        bool reserved = budget.TryReserveDetails(100);

        Assert.IsTrue(reserved);
        Assert.AreEqual(available - 100, budget.DetailBytesAvailable);
    }

    [TestMethod]
    public void TryReserveDetails_WhenNotEnoughAllowance_ReturnsFalse_AndReservesNothing()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: 0);
        int available = budget.DetailBytesAvailable;

        bool reserved = budget.TryReserveDetails(available + 1);

        Assert.IsFalse(reserved);
        // All-or-nothing: a failed reservation must not partially draw down the allowance.
        Assert.AreEqual(available, budget.DetailBytesAvailable);
    }

    [TestMethod]
    public void TryReserveDetails_ExactlyAvailableAmount_Succeeds_AndExhaustsAllowance()
    {
        var budget = SummaryBudget.ForProject(alreadyWrittenBytes: 0);
        int available = budget.DetailBytesAvailable;

        bool reserved = budget.TryReserveDetails(available);

        Assert.IsTrue(reserved);
        Assert.AreEqual(0, budget.DetailBytesAvailable);
        Assert.AreEqual(SummaryStage.NoDetails, budget.Stage);
    }

    [TestMethod]
    public void ForAggregate_WithNoGrantedShare_HasNoDetailAllowance_UntilGranted()
    {
        var budget = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 4);

        // Nothing is available until GrantModuleShare hands out a portion of the ungranted pool, but the
        // stage still reports Full: an ungranted (but non-empty) pool means detail is still possible once
        // granted, which is different from NoDetails (the allowance is genuinely exhausted).
        Assert.AreEqual(0, budget.DetailBytesAvailable);
        Assert.AreEqual(SummaryStage.Full, budget.Stage);
    }

    [TestMethod]
    public void ForAggregate_WhenUngrantedPoolIsFullyConsumedByOverhead_IsNoDetailsStage()
    {
        // With enough modules, ProjectOverheadReserve * moduleCount exceeds the whole detail budget,
        // driving the ungranted pool itself to (clamped) zero — genuinely no detail possible.
        var budget = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 1000);

        Assert.AreEqual(0, budget.DetailBytesAvailable);
        Assert.AreEqual(SummaryStage.NoDetails, budget.Stage);
    }

    [TestMethod]
    public void GrantModuleShare_DividesUngrantedPool_AcrossRemainingModules()
    {
        var budget = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 4);

        budget.GrantModuleShare(remainingModuleCount: 4);
        int firstShare = budget.DetailBytesAvailable;

        Assert.IsGreaterThan(0, firstShare);

        budget.GrantModuleShare(remainingModuleCount: 3);
        int afterSecondGrant = budget.DetailBytesAvailable;

        // A second, equal-sized grant against the (now smaller) remaining pool adds more allowance.
        Assert.IsGreaterThan(firstShare, afterSecondGrant);
    }

    [TestMethod]
    public void GrantModuleShare_LeavesUnspentAllowanceForLaterModules()
    {
        var budget = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 2);

        budget.GrantModuleShare(remainingModuleCount: 2);
        int firstModuleShare = budget.DetailBytesAvailable;

        // First module doesn't spend any of its share.
        budget.GrantModuleShare(remainingModuleCount: 1);
        int secondModuleAvailable = budget.DetailBytesAvailable;

        // The second (and last) module's grant absorbs the entire remaining ungranted pool, so it should
        // end up with at least as much as the first module got, since nothing was spent in between.
        Assert.IsGreaterThanOrEqualTo(firstModuleShare, secondModuleAvailable);
    }

    [TestMethod]
    public void GrantModuleShare_WithZeroRemainingModuleCount_DoesNotThrow_AndGrantsEntirePool()
    {
        var budget = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 1);

        // remainingModuleCount of 0 must not cause a division by zero; Math.Max(1, ...) guards it.
        budget.GrantModuleShare(remainingModuleCount: 0);

        Assert.IsGreaterThan(0, budget.DetailBytesAvailable);
    }

    [TestMethod]
    public void ForAggregate_ReservesOverheadPerModule_ReducingUngrantedPoolAsModuleCountGrows()
    {
        var fewModules = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 1);
        var manyModules = SummaryBudget.ForAggregate(consumedBytes: 0, moduleCount: 100);

        fewModules.GrantModuleShare(remainingModuleCount: 1);
        manyModules.GrantModuleShare(remainingModuleCount: 100);

        // More modules means more reserved overhead subtracted from the same total budget, so the
        // per-module detail share shrinks as module count grows.
        Assert.IsGreaterThan(manyModules.DetailBytesAvailable, fewModules.DetailBytesAvailable);
    }

    [TestMethod]
    public void ForAggregate_AlreadyConsumedBytes_ReflectedInStage()
    {
        var budget = SummaryBudget.ForAggregate(consumedBytes: StopListingLength, moduleCount: 4);

        Assert.AreEqual(SummaryStage.Unlisted, budget.Stage);
    }
}
