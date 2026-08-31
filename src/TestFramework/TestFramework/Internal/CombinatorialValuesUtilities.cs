// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class CombinatorialValuesUtilities
{
    internal static IEnumerable<object?> GetValuesFor(ParameterInfo parameter)
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        ICombinatorialValuesProvider? valuesSource = parameter.GetCustomAttributes()
            .OfType<ICombinatorialValuesProvider>()
            .SingleOrDefault();
        return valuesSource is not null
            ? valuesSource.GetValues(parameter)
            : GetValuesFor(parameter.ParameterType);
    }

    internal static object? GetValueForTestCase(ParameterInfo parameter, object?[] candidateValues, int candidateIndex)
    {
        CombinatorialMemberDataAttribute? memberData = parameter.GetCustomAttributes()
            .OfType<CombinatorialMemberDataAttribute>()
            .SingleOrDefault();
        if (memberData is null)
        {
            return candidateValues[candidateIndex];
        }

        object?[] freshValues = memberData.GetValues(parameter);
        return freshValues.Length != candidateValues.Length
            ? throw new InvalidOperationException(
                $"Member data for parameter '{parameter.Name}' returned {candidateValues.Length} values when determining combinations, but {freshValues.Length} values when creating a test case.")
            : freshValues[candidateIndex];
    }

    private static IEnumerable<object?> GetValuesFor(Type dataType)
    {
        if (dataType == typeof(bool))
        {
            yield return true;
            yield return false;
        }
        else if (dataType == typeof(int))
        {
            yield return 0;
            yield return 1;
        }
        else if (dataType.GetTypeInfo().IsEnum)
        {
            foreach (string name in Enum.GetNames(dataType))
            {
                yield return Enum.Parse(dataType, name);
            }
        }
        else if (Nullable.GetUnderlyingType(dataType) is Type underlyingType)
        {
            yield return null;
            foreach (object? value in GetValuesFor(underlyingType))
            {
                yield return value;
            }
        }
        else
        {
            throw new NotSupportedException(
                $"Unable to automatically generate values for parameter of type {dataType}. Apply an attribute that implements {nameof(ICombinatorialValuesProvider)} to specify the values.");
        }
    }
}
