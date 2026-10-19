// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class TestDataSourceHelpers
{
    internal static int GetDataParameterCount(ParameterInfo[] parameters)
        => parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken)
            ? parameters.Length - 1
            : parameters.Length;

    public static bool IsDataConsideredSingleArgumentValue(object?[]? data, ParameterInfo[] parameters)
        => data is not null
            && (data.Length != 1 || data[0] is not null)
            && GetDataParameterCount(parameters) == 1
            && parameters[0].ParameterType == typeof(object[])
            && !IsDataWithExplicitCancellationToken(data, parameters)
            && !IsSingleTupleArgumentWithExplicitCancellationToken(data, parameters);

    // A single-element data array whose element is itself a tuple matching [dataParameter, CancellationToken]
    // is considered a full-arity row supplying its own token, not an object[] data value to wrap.
    private static bool IsSingleTupleArgumentWithExplicitCancellationToken(object?[] data, ParameterInfo[] parameters)
        => parameters.Length == 2
            && data.Length == 1
            && TryHandleTupleDataSource(data[0], parameters, out object?[] tupleArguments)
            && IsDataWithExplicitCancellationToken(tupleArguments, parameters);

    public static bool TryHandleITestDataRow(
        object?[] d,
        ParameterInfo[] testMethodParameters,
        out object?[] data,
        out string? ignoreMessageFromTestDataRow,
        out string? displayNameFromTestDataRow)
        => TryHandleITestDataRow(d, testMethodParameters, out data, out ignoreMessageFromTestDataRow, out displayNameFromTestDataRow, out _);

    public static bool TryHandleITestDataRow(
        object?[] d,
        ParameterInfo[] testMethodParameters,
        out object?[] data,
        out string? ignoreMessageFromTestDataRow,
        out string? displayNameFromTestDataRow,
        out IList<string>? testCategoriesFromTestDataRow)
        => TryHandleITestDataRow(d, testMethodParameters, out data, out ignoreMessageFromTestDataRow, out displayNameFromTestDataRow, out testCategoriesFromTestDataRow, out _);

    public static bool TryHandleITestDataRow(
        object?[] d,
        ParameterInfo[] testMethodParameters,
        out object?[] data,
        out string? ignoreMessageFromTestDataRow,
        out string? displayNameFromTestDataRow,
        out IList<string>? testCategoriesFromTestDataRow,
        out string? argumentsDisplayNameFromTestDataRow)
    {
        if (d.Length == 1 && d[0] is ITestDataRow testDataRow)
        {
            object? dataFromTestDataRow = testDataRow.Value;
            ignoreMessageFromTestDataRow = testDataRow.IgnoreMessage;
            displayNameFromTestDataRow = testDataRow.DisplayName;
            testCategoriesFromTestDataRow = testDataRow.TestCategories;
            argumentsDisplayNameFromTestDataRow = testDataRow.ArgumentsDisplayName;

            data = TryHandleTupleDataSource(dataFromTestDataRow, testMethodParameters, out object?[] tupleExpandedToArray)
                ? tupleExpandedToArray
                : [dataFromTestDataRow];

            return true;
        }

        data = d;
        ignoreMessageFromTestDataRow = null;
        displayNameFromTestDataRow = null;
        testCategoriesFromTestDataRow = null;
        argumentsDisplayNameFromTestDataRow = null;
        return false;
    }

    public static bool TryHandleTupleDataSource(object? data, ParameterInfo[] testMethodParameters, out object?[] array)
    {
        bool isSingleDataArgument = GetDataParameterCount(testMethodParameters) == 1
            && testMethodParameters[0].ParameterType?.IsAssignableFrom(data?.GetType()) is true;
        if (isSingleDataArgument && testMethodParameters.Length == 1)
        {
            array = [];
            return false;
        }

        if (!TryExpandTupleDataSource(data, out array))
        {
            return false;
        }

        // A valid full-arity row supplies its own token, even when the tuple itself fits the data parameter.
        if (isSingleDataArgument && !IsDataWithExplicitCancellationToken(array, testMethodParameters))
        {
            array = [];
            return false;
        }

        return true;
    }

    private static bool IsDataWithExplicitCancellationToken(object?[] data, ParameterInfo[] parameters)
        => parameters.Length == 2
            && parameters[1].ParameterType == typeof(CancellationToken)
            && data.Length == 2
            && (data[0] is null || parameters[0].ParameterType.IsInstanceOfType(data[0]))
            && (data[1] is null or CancellationToken || data[1] == Type.Missing);

    private static bool TryExpandTupleDataSource(object? data, out object?[] array)
    {
#if NET471_OR_GREATER || NETCOREAPP
        if (data is ITuple tuple)
        {
            array = new object?[tuple.Length];
            for (int i = 0; i < tuple.Length; i++)
            {
                array[i] = tuple[i]!;
            }

            return true;
        }
#else
        if (IsTupleOrValueTuple(data, out int tupleSize))
        {
            array = new object?[tupleSize];
            ProcessTuple(data, array, 0);

            return true;
        }

        static object GetFieldOrProperty(Type type, object data, string fieldOrPropertyName)
            // ValueTuple is a value type, and uses fields for Items.
            // Tuple is a reference type, and uses properties for Items.
            => type.IsValueType
                ? type.GetField(fieldOrPropertyName).GetValue(data)
                : type.GetProperty(fieldOrPropertyName).GetValue(data);

        static void ProcessTuple(object data, object?[] array, int startingIndex)
        {
            Type type = data.GetType();
            int tupleSize = type.GenericTypeArguments.Length;
            for (int i = 0; i < tupleSize; i++)
            {
                if (i != 7)
                {
                    // Note: ItemN are properties on Tuple, but are fields on ValueTuple
                    array[startingIndex + i] = GetFieldOrProperty(type, data, $"Item{i + 1}");
                    continue;
                }

                object rest = GetFieldOrProperty(type, data, "Rest");
                if (IsTupleOrValueTuple(rest, out _))
                {
                    ProcessTuple(rest, array, startingIndex + 7);
                }
                else
                {
                    array[startingIndex + i] = rest;
                }

                return;
            }
        }
#endif

        array = [];
        return false;
    }

#if !NET471_OR_GREATER && !NETCOREAPP
    private static bool IsTupleOrValueTuple([NotNullWhen(true)] object? data, out int tupleSize)
    {
        tupleSize = 0;

        if (data is null)
        {
            return false;
        }

        Type type = data.GetType();
        if (!type.IsGenericType)
        {
            return false;
        }

        Type genericTypeDefinition = type.GetGenericTypeDefinition();

        if (genericTypeDefinition == typeof(Tuple<>) ||
            genericTypeDefinition == typeof(Tuple<,>) ||
            genericTypeDefinition == typeof(Tuple<,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,,,>))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }

        if (genericTypeDefinition == typeof(Tuple<,,,,,,,>))
        {
            object? last = type.GetProperty("Rest").GetValue(data);
            if (IsTupleOrValueTuple(last, out int restSize))
            {
                tupleSize = 7 + restSize;
                return true;
            }
            else
            {
                tupleSize = 8;
                return true;
            }
        }

#if NET462
        if (genericTypeDefinition.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            tupleSize = type.GetGenericArguments().Length;
            if (tupleSize == 8)
            {
                object? last = type.GetField("Rest").GetValue(data);
                if (IsTupleOrValueTuple(last, out int restSize))
                {
                    tupleSize = 7 + restSize;
                }
            }

            return true;
        }
#else
        if (genericTypeDefinition == typeof(ValueTuple<>) ||
            genericTypeDefinition == typeof(ValueTuple<,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,,>))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }

        if (genericTypeDefinition == typeof(ValueTuple<,,,,,,,>))
        {
            object? last = type.GetField("Rest").GetValue(data);
            if (IsTupleOrValueTuple(last, out int restSize))
            {
                tupleSize = 7 + restSize;
                return true;
            }
            else
            {
                tupleSize = 8;
                return true;
            }
        }
#endif

        return false;
    }
#endif
}
