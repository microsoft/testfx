// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// An enumeration to specify the level of the messages to be displayed when using TestContext.DisplayMessage API.
/// </summary>
public enum MessageLevel
{
    /// <summary>
    /// The message will be displayed in informational level.
    /// </summary>
    Informational,

    /// <summary>
    /// The message will be displayed in warning level.
    /// </summary>
    Warning,

    /// <summary>
    /// The message will be displayed in error level.
    /// </summary>
    Error,
}
