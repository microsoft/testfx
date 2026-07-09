// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    /// <summary>
    /// A message logger that can be marshaled into the isolation (child app-domain) host so log messages
    /// raised while running a test cross back to the parent-domain <see cref="IAdapterMessageLogger"/>.
    /// Marshaling is by reference (the wrapped logger stays in the parent domain), so no VSTest object-model
    /// type is required in the child domain.
    /// </summary>
    private sealed class RemotingMessageLogger :
#if NETFRAMEWORK
        MarshalByRefObject,
#endif
        IAdapterMessageLogger
    {
        private readonly IAdapterMessageLogger _realMessageLogger;

        public RemotingMessageLogger(IAdapterMessageLogger messageLogger)
            => _realMessageLogger = messageLogger;

        public void SendMessage(MessageLevel level, string message)
            => _realMessageLogger.SendMessage(level, message);
    }

    private void InitializeRandomTestOrder(IAdapterMessageLogger messageLogger)
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

        messageLogger.SendMessage(
            MessageLevel.Informational,
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
