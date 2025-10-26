// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class ExceptionFlattener
{
    internal static (string? CustomErrorMessage, Exception?[] Exceptions) Flatten(string? errorMessage, Exception? exception)
    {
        if (errorMessage is null && exception is null)
        {
            return (null, []);
        }

        if (exception is null)
        {
            // Only have an error message, no exception
            return (errorMessage, []);
        }

        List<Exception> exceptions = [exception];

        // Add all inner exceptions. This will flatten top level AggregateExceptions,
        // and all AggregateExceptions that are directly in AggregateExceptions, but won't expand
        // AggregateExceptions that are in non-aggregate exception inner exceptions.
        IEnumerable<Exception?> aggregateExceptions = exception switch
        {
            AggregateException aggregate => aggregate.Flatten().InnerExceptions,
            _ => [exception.InnerException],
        };

        foreach (Exception? aggregate in aggregateExceptions)
        {
            Exception? currentException = aggregate;
            while (currentException is not null)
            {
                exceptions.Add(currentException);
                currentException = currentException.InnerException;
            }
        }

        // Use custom error message if provided and not whitespace, otherwise null
        string? customMessage = !RoslynString.IsNullOrWhiteSpace(errorMessage) ? errorMessage : null;
        return (customMessage, [.. exceptions]);
    }
}
