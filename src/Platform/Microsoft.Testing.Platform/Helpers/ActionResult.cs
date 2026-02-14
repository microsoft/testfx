// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class ActionResult
{
    public static ActionResult<TResult> Ok<TResult>(TResult result)
        => new(true, result);

    public static ActionResult<TResult> Fail<TResult>()
        => new(false, default);
}

internal sealed class ActionResult<TResult>
{
    internal ActionResult(bool isSuccess, TResult? result)
    {
        IsSuccess = isSuccess;
        Result = result;
    }

    [MemberNotNullWhen(true, nameof(Result))]
    public bool IsSuccess { get; }

    public TResult? Result { get; }

    public static implicit operator ActionResult<TResult>(TResult result)
        => new(true, result);
}
