// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
internal sealed class ReflectionOperations : IReflectionOperations
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags Everything = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo)
#if NETFRAMEWORK
         => [.. GetCustomAttributesCore(memberInfo, type: null)];
#else
    {
        object[] attributes = memberInfo.GetCustomAttributes(typeof(Attribute), inherit: true);

        // Ensures that when the return of this method is used here:
        // https://github.com/microsoft/testfx/blob/e101a9d48773cc935c7b536d25d378d9a3211fee/src/Adapter/MSTest.TestAdapter/Helpers/ReflectHelper.cs#L461
        // then we are already Attribute[] to avoid LINQ Cast and extra array allocation.
        // This assert is solely for performance. Nothing "functional" will go wrong if the assert failed.
        Debug.Assert(attributes is Attribute[], $"Expected Attribute[], found '{attributes.GetType()}'.");
        return attributes;
    }
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member info. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, Type type) =>
#if NETFRAMEWORK
        [.. GetCustomAttributesCore(memberInfo, type)];
#else
        memberInfo.GetCustomAttributes(type, inherit: true);
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type) =>
#if NETFRAMEWORK
        GetCustomAttributesForAssembly(assembly, type).ToArray();
#else
        assembly.GetCustomAttributes(type, inherit: true);
#endif

#pragma warning disable IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning disable IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
    public ConstructorInfo[] GetDeclaredConstructors(Type classType)
        => classType.GetConstructors(DeclaredOnlyLookup);

    public MethodInfo[] GetDeclaredMethods(Type classType)
        => classType.GetMethods(DeclaredOnlyLookup);

    public PropertyInfo[] GetDeclaredProperties(Type type)
        => type.GetProperties(DeclaredOnlyLookup);

    public Type[] GetDefinedTypes(Assembly assembly)
        => assembly.GetTypes();

    public MethodInfo[] GetRuntimeMethods(Type type)
        => type.GetMethods(Everything);

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic)
        => includeNonPublic
            ? declaringType.GetMethod(methodName, Everything, null, parameters, null)
            : declaringType.GetMethod(methodName, parameters);

    public PropertyInfo? GetRuntimeProperty(Type classType, string testContextPropertyName, bool includeNonPublic)
        => includeNonPublic
            ? classType.GetProperty(testContextPropertyName, Everything)
            : classType.GetProperty(testContextPropertyName);

    public Type? GetType(string typeName)
        => Type.GetType(typeName);

    public Type? GetType(Assembly assembly, string typeName)
        => assembly.GetType(typeName);

    public object? CreateInstance(Type type, object?[] parameters)
        => Activator.CreateInstance(type, parameters);
#pragma warning restore IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning restore IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning restore IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning restore IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'

#if NETFRAMEWORK
    /// <summary>
    /// Get custom attributes on a member for both normal and reflection only load.
    /// </summary>
    /// <param name="memberInfo">Member for which attributes needs to be retrieved.</param>
    /// <param name="type">Type of attribute to retrieve.</param>
    /// <returns>All attributes of give type on member.</returns>
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IReadOnlyList<object> GetCustomAttributesCore(MemberInfo memberInfo, Type? type)
#pragma warning restore CA1859
    {
        bool shouldGetAllAttributes = type is null;

        if (!IsReflectionOnlyLoad(memberInfo))
        {
            return shouldGetAllAttributes ? memberInfo.GetCustomAttributes(inherit: true) : memberInfo.GetCustomAttributes(type, inherit: true);
        }
        else
        {
            List<object> nonUniqueAttributes = [];
            Dictionary<string, object> uniqueAttributes = [];

            int inheritanceThreshold = 10;
            int inheritanceLevel = 0;

            if (memberInfo.MemberType == MemberTypes.TypeInfo)
            {
                // This code is based on the code for fetching CustomAttributes in System.Reflection.CustomAttribute(RuntimeType type, RuntimeType caType, bool inherit)
                var tempTypeInfo = memberInfo as TypeInfo;

                do
                {
                    IList<CustomAttributeData> attributes = CustomAttributeData.GetCustomAttributes(tempTypeInfo);
                    AddNewAttributes(
                        attributes,
                        shouldGetAllAttributes,
                        type!,
                        uniqueAttributes,
                        nonUniqueAttributes);
                    tempTypeInfo = tempTypeInfo!.BaseType?.GetTypeInfo();
                    inheritanceLevel++;
                }
                while (tempTypeInfo is not null && tempTypeInfo != typeof(object).GetTypeInfo()
                       && inheritanceLevel < inheritanceThreshold);
            }
            else if (memberInfo.MemberType == MemberTypes.Method)
            {
                // This code is based on the code for fetching CustomAttributes in System.Reflection.CustomAttribute(RuntimeMethodInfo method, RuntimeType caType, bool inherit).
                var tempMethodInfo = memberInfo as MethodInfo;

                do
                {
                    IList<CustomAttributeData> attributes = CustomAttributeData.GetCustomAttributes(tempMethodInfo);
                    AddNewAttributes(
                        attributes,
                        shouldGetAllAttributes,
                        type!,
                        uniqueAttributes,
                        nonUniqueAttributes);
                    MethodInfo? baseDefinition = tempMethodInfo!.GetBaseDefinition();

                    if (baseDefinition is not null
                        && string.Equals(
                            string.Concat(tempMethodInfo.DeclaringType.FullName, tempMethodInfo.Name),
                            string.Concat(baseDefinition.DeclaringType.FullName, baseDefinition.Name), StringComparison.Ordinal))
                    {
                        break;
                    }

                    tempMethodInfo = baseDefinition;
                    inheritanceLevel++;
                }
                while (tempMethodInfo is not null && inheritanceLevel < inheritanceThreshold);
            }
            else
            {
                // Ideally we should not be reaching here. We only query for attributes on types/methods currently.
                // Return the attributes that CustomAttributeData returns in this cases not considering inheritance.
                IList<CustomAttributeData> firstLevelAttributes =
                CustomAttributeData.GetCustomAttributes(memberInfo);
                AddNewAttributes(firstLevelAttributes, shouldGetAllAttributes, type!, uniqueAttributes, nonUniqueAttributes);
            }

            nonUniqueAttributes.AddRange(uniqueAttributes.Values);
            return nonUniqueAttributes;
        }
    }

    private static List<Attribute> GetCustomAttributesForAssembly(Assembly assembly, Type type)
    {
        if (!assembly.ReflectionOnly)
        {
            return [.. assembly.GetCustomAttributes(type)];
        }

        List<CustomAttributeData> customAttributes = [.. CustomAttributeData.GetCustomAttributes(assembly)];

        List<Attribute> attributesArray = [];

        foreach (CustomAttributeData attribute in customAttributes)
        {
            if (!IsTypeInheriting(attribute.Constructor.DeclaringType, type)
                    && !attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(
                        type.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                continue;
            }

            Attribute? attributeInstance = CreateAttributeInstance(attribute);
            if (attributeInstance is not null)
            {
                attributesArray.Add(attributeInstance);
            }
        }

        return attributesArray;
    }

    /// <summary>
    /// Create instance of the attribute for reflection only load.
    /// </summary>
    /// <param name="attributeData">The attribute data.</param>
    /// <returns>An attribute.</returns>
    private static Attribute? CreateAttributeInstance(CustomAttributeData attributeData)
    {
        object? attribute = null;
        try
        {
            // Create instance of attribute. For some case, constructor param is returned as ReadOnlyCollection
            // instead of array. So convert it to array else constructor invoke will fail.
            var attributeType = Type.GetType(attributeData.Constructor.DeclaringType.AssemblyQualifiedName);

            List<Type> constructorParameters = [];
            List<object> constructorArguments = [];
            foreach (CustomAttributeTypedArgument parameter in attributeData.ConstructorArguments)
            {
                var parameterType = Type.GetType(parameter.ArgumentType.AssemblyQualifiedName);
                constructorParameters.Add(parameterType);
                if (!parameterType.IsArray
                    || parameter.Value is not IEnumerable enumerable)
                {
                    constructorArguments.Add(parameter.Value);
                    continue;
                }

                ArrayList list = [];
                foreach (object? item in enumerable)
                {
                    if (item is CustomAttributeTypedArgument argument)
                    {
                        list.Add(argument.Value);
                    }
                    else
                    {
                        list.Add(item);
                    }
                }

                constructorArguments.Add(list.ToArray(parameterType.GetElementType()));
            }

            ConstructorInfo constructor = attributeType.GetConstructor([.. constructorParameters]);
            attribute = constructor.Invoke([.. constructorArguments]);

            foreach (CustomAttributeNamedArgument namedArgument in attributeData.NamedArguments)
            {
                attributeType.GetProperty(namedArgument.MemberInfo.Name).SetValue(attribute, namedArgument.TypedValue.Value, null);
            }
        }

        // If not able to create instance of attribute ignore attribute. (May happen for custom user defined attributes).
        catch (BadImageFormatException)
        {
        }
        catch (FileLoadException)
        {
        }
        catch (TypeLoadException)
        {
        }

        return attribute as Attribute;
    }

    private static void AddNewAttributes(
        IList<CustomAttributeData> customAttributes,
        bool shouldGetAllAttributes,
        Type type,
        Dictionary<string, object> uniqueAttributes,
        List<object> nonUniqueAttributes)
    {
        foreach (CustomAttributeData attribute in customAttributes)
        {
            if (!shouldGetAllAttributes
                && !IsTypeInheriting(attribute.Constructor.DeclaringType, type)
                    && !attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(
                        type.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                continue;
            }

            Attribute? attributeInstance = CreateAttributeInstance(attribute);
            if (attributeInstance is null)
            {
                continue;
            }

            Type attributeType = attributeInstance.GetType();
            IReadOnlyList<object> attributeUsageAttributes = GetCustomAttributesCore(
                attributeType,
                typeof(AttributeUsageAttribute));
            if (attributeUsageAttributes.Count > 0
                && attributeUsageAttributes[0] is AttributeUsageAttribute { AllowMultiple: false })
            {
                if (!uniqueAttributes.ContainsKey(attributeType.FullName))
                {
                    uniqueAttributes.Add(attributeType.FullName, attributeInstance);
                }
            }
            else
            {
                nonUniqueAttributes.Add(attributeInstance);
            }
        }
    }

    /// <summary>
    /// Check whether the member is loaded in a reflection only context.
    /// </summary>
    /// <param name="memberInfo"> The member Info. </param>
    /// <returns> True if the member is loaded in a reflection only context. </returns>
    private static bool IsReflectionOnlyLoad(MemberInfo? memberInfo)
        => memberInfo is not null && memberInfo.Module.Assembly.ReflectionOnly;

    private static bool IsTypeInheriting(Type? type1, Type type2)
    {
        while (type1 is not null)
        {
            if (type1.AssemblyQualifiedName.Equals(type2.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                return true;
            }

            type1 = type1.BaseType;
        }

        return false;
    }
#endif
}
