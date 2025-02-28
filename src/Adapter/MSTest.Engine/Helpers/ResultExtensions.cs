// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal static class ResultExtensions
{
    public static Result WithWarning(this Result result, string message)
        => result.WithWarning(new WarningReason(message));

    public static Result WithError(this Result result, string message)
        => result.WithError(new ErrorReason(message));

    public static Result WithError(this Result result, Exception exception)
        => result.WithError(new ErrorReason(exception));
}
