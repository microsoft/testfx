// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Native;

#pragma warning disable RS0016 // Add public types and members to the declared API
public class NativeReflectionOperations : IReflectionOperations2
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public SourceGeneratedReflectionDataProvider ReflectionDataProvider { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, bool inherit)
    {
        if (memberInfo is Type type)
        {
            return ReflectionDataProvider.TypeAttributes[type];
        }
        else if (memberInfo is MethodInfo methodInfo)
        {
            // TODO: We need to use fullname here, but that is hard to populate manually, so we use just name right now.
            return ReflectionDataProvider.TypeMethodAttributes[methodInfo.DeclaringType!][methodInfo.Name];
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit) => throw new NotImplementedException();

    public object[] GetCustomAttributes(Assembly assembly, Type /* the attribute type to find */ type)
    {
        var attributes = new List<object>();
        foreach (object attribute in ReflectionDataProvider.AssemblyAttributes)
        {
            if (attribute.GetType() == type)
            {
                attributes.Add(attribute);
            }
        }

        return attributes.ToArray();
    }

    public IEnumerable<ConstructorInfo> GetDeclaredConstructors(Type classType)
        => ReflectionDataProvider.TypeConstructors[classType];

    public MethodInfo? GetDeclaredMethod(Type dynamicDataDeclaringType, string dynamicDataSourceName) => throw new NotImplementedException();

    public IEnumerable<MethodInfo> GetDeclaredMethods(Type classType)
        => ReflectionDataProvider.TypeMethods[classType];

    public IEnumerable<PropertyInfo> GetDeclaredProperties(Type type)
        => ReflectionDataProvider.TypeProperties[type];

    public PropertyInfo? GetDeclaredProperty(Type type, string propertyName) => throw new NotImplementedException();

    public Type[] GetDefinedTypes(Assembly assembly)
        => ReflectionDataProvider.Types;

    public IEnumerable<MethodInfo> GetRuntimeMethods(Type type)
        => ReflectionDataProvider.TypeMethods[type];

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters) => throw new NotImplementedException();

    public PropertyInfo? GetRuntimeProperty(Type classType, string propertyName)
    {
        Dictionary<string, PropertyInfo> type = ReflectionDataProvider.TypePropertiesByName[classType];

        // We as asking for TestContext here, it may not be there.
        return type.TryGetValue(propertyName, out PropertyInfo? propertyInfo) ? propertyInfo : null;
    }

    public Type? GetType(string typeName)
        => ReflectionDataProvider.TypesByName[typeName];

    public Type? GetType(Assembly assembly, string typeName) => throw new NotImplementedException();

    public object? CreateInstance(Type type, object?[] parameters)
    {
        Func<object?[], object>? invoker = ReflectionDataProvider.TypeConstructorsInvoker[type].FirstOrDefault(constructor =>
        {
            if (constructor.Parameters.Length != parameters.Length)
            {
                return false;
            }

            bool same = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                // TODO: How is the real activator doing this?
                if (constructor.Parameters[i] != parameters[i]!.GetType())
                {
                    same = false;
                    break;
                }
            }

            return same;
        })?.Invoker;

        return invoker != null ? invoker(parameters) : throw new InvalidOperationException($"Cannot find constructor for {type}.");
    }
}
#pragma warning restore RS0016 // Add public types and members to the declared API
