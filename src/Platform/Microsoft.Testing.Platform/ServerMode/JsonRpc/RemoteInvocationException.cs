// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class RemoteInvocationException : Exception
{
    public RemoteInvocationException(int errorCode, string errorMessage, object? errorData)
        : base(CreateMessage(errorCode, errorMessage, errorData))
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ErrorData = errorData;
    }

    public int ErrorCode { get; }

    public string ErrorMessage { get; }

    public object? ErrorData { get; }

    private static string CreateMessage(int errorCode, string errorMessage, object? errorData)
        => errorData is null
            ? $"Remote invocation failed with error code '{errorCode}': {errorMessage}"
            : $"Remote invocation failed with error code '{errorCode}': {errorMessage} Data: {errorData}";
}
