// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    private sealed class RemotingMessageLogger :
#if NETFRAMEWORK
        MarshalByRefObject,
#endif
        IMessageLogger
    {
        private readonly IMessageLogger _realMessageLogger;

        public RemotingMessageLogger(IMessageLogger messageLogger)
            => _realMessageLogger = messageLogger;

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
            => _realMessageLogger.SendMessage(testMessageLevel, message);
    }

    private void InitializeRandomTestOrder(IMessageLogger frameworkHandle)
    {
        if (!MSTestSettings.CurrentSettings.RandomizeTestOrder)
        {
            _testOrderRandom = null;
            return;
        }

        // Use Guid.NewGuid().GetHashCode() rather than new Random() so that consecutive runs do not
        // collide on the same seed on .NET Framework targets (where Random() is time-seeded with low
        // resolution). The user can still pin the seed via RandomTestOrderSeed for reproducibility.
        int seed = MSTestSettings.CurrentSettings.RandomTestOrderSeed ?? Guid.NewGuid().GetHashCode();
        _testOrderRandom = new Random(seed);

        frameworkHandle.SendMessage(
            TestMessageLevel.Informational,
            string.Format(CultureInfo.CurrentCulture, Resource.RandomTestOrderBanner, seed));
    }

    private static void Shuffle<T>(Random random, T[] items)
    {
        // Fisher-Yates shuffle. The array is mutated in place so that callers see a fully
        // materialized, deterministic order before any parallel work starts.
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
