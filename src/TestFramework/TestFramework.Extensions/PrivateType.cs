// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class represents a private class for the Private Accessors functionality.
/// </summary>
public class PrivateType
{
    /// <summary>
    /// Binds to everything.
    /// </summary>
    private const BindingFlags BindToEveryThing = BindingFlags.Default
        | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateType"/> class that contains the private type.
    /// </summary>
    /// <param name="assemblyName">Assembly name.</param>
    /// <param name="typeName">fully qualified name of the. </param>
    public PrivateType(string assemblyName, string typeName)
    {
        _ = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        _ = typeName ?? throw new ArgumentNullException(nameof(typeName));
        var asm = Assembly.Load(assemblyName);

        ReferencedType = asm.GetType(typeName, true);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateType"/> class that contains
    /// the private type from the type object.
    /// </summary>
    /// <param name="type">The wrapped Type to create.</param>
    public PrivateType(Type type)
    {
        ReferencedType = type ?? throw new ArgumentNullException(nameof(type));
    }

    /// <summary>
    /// Gets the referenced type.
    /// </summary>
    public Type ReferencedType { get; }

    /// <summary>
    /// Invokes static member.
    /// </summary>
    /// <param name="name">Name of the member to InvokeHelper.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, params object?[]? args) => InvokeStatic(name, null, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes static member.
    /// </summary>
    /// <param name="name">Name of the member to InvokeHelper.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, Type[]? parameterTypes, object?[]? args) => InvokeStatic(name, parameterTypes, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes static member.
    /// </summary>
    /// <param name="name">Name of the member to InvokeHelper.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, Type[]? parameterTypes, object?[]? args, Type[] typeArguments) => InvokeStatic(name, BindToEveryThing, parameterTypes, args, CultureInfo.InvariantCulture, typeArguments);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="culture">Culture.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, object?[]? args, CultureInfo? culture) => InvokeStatic(name, null, args, culture);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, Type[]? parameterTypes, object?[]? args, CultureInfo? culture) => InvokeStatic(name, BindingFlags.InvokeMethod, parameterTypes, args, culture);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, BindingFlags bindingFlags, params object?[]? args) => InvokeStatic(name, bindingFlags, null, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args) => InvokeStatic(name, bindingFlags, parameterTypes, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="culture">Culture.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, BindingFlags bindingFlags, object?[]? args, CultureInfo? culture) => InvokeStatic(name, bindingFlags, null, args, culture);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="culture">Culture.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args, CultureInfo? culture) => InvokeStatic(name, bindingFlags, parameterTypes, args, culture, null);

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="culture">Culture.</param>
    /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
    /// <returns>Result of invocation.</returns>
    public object InvokeStatic(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args, CultureInfo? culture, Type[]? typeArguments)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        if (parameterTypes == null)
        {
            return InvokeHelperStatic(name, bindingFlags | BindingFlags.InvokeMethod, args, culture);
        }

        MethodInfo member = ReferencedType.GetMethod(name, bindingFlags | BindToEveryThing | BindingFlags.Static, null, parameterTypes, null) ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));
        try
        {
            if (member.IsGenericMethodDefinition)
            {
                MethodInfo constructed = member.MakeGenericMethod(typeArguments);
                return constructed.Invoke(null, bindingFlags, null, args, culture);
            }
            else
            {
                return member.Invoke(null, bindingFlags, null, args, culture);
            }
        }
        catch (TargetInvocationException e)
        {
            DebugEx.Assert(e.InnerException != null, "Inner Exception should not be null.");
            if (e.InnerException != null)
            {
                throw e.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Gets the element in static array.
    /// </summary>
    /// <param name="name">Name of the array.</param>
    /// <param name="indices">
    /// A one-dimensional array of 32-bit integers that represent the indexes specifying
    /// the position of the element to get. For instance, to access a[10][11] the indices would be {10,11}.
    /// </param>
    /// <returns>element at the specified location.</returns>
    public object GetStaticArrayElement(string name, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return GetStaticArrayElement(name, BindToEveryThing, indices);
    }

    /// <summary>
    /// Sets the member of the static array.
    /// </summary>
    /// <param name="name">Name of the array.</param>
    /// <param name="value">value to set.</param>
    /// <param name="indices">
    /// A one-dimensional array of 32-bit integers that represent the indexes specifying
    /// the position of the element to set. For instance, to access a[10][11] the array would be {10,11}.
    /// </param>
    public void SetStaticArrayElement(string name, object value, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        SetStaticArrayElement(name, BindToEveryThing, value, indices);
    }

    /// <summary>
    /// Gets the element in static array.
    /// </summary>
    /// <param name="name">Name of the array.</param>
    /// <param name="bindingFlags">Additional InvokeHelper attributes.</param>
    /// <param name="indices">
    /// A one-dimensional array of 32-bit integers that represent the indexes specifying
    /// the position of the element to get. For instance, to access a[10][11] the array would be {10,11}.
    /// </param>
    /// <returns>element at the specified location.</returns>
    public object GetStaticArrayElement(string name, BindingFlags bindingFlags, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        var arr = (Array)InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.GetProperty | bindingFlags, null, CultureInfo.InvariantCulture);
        return arr.GetValue(indices);
    }

    /// <summary>
    /// Sets the member of the static array.
    /// </summary>
    /// <param name="name">Name of the array.</param>
    /// <param name="bindingFlags">Additional InvokeHelper attributes.</param>
    /// <param name="value">value to set.</param>
    /// <param name="indices">
    /// A one-dimensional array of 32-bit integers that represent the indexes specifying
    /// the position of the element to set. For instance, to access a[10][11] the array would be {10,11}.
    /// </param>
    public void SetStaticArrayElement(string name, BindingFlags bindingFlags, object value, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        var arr = (Array)InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Static | bindingFlags, null, CultureInfo.InvariantCulture);
        arr.SetValue(value, indices);
    }

    /// <summary>
    /// Gets the static field.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <returns>The static field.</returns>
    public object GetStaticField(string name)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return GetStaticField(name, BindToEveryThing);
    }

    /// <summary>
    /// Sets the static field.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="value">Argument to the invocation.</param>
    public void SetStaticField(string name, object value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        SetStaticField(name, BindToEveryThing, value);
    }

    /// <summary>
    /// Gets the static field using specified InvokeHelper attributes.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <returns>The static field.</returns>
    public object GetStaticField(string name, BindingFlags bindingFlags)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.Static | bindingFlags, null, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets the static field using binding attributes.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="bindingFlags">Additional InvokeHelper attributes.</param>
    /// <param name="value">Argument to the invocation.</param>
    public void SetStaticField(string name, BindingFlags bindingFlags, object value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        InvokeHelperStatic(name, BindingFlags.SetField | bindingFlags | BindingFlags.Static, new[] { value }, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the static field or property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <returns>The static field or property.</returns>
    public object GetStaticFieldOrProperty(string name)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return GetStaticFieldOrProperty(name, BindToEveryThing);
    }

    /// <summary>
    /// Sets the static field or property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="value">Value to be set to field or property.</param>
    public void SetStaticFieldOrProperty(string name, object value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        SetStaticFieldOrProperty(name, BindToEveryThing, value);
    }

    /// <summary>
    /// Gets the static field or property using specified InvokeHelper attributes.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <returns>The static field or property.</returns>
    public object GetStaticFieldOrProperty(string name, BindingFlags bindingFlags)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Static | bindingFlags, null, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets the static field or property using binding attributes.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="value">Value to be set to field or property.</param>
    public void SetStaticFieldOrProperty(string name, BindingFlags bindingFlags, object value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        InvokeHelperStatic(name, BindingFlags.SetField | BindingFlags.SetProperty | bindingFlags | BindingFlags.Static, new[] { value }, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the static property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <returns>The static property.</returns>
    public object GetStaticProperty(string name, params object?[]? args) => GetStaticProperty(name, BindToEveryThing, args);

    /// <summary>
    /// Sets the static property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="value">Value to be set to field or property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetStaticProperty(string name, object value, params object?[]? args) => SetStaticProperty(name, BindToEveryThing, value, null, args);

    /// <summary>
    /// Sets the static property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="value">Value to be set to field or property.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetStaticProperty(string name, object value, Type[]? parameterTypes, object?[]? args) => SetStaticProperty(name, BindingFlags.SetProperty, value, parameterTypes, args);

    /// <summary>
    /// Gets the static property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The static property.</returns>
    public object GetStaticProperty(string name, BindingFlags bindingFlags, params object?[]? args) => GetStaticProperty(name, BindingFlags.GetProperty | BindingFlags.Static | bindingFlags, null, args);

    /// <summary>
    /// Gets the static property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The static property.</returns>
    public object GetStaticProperty(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        if (parameterTypes == null)
        {
            return InvokeHelperStatic(name, bindingFlags | BindingFlags.GetProperty, args, null);
        }

        PropertyInfo? pi = ReferencedType.GetProperty(name, bindingFlags | BindingFlags.Static, null, null, parameterTypes, null) ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));
        return pi.GetValue(null, args);
    }

    /// <summary>
    /// Sets the static property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="value">Value to be set to field or property.</param>
    /// <param name="args">Optional index values for indexed properties. The indexes of indexed properties are zero-based. This value should be null for non-indexed properties. </param>
    public void SetStaticProperty(string name, BindingFlags bindingFlags, object value, params object?[]? args) => SetStaticProperty(name, bindingFlags, value, null, args);

    /// <summary>
    /// Sets the static property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="value">Value to be set to field or property.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetStaticProperty(string name, BindingFlags bindingFlags, object value, Type[]? parameterTypes, object?[]? args)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));

        if (parameterTypes != null)
        {
            PropertyInfo pi = ReferencedType.GetProperty(name, bindingFlags | BindingFlags.Static, null, null, parameterTypes, null)
                ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));
            pi.SetValue(null, value, args);
        }
        else
        {
            object[] pass = new object[(args?.Length ?? 0) + 1];
            pass[0] = value;
            args?.CopyTo(pass, 1);
            InvokeHelperStatic(name, bindingFlags | BindingFlags.SetProperty, pass, null);
        }
    }

    /// <summary>
    /// Invokes the static method.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">Additional invocation attributes.</param>
    /// <param name="args">Arguments to the invocation.</param>
    /// <param name="culture">Culture.</param>
    /// <returns>Result of invocation.</returns>
    private object InvokeHelperStatic(string name, BindingFlags bindingFlags, object?[]? args, CultureInfo? culture)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        try
        {
            return ReferencedType.InvokeMember(name, bindingFlags | BindToEveryThing | BindingFlags.Static, null, null, args, culture);
        }
        catch (TargetInvocationException e)
        {
            DebugEx.Assert(e.InnerException != null, "Inner Exception should not be null.");
            if (e.InnerException != null)
            {
                throw e.InnerException;
            }

            throw;
        }
    }
}
#endif
