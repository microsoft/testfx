// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal abstract class StopwatchAbstraction
{
    public abstract void Start();

    public abstract void Stop();

    public abstract TimeSpan Elapsed { get; }
}
