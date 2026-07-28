// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TerminalProgressMessageState
{
    public TerminalProgressMessageState(long id, long version, string text)
    {
        Id = id;
        Version = version;
        Text = text;
    }

    public long Id { get; }

    public long Version { get; }

    public string Text { get; }
}
