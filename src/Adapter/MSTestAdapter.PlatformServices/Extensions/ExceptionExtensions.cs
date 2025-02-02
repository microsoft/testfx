// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

/// <summary>
/// Extension methods for the exception class.
/// </summary>
internal static class ExceptionExtensions
{
    /// <summary>
    /// Returns an exception message with all inner exceptions messages.
    /// </summary>
    /// <param name="exception"> The exception. </param>
    /// <returns> Custom exception message that includes inner exceptions. </returns>
    internal static string GetExceptionMessage(this Exception exception)
    {
        var builder = new StringBuilder(exception.Message);
        Exception? inner = exception.InnerException;
        while (inner != null)
        {
            builder.AppendLine();
            builder.AppendLine(inner.Message);
            inner = inner.InnerException;
        }

        return builder.ToString();
    }

    internal static bool IsOperationCanceledExceptionFromToken(this Exception ex, CancellationToken cancellationToken)
        => (ex is OperationCanceledException oce && oce.CancellationToken == cancellationToken)
        || (ex is AggregateException aggregateEx && aggregateEx.InnerExceptions.OfType<OperationCanceledException>().Any(oce => oce.CancellationToken == cancellationToken));
}
