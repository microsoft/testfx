// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable RS0016 // Add public types and members to the declared API
public class NativeReflectionOperations : IReflectionOperations2
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public SourceGeneratedReflectionDataProvider ReflectionDataProvider { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, bool inherit) => throw new NotImplementedException();

    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit) => throw new NotImplementedException();

    public object[] GetCustomAttributes(Assembly assembly, Type type) => throw new NotImplementedException();

    public MethodInfo? GetDeclaredMethod(Type dynamicDataDeclaringType, string dynamicDataSourceName) => throw new NotImplementedException();

    public PropertyInfo? GetDeclaredProperty(Type type, string propertyName) => throw new NotImplementedException();

    public IReadOnlyList<Type> GetDefinedTypes(Assembly assembly) => throw new NotImplementedException();
}
#pragma warning restore RS0016 // Add public types and members to the declared API
