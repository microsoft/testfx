// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Timeout attribute; used to specify the timeout of a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TimeoutAttribute : Attribute
{
    private bool _isCooperativeCancellation;

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

    /// <summary>
    /// Gets or sets a value indicating whether the method should be cooperatively cancelled on timeout.
    /// When set to <see langword="true"/>, the method should be designed to cooperatively cancel itself when the timeout is reached.
    /// Otherwise, when set to <see langword="false"/>, the task method will be unobserved.
    /// </summary>
    public bool CooperativeCancellation
    {
        get => _isCooperativeCancellation;
        set
        {
            IsCooperativeCancellationSet = true;
            _isCooperativeCancellation = value;
        }
    }

    internal bool IsCooperativeCancellationSet { get; private set; }
}
