// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    internal void SetArguments(object?[]? arguments) => Arguments = arguments == null ? null : ResolveArguments(arguments);

    internal object?[] ResolveArguments(object?[] arguments)
    {
        // Use the cached ParameterTypes property rather than calling MethodInfo.GetParameters()
        // (which allocates a fresh ParameterInfo[] on every call). ResolveArguments only reads the
        // array, so sharing the cached instance is safe.
        ParameterInfo[] parametersInfo = ParameterTypes;

        // Use the shared, per-method cached parameter metadata instead of re-scanning attributes on every
        // call. The metadata is a pure function of the method, so it is computed once per test method and
        // reused across all data rows regardless of whether they share a TestMethodInfo instance.
        ParameterMetadata metadata = GetParameterMetadata();
        int requiredParameterCount = metadata.RequiredParameterCount;
        int parameterCount = parametersInfo.Length;
        if (arguments.Length < parameterCount && TestDataSourceHelpers.GetDataParameterCount(parametersInfo) < parameterCount)
        {
            parameterCount--;
            if (!parametersInfo[^1].IsOptional)
            {
                requiredParameterCount--;
            }
        }

        bool hasParamsValue = metadata.HasParams;
        object? paramsValues = null;

        // If all the parameters are required, we have fewer arguments
        // supplied than required, or more arguments than the method takes
        // and it doesn't have a params parameter don't try and resolve anything
        if (requiredParameterCount == parameterCount ||
            arguments.Length < requiredParameterCount ||
            (!hasParamsValue && arguments.Length > parameterCount))
        {
            return arguments;
        }

        object?[] newParameters = new object[parameterCount];
        for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
        {
            // We have reached the end of the regular parameters and any additional
            // values will go in a params array
            if (argumentIndex >= parameterCount - 1 && hasParamsValue)
            {
                // If this is the params parameter, instantiate a new object of that type
                if (argumentIndex == parameterCount - 1)
                {
                    paramsValues = PlatformServiceProvider.Instance.ReflectionOperations.CreateInstance(parametersInfo[argumentIndex].ParameterType, [arguments.Length - argumentIndex]);
                    newParameters[argumentIndex] = paramsValues;
                }

                // The params parameters is an array but the type is not known
                // set the values as a generic array
                if (paramsValues is Array paramsArray)
                {
                    paramsArray.SetValue(arguments[argumentIndex], argumentIndex - (parameterCount - 1));
                }
            }
            else
            {
                newParameters[argumentIndex] = arguments[argumentIndex];
            }
        }

        // If arguments supplied are less than total possible arguments set
        // the values supplied to the default values for those parameters
        for (int parameterNotProvidedIndex = arguments.Length; parameterNotProvidedIndex < parameterCount; parameterNotProvidedIndex++)
        {
            // If this is the params parameters, set it to an empty
            // array of that type as DefaultValue is DBNull
            newParameters[parameterNotProvidedIndex] = hasParamsValue && parameterNotProvidedIndex == parameterCount - 1
                ? PlatformServiceProvider.Instance.ReflectionOperations.CreateInstance(parametersInfo[parameterNotProvidedIndex].ParameterType, [0])
                : parametersInfo[parameterNotProvidedIndex].DefaultValue;
        }

        return newParameters;
    }

    private object?[]? GetArgumentsWithCancellationToken(object?[]? arguments)
    {
        ParameterInfo[] parameters = ParameterTypes;
        int dataParameterCount = TestDataSourceHelpers.GetDataParameterCount(parameters);
        if (dataParameterCount == parameters.Length)
        {
            return arguments;
        }

        if (TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(arguments, parameters)
            && arguments is not [object[]])
        {
            arguments = [arguments];
        }

        if (arguments is not null)
        {
            arguments = ResolveArguments(arguments);
        }

        // Bind after initialization and timeout setup, without adding the token to the stored test data.
        return (arguments?.Length ?? 0) == dataParameterCount
            ? [.. arguments ?? [], TestContext.Context.CancellationToken]
            : arguments;
    }
}
