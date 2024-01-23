// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Polly;

namespace Microsoft.Testing.TestInfrastructure;

public class RetryHelper
{
    public static async Task RetryAsync(Func<Task> action, uint times, TimeSpan every, Func<Exception, bool>? predicate = null)
    {
        await Policy.Handle<Exception>(exception =>
        {
            if (predicate is null)
            {
                return true;
            }

            return predicate(exception);
        })
       .WaitAndRetryAsync((int)times, _ => every)
       .ExecuteAsync(action);
    }

    public static async Task<T> RetryAsync<T>(Func<Task<T>> action, uint times, TimeSpan every, Func<Exception, bool>? predicate = null)
    {
        return await Policy.Handle<Exception>(exception =>
        {
            if (predicate is null)
            {
                return true;
            }

            return predicate(exception);
        })
        .WaitAndRetryAsync((int)times, _ => every)
        .ExecuteAsync(action);
    }
}
