// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

internal enum MSTestAnalysisMode
{
    /// <summary>
    /// Disables all analyzers. User will need to opt-in to all analyzers.
    /// </summary>
    None,

    /// <summary>
    /// Follow the current behavior.
    /// Rules that are enabled by default as warnings remain enabled as warnings. Anything else isn't enabled by default.
    /// </summary>
    Default,

    /// <summary>
    /// Enables everything from Default, plus anything that's enabled by default as "info" will be elevated to warning.
    /// </summary>
    Recommended,

    /// <summary>
    /// Enables all rules as build warnings, including those that are disabled by default. We may decide
    /// to exclude very specific rules even in "All" mode.
    /// </summary>
    All,
}
