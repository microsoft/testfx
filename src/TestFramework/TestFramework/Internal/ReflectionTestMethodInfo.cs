// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

/// <summary>
/// Wraps MethodInfo with the addition of DisplayName.
/// This only exists because we need to use the correct DisplayName for parameterized tests, and because the current public API design doesn't allow us to do that cleanly.
/// We will remove it in MSTest v4 and we may remove it even in a minor release.
/// </summary>
[Obsolete("This class is public only for use via TestAdapter. Don't use it in your code. We will remove it in MSTest v4 and we may remove it even in a minor release.")]
public sealed class ReflectionTestMethodInfo : MethodInfo
{
    private readonly MethodInfo _methodInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionTestMethodInfo"/> class.
    /// </summary>
    /// <param name="methodInfo">The original MethodInfo that we wrap.</param>
    /// <param name="displayName">The test display name to use.</param>
    public ReflectionTestMethodInfo(MethodInfo methodInfo, string? displayName)
    {
        _methodInfo = methodInfo;
        DisplayName = displayName ?? methodInfo.Name;
    }

    /// <summary>
    /// Gets the test display name.
    /// </summary>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public override ICustomAttributeProvider ReturnTypeCustomAttributes => _methodInfo.ReturnTypeCustomAttributes;

    /// <inheritdoc/>
    public override MethodAttributes Attributes => _methodInfo.Attributes;

    /// <inheritdoc/>
    public override RuntimeMethodHandle MethodHandle => _methodInfo.MethodHandle;

    /// <inheritdoc/>
    public override Type? DeclaringType => _methodInfo.DeclaringType;

    /// <inheritdoc/>
    public override string Name => _methodInfo.Name;

    /// <inheritdoc/>
    public override Type? ReflectedType => _methodInfo.ReflectedType;

    /// <inheritdoc/>
    public override bool ContainsGenericParameters => _methodInfo.ContainsGenericParameters;

    /// <inheritdoc/>
    public override bool IsGenericMethod => _methodInfo.IsGenericMethod;

    /// <inheritdoc/>
    public override bool IsGenericMethodDefinition => _methodInfo.IsGenericMethodDefinition;

    /// <inheritdoc/>
    public override MethodInfo GetBaseDefinition() => _methodInfo.GetBaseDefinition();

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(bool inherit) => _methodInfo.GetCustomAttributes(inherit);

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _methodInfo.GetCustomAttributes(attributeType, inherit);

    /// <inheritdoc/>
    public override MethodImplAttributes GetMethodImplementationFlags() => _methodInfo.GetMethodImplementationFlags();

    /// <inheritdoc/>
    public override ParameterInfo[] GetParameters() => _methodInfo.GetParameters();

    /// <inheritdoc/>
    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => _methodInfo.Invoke(obj, invokeAttr, binder, parameters, culture);

    /// <inheritdoc/>
    public override bool IsDefined(Type attributeType, bool inherit) => _methodInfo.IsDefined(attributeType, inherit);

    /// <inheritdoc/>
    public override MethodInfo MakeGenericMethod(params Type[] typeArguments) => new ReflectionTestMethodInfo(_methodInfo.MakeGenericMethod(typeArguments), DisplayName);

    /// <inheritdoc/>
    public override Type[] GetGenericArguments() => _methodInfo.GetGenericArguments();

    /// <inheritdoc/>
    public override MethodInfo GetGenericMethodDefinition() => _methodInfo.GetGenericMethodDefinition();
}
