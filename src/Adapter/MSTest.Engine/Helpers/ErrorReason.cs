// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal sealed class ErrorReason(string message) : IErrorReason
{
    internal ErrorReason(string message, Exception exception)
        : this(message)
        => Exception = exception;

    internal ErrorReason(Exception exception)
        : this(exception.Message)
        => Exception = exception;

    public Exception? Exception { get; }

    public string Message { get; } = message;
}
