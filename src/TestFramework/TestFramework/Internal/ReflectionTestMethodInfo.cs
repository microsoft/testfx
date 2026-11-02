// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal sealed class ReflectionTestMethodInfo : MethodInfo
{
    private readonly MethodInfo _methodInfo;
    private ParameterInfo[]? _parameters;

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

    public override bool ContainsGenericParameters => _methodInfo.ContainsGenericParameters;

    public override bool IsGenericMethod => _methodInfo.IsGenericMethod;

    public override bool IsGenericMethodDefinition => _methodInfo.IsGenericMethodDefinition;

    public override MethodInfo GetBaseDefinition() => _methodInfo.GetBaseDefinition();

    public override object[] GetCustomAttributes(bool inherit) => _methodInfo.GetCustomAttributes(inherit);

    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _methodInfo.GetCustomAttributes(attributeType, inherit);

    public override MethodImplAttributes GetMethodImplementationFlags() => _methodInfo.GetMethodImplementationFlags();

    /// <summary>
    /// Returns the parameters of the wrapped method, caching the array across calls.
    /// </summary>
    /// <returns>The cached <see cref="ParameterInfo"/> array of the wrapped method.</returns>
    /// <remarks>
    /// <c>MethodInfo.GetParameters()</c> allocates a fresh <see cref="ParameterInfo"/> array on
    /// every call (a CLR safety guarantee). The wrapped <see cref="MethodInfo"/> is immutable, so the
    /// array is cached and shared across callers of this wrapper (e.g. display-name computation for every
    /// data-driven row) to avoid that per-call allocation.
    /// <para>
    /// Because the same instance is handed out to every caller, the returned array MUST be treated as
    /// read-only. Mutating it (including callers such as user-provided
    /// <c>ITestDataSource.GetDisplayName</c> implementations) would corrupt the cache for all subsequent
    /// callers.
    /// </para>
    /// </remarks>
    public override ParameterInfo[] GetParameters() => _parameters ??= _methodInfo.GetParameters();

    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => _methodInfo.Invoke(obj, invokeAttr, binder, parameters, culture);

    public override bool IsDefined(Type attributeType, bool inherit) => _methodInfo.IsDefined(attributeType, inherit);

#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("The native code for the generic method instantiation might not be available at runtime.")]
#endif
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("The native code for the generic method instantiation might not be available at runtime.")]
#endif
    public override MethodInfo MakeGenericMethod(params Type[] typeArguments) => new ReflectionTestMethodInfo(_methodInfo.MakeGenericMethod(typeArguments), DisplayName);

    public override Type[] GetGenericArguments() => _methodInfo.GetGenericArguments();

    public override MethodInfo GetGenericMethodDefinition() => _methodInfo.GetGenericMethodDefinition();
}
