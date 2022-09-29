// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

/// <summary>
/// Timeout attribute; used to specify the timeout of a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TimeoutAttribute : Attribute
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class.
    /// </summary>
    /// <param name="timeout">
    /// The timeout in milliseconds.
    /// </param>
    /// <param name="cleanupTimeout">
    /// The cleanup timeout in milliseconds.
    /// This represents the time allowed for the cleanup (TestCleanup, Dispose) to complete when the test times out.
    /// The default value is infinite, meaning that all cleanup will be called and left infinite time to process.
    /// </param>
    public TimeoutAttribute(int timeout, int cleanupTimeout = int.MaxValue)
    {
        Timeout = timeout;
        CleanupTimeout = cleanupTimeout;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class with a preset timeout
    /// </summary>
    /// <param name="timeout">
    /// The timeout
    /// </param>
    public TimeoutAttribute(TestTimeout timeout)
        : this((int)timeout)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public int Timeout { get; }

    /// <summary>
    /// Gets the cleanup timeout in milliseconds.
    /// This represents the time allowed for the cleanup (TestCleanup, Dispose) to complete when the test times out.
    /// </summary>
    public int CleanupTimeout { get; }

    #endregion
}
