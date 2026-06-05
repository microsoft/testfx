// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class represents the live NON public INTERNAL object in the system.
/// </summary>
public partial class PrivateObject
{
    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, params object?[]? args)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return Invoke(name, null, args, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, Type[] parameterTypes, object?[]? args) => Invoke(name, parameterTypes, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, Type[] parameterTypes, object?[]? args, Type[] typeArguments) => Invoke(name, BindToEveryThing, parameterTypes, args, CultureInfo.InvariantCulture, typeArguments);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, object?[]? args, CultureInfo culture) => Invoke(name, null, args, culture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, Type[]? parameterTypes, object?[]? args, CultureInfo culture) => Invoke(name, BindToEveryThing, parameterTypes, args, culture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, params object?[]? args) => Invoke(name, bindingFlags, null, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, Type[] parameterTypes, object?[]? args) => Invoke(name, bindingFlags, parameterTypes, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, object?[]? args, CultureInfo culture) => Invoke(name, bindingFlags, null, args, culture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args, CultureInfo culture) => Invoke(name, bindingFlags, parameterTypes, args, culture, null);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args, CultureInfo? culture, Type[]? typeArguments)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        if (parameterTypes == null)
        {
            return InvokeHelper(name, bindingFlags | BindingFlags.InvokeMethod, args, culture);
        }

        bindingFlags |= BindToEveryThing | BindingFlags.Instance;

        // Fix up the parameter types
        MethodInfo? member = RealType.GetMethod(name, bindingFlags, null, parameterTypes, null);

        // If the method was not found and type arguments were provided for generic parameters,
        // attempt to look up a generic method.
        if (member == null && typeArguments != null)
        {
            // This method may contain generic parameters...if so, the previous call to
            // GetMethod() will fail because it doesn't fully support generic parameters.

            // Look in the method cache to see if there is a generic method
            // on the incoming type that contains the correct signature.
            member = GetGenericMethodFromCache(name, parameterTypes, typeArguments, bindingFlags);
        }

        if (member == null)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));
        }

        try
        {
            if (member.IsGenericMethodDefinition)
            {
                MethodInfo constructed = member.MakeGenericMethod(typeArguments);
                return constructed.Invoke(_target, bindingFlags, null, args, culture);
            }
            else
            {
                return member.Invoke(_target, bindingFlags, null, args, culture);
            }
        }
        catch (TargetInvocationException e)
        {
            DebugEx.Assert(e.InnerException != null, "Inner exception should not be null.");
            if (e.InnerException != null)
            {
                throw e.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Invokes the member.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional attributes.</param>
    /// <param name="args">Arguments for the invocation.</param>
    /// <param name="culture">Culture.</param>
    /// <returns>Result of the invocation.</returns>
    private object? InvokeHelper(string name, BindingFlags bindingFlags, object?[]? args, CultureInfo? culture)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        DebugEx.Assert(_target != null, "Internal Error: Null reference is returned for internal object");

        // Invoke the actual Method
        try
        {
            return RealType.InvokeMember(name, bindingFlags, null, _target, args, culture);
        }
        catch (TargetInvocationException e)
        {
            DebugEx.Assert(e.InnerException != null, "Inner exception should not be null.");
            if (e.InnerException != null)
            {
                throw e.InnerException;
            }

            throw;
        }
    }
}
#endif
