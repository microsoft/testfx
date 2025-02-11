// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// Helper to provide a uids to user data, using the same logic that DynamicDataAttribute.GetDisplayName is using. This class is called by source generator.
/// </summary>
public static class DynamicDataNameProvider
{
    /// <summary>
    /// Returns a stable fragment of uid by converting parameter types to strings, and suffixing them with index in brackets (e.g. [1]).
    /// </summary>
    /// <param name="parameterNames">Names of the parameters of the receiving method.</param>
    /// <param name="data">The data for each parameter.</param>
    /// <param name="index">Position in the collection.</param>
    /// <returns>Stable uid.</returns>
    /// <exception cref="ArgumentException">Arrays in both parameters need to have the same number of items.</exception>
    public static string GetUidFragment(string[] parameterNames, object?[] data, int index)
    {
        if (parameterNames.Length != data.Length)
        {
            throw new ArgumentException($"Parameter count mismatch. The provided data ({string.Join(", ", data.Select(d => d?.ToString() ?? "null"))}) have {data.Length} items, but there are {parameterNames.Length} parameters.");
        }

        StringBuilder stringBuilder = new StringBuilder().Append('(');

        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0)
            {
                stringBuilder.Append(", ");
            }

            stringBuilder.Append(parameterNames[i]).Append(": ").Append(data[i]?.ToString() ?? "null");
        }

        stringBuilder.Append(CultureInfo.InvariantCulture, $")[{index}]");
        return stringBuilder.ToString();
    }
}
