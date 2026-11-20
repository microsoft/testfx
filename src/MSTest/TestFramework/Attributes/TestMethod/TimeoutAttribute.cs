// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Timeout attribute; used to specify the timeout of a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TimeoutAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class.
    /// </summary>
    /// <param name="timeout">
    /// The timeout in milliseconds.
    /// </param>
    public TimeoutAttribute(int timeout) => Timeout = timeout;

    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public int Timeout { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the test method should be cooperatively canceled on timeout.
    /// </summary>
    /// <remarks>
    /// By default, MSTest cancels the test's cancellation token and reports a timeout without waiting for the test method
    /// to finish. The method may continue running and interfere with test cleanup.
    /// If cooperative cancellation is enabled here or in the run configuration, MSTest cancels the token on timeout
    /// and waits for the method to finish. Code that ignores cancellation can keep MSTest waiting indefinitely.
    /// An explicit value on this attribute overrides the run configuration.
    /// </remarks>
    public bool CooperativeCancellation
    {
        get;
        set
        {
            // Attributes don't allow nullable boolean, so we need this to know that the value was set explicitly.
            IsCooperativeCancellationSet = true;
            field = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the instance has the correct test timeout signature.
    /// </summary>
    internal bool HasCorrectTimeout => Timeout > 0;

    internal bool IsCooperativeCancellationSet { get; private set; }
}
