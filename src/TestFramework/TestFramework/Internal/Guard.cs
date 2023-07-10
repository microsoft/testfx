// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A guard helper class to help validate preconditions.
/// </summary>
internal static class Guard
{
    internal static void IsNotNull(object? param, string parameterName, string? message)
    {
        if (param is not null)
        {
            return;
        }

        if (message is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        throw new ArgumentNullException(parameterName, message);
    }

    internal static void IsNotNullOrEmpty(string? argument, string parameterName, string message)
    {
        if (StringEx.IsNullOrEmpty(argument))
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}
