// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal interface IMessage
{
    MessageSeverity Severity { get; }

    object Message { get; }
}

internal enum MessageSeverity
{
    /// <summary>
    /// Error.
    /// </summary>
    Error,

    /// <summary>
    /// Warning.
    /// </summary>
    Warning,
}
