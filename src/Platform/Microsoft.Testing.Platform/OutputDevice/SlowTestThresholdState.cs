// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class SlowTestThresholdState
{
    private long _nextThresholdTicks;

    internal SlowTestThresholdState(TimeSpan initialThreshold)
        => _nextThresholdTicks = initialThreshold.Ticks;

    internal bool IsDue(TimeSpan elapsed)
    {
        long elapsedTicks = elapsed.Ticks;
        if (_nextThresholdTicks <= 0 || elapsedTicks < _nextThresholdTicks)
        {
            return false;
        }

        while (_nextThresholdTicks <= elapsedTicks)
        {
            if (_nextThresholdTicks > TimeSpan.MaxValue.Ticks / 2)
            {
                _nextThresholdTicks = TimeSpan.MaxValue.Ticks;
                break;
            }

            _nextThresholdTicks *= 2;
        }

        return true;
    }
}
