// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class CombinatorialValuesUtilities
{
    internal static IEnumerable<object?> GetValuesFor(ParameterInfo parameter, out ICombinatorialValuesProvider? valuesSource)
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        ICombinatorialValuesProvider[] valueSources = parameter.GetCustomAttributes()
            .OfType<ICombinatorialValuesProvider>()
            .ToArray();
        valuesSource = valueSources.Length switch
        {
            0 => null,
            1 => valueSources[0],
            _ => throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialMultipleValueProviders,
                    parameter.Name,
                    parameter.Member.Name,
                    string.Join(", ", valueSources.Select(provider => provider.GetType().Name)),
                    nameof(ICombinatorialValuesProvider)),
                nameof(parameter)),
        };
        return valuesSource is null
            ? GetValuesFor(parameter.ParameterType)
            : valuesSource.GetValues(parameter);
    }

    internal static object? GetValueForTestCase(ParameterInfo parameter, ICombinatorialValuesProvider? valuesSource, object?[] candidateValues, int candidateIndex)
    {
        if (valuesSource is not (CombinatorialMemberDataAttribute or CombinatorialClassDataAttribute))
        {
            return candidateValues[candidateIndex];
        }

        object?[] freshValues = valuesSource.GetValues(parameter);
        return freshValues.Length != candidateValues.Length
            ? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialProviderValueCountChanged,
                    parameter.Name,
                    candidateValues.Length,
                    freshValues.Length))
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
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialUnableToInferValues,
                    dataType,
                    nameof(ICombinatorialValuesProvider)));
        }
    }
}
