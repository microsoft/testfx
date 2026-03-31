// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal sealed class Result
{
    private readonly List<IReason> _reasons = [];

    private Result()
    {
    }

    public bool IsSuccess => !IsFailed;

    public bool IsFailed => _reasons.OfType<IErrorReason>().Any();

    public IReadOnlyList<IReason> Reasons => _reasons;

    public Result WithSuccess(ISuccessReason success)
    {
        _reasons.Add(success);
        return this;
    }

    public Result WithWarning(IWarningReason warning)
    {
        _reasons.Add(warning);
        return this;
    }

    public Result WithError(IErrorReason error)
    {
        _reasons.Add(error);
        return this;
    }

    public static Result Ok() => new();

    public static Result Ok(string reason) => new Result().WithSuccess(new SuccessReason(reason));

    public static Result Fail(Exception exception) => new Result().WithError(new ErrorReason(exception));

    public static Result Fail(string reason) => new Result().WithError(new ErrorReason(reason));

    public static Result Fail(string reason, Exception exception) => new Result().WithError(new ErrorReason(reason, exception));

    public static Result Combine(IEnumerable<Result> results)
    {
        Result result = Ok();
        foreach (Result r in results)
        {
            result._reasons.AddRange(r.Reasons);
        }

        return result;
    }
}
