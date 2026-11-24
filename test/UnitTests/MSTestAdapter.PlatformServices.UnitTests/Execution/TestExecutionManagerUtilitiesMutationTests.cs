// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerUtilitiesMutationTests : TestContainer
{
    private const int Seed = 123;

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            MSTestSettings.Reset();
        }
    }

    public void InitializeRandomTestOrderAnnouncesTheSeedThroughTheMessageLogger()
    {
        MSTestSettings.Reset();
        MSTestSettings.PopulateSettings(
            $"""
            <RunSettings>
              <MSTest>
                <RandomizeTestOrder>true</RandomizeTestOrder>
                <RandomTestOrderSeed>{Seed}</RandomTestOrderSeed>
              </MSTest>
            </RunSettings>
            """,
            logger: null,
            configuration: null);
        var messageLogger = new Mock<IAdapterMessageLogger>();
        var manager = new TestExecutionManager();

        typeof(TestExecutionManager)
            .GetMethod("InitializeRandomTestOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, [messageLogger.Object]);

        messageLogger.Verify(
            logger => logger.SendMessage(MessageLevel.Informational, string.Format(CultureInfo.CurrentCulture, Resource.RandomTestOrderBanner, Seed)),
            Times.Once);
        messageLogger.VerifyNoOtherCalls();
        object? random = typeof(TestExecutionManager).GetField("_testOrderRandom", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager);
        random.Should().BeOfType<Random>().Which.Next().Should().Be(new Random(Seed).Next());
    }

    public void ShuffleReordersTheItemsAccordingToTheRandomSequence()
    {
        int[] items = [0, 1, 2, 3, 4, 5, 6, 7];

        Shuffle(new Random(Seed), items);

        items.Should().Equal(1, 3, 0, 2, 5, 4, 6, 7);
    }

    public void ShuffleDrawsExactlyOneRandomValueForEachPositionAfterTheFirstOne()
    {
        var random = new Random(Seed);

        Shuffle(random, [0, 1, 2, 3, 4, 5, 6, 7]);

        // Seven draws were consumed (positions 7 to 1); drawing for position 0 as well would shift this value.
        random.Next().Should().Be(320746911);
    }

    private static void Shuffle(Random random, int[] items)
        => typeof(TestExecutionManager)
            .GetMethod("Shuffle", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int))
            .Invoke(null, [random, items]);
}
