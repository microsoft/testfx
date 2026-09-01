// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Specifies a member that provides values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
[CLSCompliant(false)]
public class CombinatorialDynamicValuesAttribute : Attribute, ICombinatorialValuesProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialDynamicValuesAttribute"/> class.
    /// </summary>
    /// <param name="memberName">The name of the public static member that provides values.</param>
    /// <param name="arguments">Arguments for a method member. They are ignored for fields and properties.</param>
    public CombinatorialDynamicValuesAttribute(string memberName, params object?[]? arguments)
    {
        MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
        Arguments = arguments ?? [null];
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
    public object?[] Arguments { get; }

    /// <inheritdoc />
    public object?[] GetValues(ParameterInfo parameter)
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        Type? type = GetMemberType(parameter);
        if (type is null)
        {
            return [];
        }

        Func<object?>? accessor = GetPropertyAccessor(type, parameter)
            ?? GetMethodAccessor(type, parameter)
            ?? GetFieldAccessor(type, parameter);
        if (accessor is null)
        {
            string message = Arguments.Length > 0
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialDynamicValuesMemberNotFoundWithParameterTypes,
                    MemberName,
                    type.FullName,
                    string.Join(", ", Arguments.Select(p => p?.GetType().FullName ?? FrameworkMessages.Common_NullInMessages)))
                : string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialDynamicValuesMemberNotFound, MemberName, type.FullName);
            throw new ArgumentException(message);
        }

        IEnumerable values = accessor() as IEnumerable
            ?? throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialDynamicValuesMemberReturnedNull, MemberName, type.FullName));

        return values.Cast<object?>().ToArray();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern", Justification = "The reflected source member is preserved by MemberType, including its return type. NativeAOT acceptance coverage exercises this interface lookup with a concrete collection return type.")]
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

    private Func<object?>? GetPropertyAccessor([DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)] Type type, ParameterInfo parameterInfo)
    {
        PropertyInfo? propertyInfo = null;
        for (Type? reflectionType = type; reflectionType is not null; reflectionType = GetBaseType(reflectionType))
        {
            propertyInfo = reflectionType.GetTypeInfo().DeclaredProperties.FirstOrDefault(property =>
                property.Name == MemberName
                && property.GetMethod is { IsPublic: true, IsStatic: true }
                && property.GetIndexParameters().Length == 0);
            if (propertyInfo is not null)
            {
                break;
            }
        }

        if (propertyInfo is null)
        {
            return null;
        }

        EnsureValidMemberDataType(propertyInfo.PropertyType, propertyInfo.DeclaringType!, parameterInfo);
        return () => propertyInfo.GetValue(null, null);
    }

    private Func<object?>? GetMethodAccessor([DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)] Type type, ParameterInfo parameterInfo)
    {
        MethodInfo? methodInfo = null;
        for (Type? reflectionType = type; reflectionType is not null; reflectionType = GetBaseType(reflectionType))
        {
            MethodInfo[] compatibleMethods = reflectionType.GetTypeInfo().DeclaredMethods
                .Where(method =>
                    method.Name == MemberName
                    && method.IsPublic
                    && method.IsStatic
                    && !method.ContainsGenericParameters
                    && ParameterTypesCompatible(method.GetParameters(), Arguments))
                .ToArray();
            if (compatibleMethods.Length > 0)
            {
                methodInfo = SelectMostSpecificMethod(compatibleMethods);
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

    private Func<object?>? GetFieldAccessor([DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)] Type type, ParameterInfo parameterInfo)
    {
        FieldInfo? fieldInfo = null;
        for (Type? reflectionType = type; reflectionType is not null; reflectionType = GetBaseType(reflectionType))
        {
            fieldInfo = reflectionType.GetTypeInfo().DeclaredFields.FirstOrDefault(field =>
                field.Name == MemberName
                && field.IsPublic
                && field.IsStatic);
            if (fieldInfo is not null)
            {
                break;
            }
        }

        if (fieldInfo is null)
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
                Type parameterType = parameters[i].ParameterType;
                Type argumentType = argument.GetType();
                if (!IsParameterTypeCompatible(parameterType, argumentType))
                {
                    return false;
                }
            }
            else if (parameters[i].ParameterType.IsValueType
                && Nullable.GetUnderlyingType(parameters[i].ParameterType) is null)
            {
                return false;
            }
        }

        return true;
    }

    private MethodInfo SelectMostSpecificMethod(MethodInfo[] methods)
    {
        MethodInfo[] mostSpecificMethods = methods
            .Where(candidate => methods.All(other => candidate == other || IsMoreSpecific(candidate, other)))
            .ToArray();
        return mostSpecificMethods.Length == 1
            ? mostSpecificMethods[0]
            : throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialDynamicValuesMethodAmbiguous, MemberName));
    }

    private static bool IsMoreSpecific(MethodInfo candidate, MethodInfo other)
    {
        ParameterInfo[] candidateParameters = candidate.GetParameters();
        ParameterInfo[] otherParameters = other.GetParameters();
        bool isStrictlyMoreSpecific = false;
        for (int i = 0; i < candidateParameters.Length; i++)
        {
            Type candidateType = candidateParameters[i].ParameterType;
            Type otherType = otherParameters[i].ParameterType;
            if (candidateType == otherType)
            {
                continue;
            }

            if (!IsParameterTypeCompatible(otherType, candidateType))
            {
                return false;
            }

            isStrictlyMoreSpecific = true;
        }

        return isStrictlyMoreSpecific;
    }

    private static bool IsParameterTypeCompatible(Type parameterType, Type valueType)
        => parameterType.GetTypeInfo().IsAssignableFrom(valueType.GetTypeInfo())
            || Nullable.GetUnderlyingType(parameterType) == valueType;

    [return: DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)]
    private Type? GetMemberType(ParameterInfo parameter)
        => MemberType ?? GetParameterDeclaringType(parameter);

    [return: DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2073:Value returned does not have matching annotations", Justification = "DynamicallyAccessedMemberTypes.All on the derived type preserves its inherited members, so the base type is rooted with the same member set.")]
    private static Type? GetBaseType([DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)] Type type)
        => type.GetTypeInfo().BaseType;

    [return: DynamicallyAccessedMembers(DynamicDataOperations.RequiredMemberTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2073:Value returned does not have matching annotations", Justification = "In supported trimming and NativeAOT configurations, MSTest.SourceGeneration roots each test class and its base types with DynamicallyAccessedMemberTypes.All.")]
    private static Type? GetParameterDeclaringType(ParameterInfo parameter)
        => parameter.Member.DeclaringType;

    private void EnsureValidMemberDataType(Type enumerableType, Type declaringType, ParameterInfo parameterInfo)
    {
        TypeInfo enumeratedType = GetEnumeratedType(enumerableType)
            ?? throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialDynamicValuesMustReturnGenericEnumerable, MemberName, declaringType.FullName));

        if (enumeratedType.IsGenericType && enumeratedType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialDynamicValuesNestedEnumerableUnsupported,
                    MemberName,
                    declaringType.FullName,
                    enumeratedType.GenericTypeArguments[0].Name));
        }

        if (!IsParameterTypeCompatible(parameterInfo.ParameterType, enumeratedType.AsType()))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialDynamicValuesTypeIncompatible,
                    parameterInfo.ParameterType.FullName,
                    enumeratedType.FullName));
        }
    }
}
