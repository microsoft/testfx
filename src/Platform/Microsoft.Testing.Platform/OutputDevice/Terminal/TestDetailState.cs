// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class TestDetailState
{
    public TestDetailState(long id, long version, IStopwatch stopwatch, string text)
    {
        Id = id;
        Version = version;
        Stopwatch = stopwatch;
        Text = text;
    }

    public long Id { get; }

    public long Version { get; }

    public IStopwatch Stopwatch { get; }

    public string Text { get; }
}

