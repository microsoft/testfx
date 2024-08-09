// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A guard helper class to help validate preconditions.
/// </summary>
internal static class Guard
{
    internal static void IsNotNull(object? param, string parameterName)
    {
        if (param is not null)
        {
            return;
        }

        throw new ArgumentNullException(parameterName);
    }
}
