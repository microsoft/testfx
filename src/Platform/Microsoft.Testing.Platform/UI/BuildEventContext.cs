// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal sealed class BuildEventContext
{
    public int WorkerId { get; internal set; }

    public string Assembly { get; internal set; }
}
