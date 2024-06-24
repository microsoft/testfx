// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Timeout attribute; used to specify the timeout of a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TimeoutAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class.
    /// </summary>
    /// <param name="timeout">
    /// The timeout in milliseconds.
    /// </param>
    public TimeoutAttribute(int timeout)
    {
        Timeout = timeout;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class with a preset timeout.
    /// </summary>
    /// <param name="timeout">
    /// The timeout.
    /// </param>
    public TimeoutAttribute(TestTimeout timeout)
    {
        Timeout = (int)timeout;
    }

    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public int Timeout { get; }
}
