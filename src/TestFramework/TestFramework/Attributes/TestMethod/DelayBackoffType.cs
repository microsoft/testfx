// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies a backoff type for the delay between retries.
/// </summary>
public enum DelayBackoffType
{
    /// <summary>
    /// Specifies a constant backoff type. Meaning the delay between retries is constant.
    /// </summary>
    Constant,

    /// <summary>
    /// Specifies an exponential backoff type.
    /// The delay is calculated as the base delay * 2^(n-1) where n is the retry attempt.
    /// For example, if the base delay is 1000ms, the delays will be 1000ms, 2000ms, 4000ms, 8000ms, etc.
    /// </summary>
    Exponential,
}
