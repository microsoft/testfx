// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal class ExceptionFlattener
{
    internal static FlatException Flatten(string? errorMessage, Exception? exception)
    {
        if (errorMessage is null && exception is null)
        {
            return FlatException.Empty;
        }

        List<Exception> exceptions = new();

        List<Exception?> allExceptions = new()
        {
            // Add first exception or null to make space for it.
            exception,
        };

        // Add all inner exceptions. This will flatten top level AggregateExceptions,
        // and all AggregateExceptions that are directly in AggregateExceptions, but won't expand
        // AggregateExceptions that are in non-aggregate exception inner exceptions.
        IEnumerable<Exception?> aggregateExceptions = exception switch
        {
            AggregateException aggregate => aggregate.Flatten().InnerExceptions,
            _ => [exception?.InnerException],
        };

        foreach (Exception? aggregate in aggregateExceptions)
        {
            Exception? currentException = aggregate;
            while (currentException is not null)
            {
                allExceptions.Add(currentException);
                currentException = currentException.InnerException;
            }
        }

        string?[] errorMessages = allExceptions.Select(static e => e?.Message).ToArray();
        if (!RoslynString.IsNullOrWhiteSpace(errorMessage) && errorMessages.Length > 0)
        {
            errorMessages[0] = errorMessage;
        }

        var flatException = new FlatException
        {
            ErrorMessages = errorMessages,
            ErrorTypes = allExceptions.Select(e => e?.GetType().FullName).ToArray(),
            StackTraces = allExceptions.Select(static e => e?.StackTrace).ToArray(),
        };

        return flatException;
    }
}

internal class FlatException
{
    public string?[]? ErrorMessages { get; init; }

    public string?[]? ErrorTypes { get; init; }

    public string?[]? StackTraces { get; init; }

    public static FlatException Empty { get; } = new FlatException { ErrorMessages = null, ErrorTypes = null, StackTraces = null };
}
