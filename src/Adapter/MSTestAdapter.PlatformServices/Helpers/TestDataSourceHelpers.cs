// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class TestDataSourceHelpers
{
    public static bool IsDataConsideredSingleArgumentValue(object?[]? data, ParameterInfo[] parameters)
        => data is not null
            && (data.Length != 1 || data[0] is not null)
            && parameters.Length == 1
            && parameters[0].ParameterType == typeof(object[]);

    public static bool TryHandleITestDataRow(
        object?[] d,
        ParameterInfo[] testMethodParameters,
        out object?[] data,
        out string? ignoreMessageFromTestDataRow,
        out string? displayNameFromTestDataRow)
    {
        if (d is [ITestDataRow testDataRow])
        {
            object? dataFromTestDataRow = testDataRow.Value;
            ignoreMessageFromTestDataRow = testDataRow.IgnoreMessage;
            displayNameFromTestDataRow = testDataRow.DisplayName;

            data = TryHandleTupleDataSource(dataFromTestDataRow, testMethodParameters, out object?[] tupleExpandedToArray)
                ? tupleExpandedToArray
                : [dataFromTestDataRow];

            return true;
        }

        data = d;
        ignoreMessageFromTestDataRow = null;
        displayNameFromTestDataRow = null;
        return false;
    }

    public static bool TryHandleTupleDataSource(object? data, ParameterInfo[] testMethodParameters, out object?[] array)
    {
        if (testMethodParameters.Length == 1 &&
            data?.GetType().IsAssignableTo(testMethodParameters[0].ParameterType) == true)
        {
            array = [];
            return false;
        }

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
