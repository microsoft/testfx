// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal sealed class ReflectionTestMethodInfo : MethodInfo
{
    private readonly MethodInfo _methodInfo;

    public ReflectionTestMethodInfo(MethodInfo methodInfo, string? displayName)
    {
        _methodInfo = methodInfo;
        DisplayName = displayName ?? methodInfo.Name;
    }

    public string DisplayName { get; }

    public override ICustomAttributeProvider ReturnTypeCustomAttributes => _methodInfo.ReturnTypeCustomAttributes;

    public override MethodAttributes Attributes => _methodInfo.Attributes;

    public override RuntimeMethodHandle MethodHandle => _methodInfo.MethodHandle;

    public override Type? DeclaringType => _methodInfo.DeclaringType;

    public override string Name => _methodInfo.Name;

    public override Type? ReflectedType => _methodInfo.ReflectedType;

    public override MethodInfo GetBaseDefinition() => _methodInfo.GetBaseDefinition();

    public override object[] GetCustomAttributes(bool inherit) => _methodInfo.GetCustomAttributes(inherit);

    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _methodInfo.GetCustomAttributes(attributeType, inherit);

    public override MethodImplAttributes GetMethodImplementationFlags() => _methodInfo.GetMethodImplementationFlags();

    public override ParameterInfo[] GetParameters() => _methodInfo.GetParameters();

    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => _methodInfo.Invoke(obj, invokeAttr, binder, parameters, culture);

    public override bool IsDefined(Type attributeType, bool inherit) => _methodInfo.IsDefined(attributeType, inherit);
}
