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
    public TimeoutAttribute(int timeout) => Timeout = timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class with a preset timeout.
    /// </summary>
    /// <param name="timeout">
    /// The timeout.
    /// </param>
    public TimeoutAttribute(TestTimeout timeout) => Timeout = (int)timeout;

    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public int Timeout { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the test method should be cooperatively canceled on timeout.
    /// When set to <see langword="true"/>, the cancellation token is canceled on timeout, and the method completion is awaited.
    /// The test method and all the code it calls, must be designed in a way that it observes the cancellation and cancels
    /// cooperatively. If the test method does not complete, the timeout does not force it to complete.
    /// When set to <see langword="false"/>, the cancellation token is canceled on timeout, timeout result is reported and the
    /// method task will continue running on background. This may lead to conflicts in file access on test cleanup, unobserved
    /// exceptions, and memory leaks.
    /// </summary>
    public bool CooperativeCancellation
    {
        get => _isCooperativeCancellation;
        set
        {
            // Attributes don't allow nullable boolean, so we need this to know that the value was set explicitly.
            IsCooperativeCancellationSet = true;
            _isCooperativeCancellation = value;
        }
    }

    internal bool IsCooperativeCancellationSet { get; private set; }
}
