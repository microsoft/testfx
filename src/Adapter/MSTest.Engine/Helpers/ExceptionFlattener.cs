// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.Helpers;

internal static class ExceptionFlattener
{
    /// <summary>
    /// Returns the same exception for any exception that is not AggregateException.
    /// For AggregateException it unwraps it when it holds just a single concrete exception,
    /// otherwise it flattens the AggregateException and returns that.
    /// </summary>
    public static Exception FlattenOrUnwrap(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            if (aggregateException.InnerExceptions.Count == 1)
            {
                Exception innerException = aggregateException.InnerExceptions[0];
                if (innerException is not AggregateException)
                {
                    return innerException;
                }
            }

            return aggregateException.Flatten();
        }

        return exception;
    }
}
