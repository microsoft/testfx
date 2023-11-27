// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.TestInfrastructure;

public class RetryHelper
{
    public static async Task Retry(Func<Task> action, uint times, TimeSpan every, Func<Exception, bool>? predicate = null)
    {
        var exceptions = new List<Exception>();
        uint totalTries = times;
        while (times > 0)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                if (predicate is not null && !predicate(ex))
                {
                    throw;
                }

                if (times == 1)
                {
                    throw new RetryException(string.Format(CultureInfo.InvariantCulture, PlatformResources.RetryFailedAfterTriesErrorMessage, totalTries), exceptions.ToArray());
                }
            }

            await Task.Delay(every);
            times--;
        }
    }

    public static async Task<T> Retry<T>(Func<Task<T>> action, uint times, TimeSpan every, Func<Exception, bool>? predicate = null)
    {
        var exceptions = new List<Exception>();
        uint totalTries = times;
        while (times > 0)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                if (predicate is not null && !predicate(ex))
                {
                    throw;
                }

                if (times == 1)
                {
                    throw new RetryException(string.Format(CultureInfo.InvariantCulture, PlatformResources.RetryFailedAfterTriesErrorMessage, totalTries), exceptions.ToArray());
                }

                exceptions.Add(ex);
            }

            await Task.Delay(every);
            times--;
        }

        throw ApplicationStateGuard.Unreachable();
    }
}

public class RetryException : AggregateException
{
    public RetryException(string message, Exception[] inner)
        : base(message, inner)
    {
    }
}
