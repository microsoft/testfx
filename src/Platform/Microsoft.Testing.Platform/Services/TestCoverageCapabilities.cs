// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestCoverageCapabilities : ITestCoverageCapabilities
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly List<string> _enabledProducerUids = [];

    public bool SupportsTestCoverageMessages => true;

    public IReadOnlyCollection<string> EnabledProducerUids
    {
        get
        {
            lock (_lock)
            {
                return [.. _enabledProducerUids];
            }
        }
    }

    public void RegisterProducer(IDataProducer producer)
    {
        Type[] dataTypesProduced = producer.DataTypesProduced;
        if (Array.IndexOf(dataTypesProduced, typeof(TestCoverageMessage)) < 0
            && Array.IndexOf(dataTypesProduced, typeof(TestCoverageThresholdMessage)) < 0
            && Array.IndexOf(dataTypesProduced, typeof(TestCoverageReportMessage)) < 0)
        {
            return;
        }

        lock (_lock)
        {
            if (!_enabledProducerUids.Contains(producer.Uid, StringComparer.Ordinal))
            {
                _enabledProducerUids.Add(producer.Uid);
            }
        }
    }

    public void RegisterProducers(IEnumerable<IDataConsumer> dataConsumers)
    {
        foreach (IDataProducer producer in dataConsumers.OfType<IDataProducer>())
        {
            RegisterProducer(producer);
        }
    }
}
