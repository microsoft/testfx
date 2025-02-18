// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Runtime.Serialization;

namespace Microsoft.Testing.Framework;

/// <summary>
/// AssertFailedException class. Used to indicate failure for a test case.
/// </summary>
[Serializable]
public sealed class AssertFailedException : Exception
{
    /// <summary>
    /// Creates AssertFailedException with a given message and metadata.
    /// </summary>
    /// <param name="message">Message to be reported to the user.</param>
    /// <returns>AssertFailedException.</returns>
    internal static AssertFailedException Create(string message) => CreateInternal(message, expected: null, actual: null);

    /// <summary>
    /// Creates AssertFailedException with a given message and metadata.
    /// </summary>
    /// <param name="message">Message to be reported to the user, it may, or may not include the values that were compared. If values are not included provide them to 'expected' and 'actual'.</param>
    /// <param name="expected">The expected value, when that value is complex, and diffing it to actual makes it easier for user to see the difference.</param>
    /// <param name="actual">The actual value, when that value is complex, and diffing it to expected makes it easier for user to see the difference.</param>
    /// <returns>AssertFailedException.</returns>
    internal static AssertFailedException Create(string message, string expected, string actual) => CreateInternal(message, expected, actual);

    private static AssertFailedException CreateInternal(string message, string? expected, string? actual)
    {
#pragma warning disable SYSLIB0051 // Type or member is obsolete
        var ex = new AssertFailedException(message);
#pragma warning restore SYSLIB0051 // Type or member is obsolete

        if (expected != null)
        {
            ex.Data["assert.expected"] = expected;
        }

        if (actual != null)
        {
            ex.Data["assert.actual"] = actual;
        }

        return ex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
    /// </summary>
#if NET8_0_OR_GREATER
    [Obsolete("Use Create instead", DiagnosticId = "SYSLIB0051")]
#endif
    public AssertFailedException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
#if NET8_0_OR_GREATER
    [Obsolete("Use Create instead", DiagnosticId = "SYSLIB0051")]
#endif
    public AssertFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="ex">The inner exception.</param>
#if NET8_0_OR_GREATER
    [Obsolete("Use Create instead", DiagnosticId = "SYSLIB0051")]
#endif
    public AssertFailedException(string message, Exception ex)
        : base(message, ex)
    {
    }

#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
    private AssertFailedException(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
    }
}
