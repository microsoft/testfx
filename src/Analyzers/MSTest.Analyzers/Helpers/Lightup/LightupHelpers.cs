// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers.Lightup;

internal static class LightupHelpers
{
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<OperationKind, bool>> SupportedOperationWrappers = new();

    internal static bool CanWrapOperation(IOperation? operation, Type? underlyingType)
    {
        if (operation == null)
        {
            // The wrappers support a null instance
            return true;
        }

        if (underlyingType == null)
        {
            // The current runtime doesn't define the target type of the conversion, so no instance of it can exist
            return false;
        }

        ConcurrentDictionary<OperationKind, bool> wrappedSyntax = SupportedOperationWrappers.GetOrAdd(underlyingType, _ => new ConcurrentDictionary<OperationKind, bool>());

        // Avoid creating the delegate if the value already exists
        if (!wrappedSyntax.TryGetValue(operation.Kind, out bool canCast))
        {
            canCast = wrappedSyntax.GetOrAdd(
                operation.Kind,
                kind => underlyingType.GetTypeInfo().IsAssignableFrom(operation.GetType().GetTypeInfo()));
        }

        return canCast;
    }

    internal static Func<TOperation, TProperty> CreateOperationPropertyAccessor<TOperation, TProperty>(Type? type, string propertyName, TProperty fallbackResult)
        where TOperation : IOperation
        => CreatePropertyAccessor<TOperation, TProperty>(type, "operation", propertyName, fallbackResult);

    private static Func<T, TProperty> CreatePropertyAccessor<T, TProperty>(Type? type, string parameterName, string propertyName, TProperty fallbackResult)
    {
        if (!TryGetProperty<T, TProperty>(type, propertyName, out PropertyInfo? property))
        {
            return instance => FallbackAccessor(instance, fallbackResult);
        }

        ParameterExpression parameter = Expression.Parameter(typeof(T), parameterName);
        Expression instance =
            type.GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo())
            ? parameter
            : Expression.Convert(parameter, type);

        Expression result = Expression.Call(instance, property.GetMethod!);
        if (!typeof(TProperty).GetTypeInfo().IsAssignableFrom(property.PropertyType.GetTypeInfo()))
        {
            result = Expression.Convert(result, typeof(TProperty));
        }

        var expression = Expression.Lambda<Func<T, TProperty>>(result, parameter);
        return expression.Compile();

        // Local function
        static TProperty FallbackAccessor(T instance, TProperty fallbackResult)
        {
            if (instance is null)
            {
                // Unlike an extension method which would throw ArgumentNullException here, the light-up
                // behavior needs to match behavior of the underlying property.
                throw new ArgumentNullException(nameof(instance));
            }

            return fallbackResult;
        }
    }

    private static void VerifyTypeArgument<T>(Type type)
    {
        if (!typeof(T).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
        {
            throw new InvalidOperationException();
        }
    }

    private static void VerifyResultTypeCompatibility<TValue>(Type resultType)
    {
        if (!typeof(TValue).GetTypeInfo().IsAssignableFrom(resultType.GetTypeInfo()))
        {
            if (resultType.GetTypeInfo().IsEnum
                && typeof(TValue).GetTypeInfo().IsEnum
                && Enum.GetUnderlyingType(typeof(TValue)).GetTypeInfo().IsAssignableFrom(Enum.GetUnderlyingType(resultType).GetTypeInfo()))
            {
                // Allow this
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }

    private static bool TryGetProperty<T, TProperty>([NotNullWhen(true)] Type? type, string propertyName, [NotNullWhen(true)] out PropertyInfo? propertyInfo)
    {
        if (type is null)
        {
            propertyInfo = null;
            return false;
        }

        VerifyTypeArgument<T>(type);

        propertyInfo = type.GetTypeInfo().GetDeclaredProperty(propertyName);
        if (propertyInfo is null)
        {
            return false;
        }

        VerifyResultTypeCompatibility<TProperty>(propertyInfo.PropertyType);
        return true;
    }
}
