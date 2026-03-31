// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies the severity level of messages displayed using the <see cref="TestContext.DisplayMessage(MessageLevel, string)"/> API.
/// </summary>
public enum MessageLevel
{
    /// <summary>
    /// The message will be displayed as informational, typically used for general updates or non-critical messages.
    /// </summary>
    Informational,

    /// <summary>
    /// The message will be displayed as a warning, indicating a potential issue or something requiring attention.
    /// </summary>
    Warning,

    /// <summary>
    /// The message will be displayed as an error, representing a significant issue or failure.
    /// </summary>
    Error,
}
