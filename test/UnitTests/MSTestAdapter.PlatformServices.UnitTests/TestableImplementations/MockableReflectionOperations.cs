// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

/// <summary>
/// An <see cref="IReflectionOperations"/> implementation that delegates non-generic interface methods to a Moq
/// mock, and implements the higher-level generic methods by filtering the mock's
/// <see cref="IReflectionOperations.GetCustomAttributes(MemberInfo)"/> results.
/// This bridges the gap where Moq cannot set up generic methods with type constraints.
/// Tests should set up <c>GetCustomAttributes(MemberInfo)</c> or
/// <c>GetCustomAttributes(Assembly, Type)</c> on the mock, and the generic methods will filter those results.
/// </summary>
internal sealed class MockableReflectionOperations(Mock<IReflectionOperations> mock) : IReflectionOperations
{
    /// <summary>
    /// Creates a new <see cref="MockableReflectionOperations"/> from a mock.
    /// </summary>
    public static MockableReflectionOperations Create(Mock<IReflectionOperations> mock)
        => new(mock);

    // Pre-existing interface methods → delegate to mock
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo) => mock.Object.GetCustomAttributes(memberInfo);

    public object[] GetCustomAttributes(Assembly assembly, Type type) => mock.Object.GetCustomAttributes(assembly, type);

    public ConstructorInfo[] GetDeclaredConstructors(Type classType) => mock.Object.GetDeclaredConstructors(classType);

    public MethodInfo[] GetDeclaredMethods(Type classType) => mock.Object.GetDeclaredMethods(classType);

    public PropertyInfo[] GetDeclaredProperties(Type type) => mock.Object.GetDeclaredProperties(type);

    public Type[] GetDefinedTypes(Assembly assembly) => mock.Object.GetDefinedTypes(assembly);

    public MethodInfo[] GetRuntimeMethods(Type type) => mock.Object.GetRuntimeMethods(type);

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic)
        => mock.Object.GetRuntimeMethod(declaringType, methodName, parameters, includeNonPublic);

    public PropertyInfo? GetRuntimeProperty(Type classType, string propertyName, bool includeNonPublic)
        => mock.Object.GetRuntimeProperty(classType, propertyName, includeNonPublic);

    public Type? GetType(string typeName) => mock.Object.GetType(typeName);

    public Type? GetType(Assembly assembly, string typeName) => mock.Object.GetType(assembly, typeName);

    public object? CreateInstance(Type type, object?[] parameters) => mock.Object.CreateInstance(type, parameters);

    // Higher-level generic methods → filter results from mock's GetCustomAttributes
    public bool IsAttributeDefined<TAttribute>(MemberInfo memberInfo)
        where TAttribute : Attribute
        => GetCustomAttributesCached(memberInfo).OfType<TAttribute>().Any();

    public TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
        => GetCustomAttributesCached(attributeProvider).OfType<TAttribute>().FirstOrDefault();

    public TAttribute? GetSingleAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
        => GetCustomAttributesCached(attributeProvider).OfType<TAttribute>().SingleOrDefault();

    public IEnumerable<TAttributeType> GetAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider)
        where TAttributeType : Attribute
        => GetCustomAttributesCached(attributeProvider).OfType<TAttributeType>();

    public Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider)
        => attributeProvider switch
        {
            MemberInfo memberInfo => mock.Object.GetCustomAttributes(memberInfo)?.OfType<Attribute>().ToArray() ?? [],
            Assembly assembly => mock.Object.GetCustomAttributes(assembly, typeof(Attribute)).OfType<Attribute>().ToArray(),
            _ => attributeProvider.GetCustomAttributes(true).OfType<Attribute>().ToArray(),
        };

    public bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        => mock.Object.IsMethodDeclaredInSameAssemblyAsType(method, type);
}
