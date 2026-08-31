// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies a member that provides values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
[CLSCompliant(false)]
public class CombinatorialMemberDataAttribute : Attribute, ICombinatorialValuesProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialMemberDataAttribute"/> class.
    /// </summary>
    /// <param name="memberName">The name of the public static member that provides values.</param>
    /// <param name="arguments">Arguments for a method member. They are ignored for fields and properties.</param>
    public CombinatorialMemberDataAttribute(string memberName, params object?[]? arguments)
    {
        MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
        Arguments = arguments;
    }

    /// <summary>
    /// Gets the member name.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// Gets or sets the type from which to retrieve the member. The test class is used by default.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)]
    public Type? MemberType { get; set; }

    /// <summary>
    /// Gets the arguments passed to a method member.
    /// </summary>
    public object?[]? Arguments { get; }

    /// <inheritdoc />
    public object?[] GetValues(ParameterInfo parameter)
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        Type? type = MemberType ?? parameter.Member.DeclaringType;
        if (type is null)
        {
            return [];
        }

        Func<object?>? accessor = GetPropertyAccessor(type, parameter)
            ?? GetMethodAccessor(type, parameter)
            ?? GetFieldAccessor(type, parameter);
        if (accessor is null)
        {
            string parameterText = Arguments?.Length > 0
                ? $" with parameter types: {string.Join(", ", Arguments.Select(p => p?.GetType().FullName ?? "(null)"))}"
                : string.Empty;
            throw new ArgumentException(
                $"Could not find public static member (property, field, or method) named '{MemberName}' on {type.FullName}{parameterText}.");
        }

        IEnumerable values = accessor() as IEnumerable
            ?? throw new ArgumentException($"Member {MemberName} on {type.FullName} returned null.");

        return values is IEnumerable<object[]> rows
            ? rows.SelectMany(row => row).ToArray()
            : values.Cast<object?>().ToArray();
    }

    private static TypeInfo? GetEnumeratedType(Type enumerableType)
    {
        if (enumerableType.IsGenericType && enumerableType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return enumerableType.GetTypeInfo().GenericTypeArguments[0].GetTypeInfo();
        }

        foreach (Type implementedInterface in enumerableType.GetTypeInfo().ImplementedInterfaces)
        {
            TypeInfo interfaceTypeInfo = implementedInterface.GetTypeInfo();
            if (interfaceTypeInfo.IsGenericType && interfaceTypeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return interfaceTypeInfo.GenericTypeArguments[0].GetTypeInfo();
            }
        }

        return null;
    }

    private Func<object?>? GetPropertyAccessor(Type type, ParameterInfo parameterInfo)
    {
        PropertyInfo? propertyInfo = null;
        for (Type? reflectionType = type; reflectionType is not null; reflectionType = reflectionType.GetTypeInfo().BaseType)
        {
            propertyInfo = reflectionType.GetRuntimeProperty(MemberName);
            if (propertyInfo is not null)
            {
                break;
            }
        }

        if (propertyInfo?.GetMethod is not { IsPublic: true, IsStatic: true })
        {
            return null;
        }

        EnsureValidMemberDataType(propertyInfo.PropertyType, propertyInfo.DeclaringType!, parameterInfo);
        return () => propertyInfo.GetValue(null, null);
    }

    private Func<object?>? GetMethodAccessor(Type type, ParameterInfo parameterInfo)
    {
        MethodInfo? methodInfo = null;
        for (Type? reflectionType = type; reflectionType is not null; reflectionType = reflectionType.GetTypeInfo().BaseType)
        {
            methodInfo = reflectionType.GetRuntimeMethods()
                .FirstOrDefault(method =>
                    method.Name == MemberName
                    && method.IsPublic
                    && method.IsStatic
                    && ParameterTypesCompatible(method.GetParameters(), Arguments));
            if (methodInfo is not null)
            {
                break;
            }
        }

        if (methodInfo is null)
        {
            return null;
        }

        EnsureValidMemberDataType(methodInfo.ReturnType, methodInfo.DeclaringType!, parameterInfo);
        return () => methodInfo.Invoke(null, Arguments);
    }

    private Func<object?>? GetFieldAccessor(Type type, ParameterInfo parameterInfo)
    {
        FieldInfo? fieldInfo = null;
        for (Type? reflectionType = type; reflectionType is not null; reflectionType = reflectionType.GetTypeInfo().BaseType)
        {
            fieldInfo = reflectionType.GetRuntimeField(MemberName);
            if (fieldInfo is not null)
            {
                break;
            }
        }

        if (fieldInfo is null || !fieldInfo.IsPublic || !fieldInfo.IsStatic)
        {
            return null;
        }

        EnsureValidMemberDataType(fieldInfo.FieldType, fieldInfo.DeclaringType!, parameterInfo);
        return () => fieldInfo.GetValue(null);
    }

    private static bool ParameterTypesCompatible(ParameterInfo[] parameters, object?[]? arguments)
    {
        if (arguments is null)
        {
            return parameters.Length == 0;
        }

        if (parameters.Length != arguments.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (arguments[i] is object argument)
            {
                if (!parameters[i].ParameterType.GetTypeInfo().IsAssignableFrom(argument.GetType().GetTypeInfo()))
                {
                    return false;
                }
            }
            else if (parameters[i].ParameterType.IsValueType)
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureValidMemberDataType(Type enumerableType, Type declaringType, ParameterInfo parameterInfo)
    {
        if (typeof(IEnumerable<object[]>).IsAssignableFrom(enumerableType))
        {
            return;
        }

        TypeInfo enumeratedType = GetEnumeratedType(enumerableType)
            ?? throw new ArgumentException($"Member {MemberName} on {declaringType.FullName} must return a type that implements IEnumerable<T>.");

        if (enumeratedType.IsArray)
        {
            throw new ArgumentException(
                $"Member {MemberName} on {declaringType.FullName} returned an IEnumerable<{enumeratedType.Name}>, which is not supported.");
        }

        if (enumeratedType.IsGenericType && enumeratedType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            throw new ArgumentException(
                $"Member {MemberName} on {declaringType.FullName} returned an IEnumerable<IEnumerable<{enumeratedType.GenericTypeArguments[0].Name}>>, which is not supported.");
        }

        if (!parameterInfo.ParameterType.GetTypeInfo().IsAssignableFrom(enumeratedType))
        {
            throw new ArgumentException(
                $"Parameter type {parameterInfo.ParameterType.FullName} is not compatible with returned member type {enumeratedType.FullName}.");
        }
    }
}
