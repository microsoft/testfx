// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class represents the live NON public INTERNAL object in the system.
/// </summary>
public class PrivateObject
{
    #region Data

    // bind everything
    private const BindingFlags BindToEveryThing = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    private static readonly BindingFlags ConstructorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.NonPublic;

    private object _target;
    private Dictionary<string, LinkedList<MethodInfo>>? _methodCache;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that contains
    /// the already existing object of the private class.
    /// </summary>
    /// <param name="obj"> object that serves as starting point to reach the private members.</param>
    /// <param name="memberToAccess">the de-referencing string using . that points to the object to be retrieved as in m_X.m_Y.m_Z.</param>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We don't know anything about the object other than that it's an object, so 'obj' seems reasonable")]
    public PrivateObject(object obj, string memberToAccess)
    {
        _ = obj ?? throw new ArgumentNullException(nameof(obj));
        ValidateAccessString(memberToAccess);

        var temp = obj as PrivateObject;
        temp ??= new PrivateObject(obj);

        // Split The access string
        string[] arr = memberToAccess.Split(['.']);

        for (int i = 0; i < arr.Length; i++)
        {
            object? next = temp.InvokeHelper(arr[i], BindToEveryThing | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty, null, CultureInfo.InvariantCulture);
            DebugEx.Assert(next is not null, "next should not be null");
            temp = new PrivateObject(next);
        }

        _target = temp._target;
        RealType = temp.RealType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <param name="typeName">fully qualified name.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(string assemblyName, string typeName, params object?[]? args)
        : this(assemblyName, typeName, null, args)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <param name="typeName">fully qualified name.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the constructor to get.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(string assemblyName, string typeName, Type[]? parameterTypes, object?[]? args)
        : this(Type.GetType(string.Format(CultureInfo.InvariantCulture, "{0}, {1}", typeName, assemblyName), false), parameterTypes, args)
    {
        _ = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        _ = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="type">type of the object to create.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(Type type, params object?[]? args)
        : this(type, null, args)
    {
        Guard.IsNotNull(type, "type");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
    /// specified type.
    /// </summary>
    /// <param name="type">type of the object to create.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the constructor to get.</param>
    /// <param name="args">Arguments to pass to the constructor.</param>
    public PrivateObject(Type type, Type[]? parameterTypes, object?[]? args)
    {
        Guard.IsNotNull(type, "type");
        object? o;
        if (parameterTypes != null)
        {
            ConstructorInfo? ci = type.GetConstructor(BindToEveryThing, null, parameterTypes, null) ?? throw new ArgumentException(FrameworkMessages.PrivateAccessorConstructorNotFound);
            try
            {
                o = ci.Invoke(args);
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
        else
        {
            o = Activator.CreateInstance(type, ConstructorFlags, null, args, null);
        }

        // Kept for compat reasons
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
        _ = o ?? throw new ArgumentNullException(nameof(o));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
        _target = o;
        RealType = o.GetType();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps
    /// the given object.
    /// </summary>
    /// <param name="obj">object to wrap.</param>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We don't know anything about the object other than that it's an object, so 'obj' seems reasonable")]
    public PrivateObject(object obj)
    {
        _ = obj ?? throw new ArgumentNullException(nameof(obj));
        _target = obj;
        RealType = obj.GetType();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps
    /// the given object.
    /// </summary>
    /// <param name="obj">object to wrap.</param>
    /// <param name="type">PrivateType object.</param>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We don't know anything about the object other than that it's an object, so 'obj' seems reasonable")]
    public PrivateObject(object obj, PrivateType type)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));
        _target = obj;
        RealType = type.ReferencedType;
    }

    #endregion

    /// <summary>
    /// Gets or sets the target.
    /// </summary>
    public object Target
    {
        get => _target;

        set
        {
            _ = value ?? throw new ArgumentNullException(nameof(Target));
            _target = value;
            RealType = value.GetType();
        }
    }

    /// <summary>
    /// Gets the type of underlying object.
    /// </summary>
    public Type RealType { get; private set; }

    private Dictionary<string, LinkedList<MethodInfo>> GenericMethodCache
    {
        get
        {
            if (_methodCache == null)
            {
                BuildGenericMethodCacheForType(RealType);
            }

            DebugEx.Assert(_methodCache != null, "Invalid method cache for type.");

            return _methodCache;
        }
    }

    /// <summary>
    /// returns the hash code of the target object.
    /// </summary>
    /// <returns>int representing hashcode of the target object.</returns>
    public override int GetHashCode()
    {
        DebugEx.Assert(_target != null, "target should not be null.");
        return _target.GetHashCode();
    }

    /// <summary>
    /// Equals.
    /// </summary>
    /// <param name="obj">Object with whom to compare.</param>
    /// <returns>returns true if the objects are equal.</returns>
    public override bool Equals(object? obj)
    {
        if (this != obj)
        {
            DebugEx.Assert(_target != null, "target should not be null.");
            return typeof(PrivateObject) == obj?.GetType()
                && _target.Equals(((PrivateObject)obj)._target);
        }

        return true;
    }

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
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, Type[] parameterTypes, object?[]? args) => Invoke(name, parameterTypes, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
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
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, Type[]? parameterTypes, object?[]? args, CultureInfo culture) => Invoke(name, BindToEveryThing, parameterTypes, args, culture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, params object?[]? args) => Invoke(name, bindingFlags, null, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, Type[] parameterTypes, object?[]? args) => Invoke(name, bindingFlags, parameterTypes, args, CultureInfo.InvariantCulture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, object?[]? args, CultureInfo culture) => Invoke(name, bindingFlags, null, args, culture);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>Result of method call.</returns>
    public object? Invoke(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args, CultureInfo culture) => Invoke(name, bindingFlags, parameterTypes, args, culture, null);

    /// <summary>
    /// Invokes the specified method.
    /// </summary>
    /// <param name="name">Name of the method.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
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
    /// Gets the array element using array of subscripts for each dimension.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="indices">the indices of array.</param>
    /// <returns>An array of elements.</returns>
    public object GetArrayElement(string name, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return GetArrayElement(name, BindToEveryThing, indices);
    }

    /// <summary>
    /// Sets the array element using array of subscripts for each dimension.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="value">Value to set.</param>
    /// <param name="indices">the indices of array.</param>
    public void SetArrayElement(string name, object value, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        SetArrayElement(name, BindToEveryThing, value, indices);
    }

    /// <summary>
    /// Gets the array element using array of subscripts for each dimension.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="indices">the indices of array.</param>
    /// <returns>An array of elements.</returns>
    public object GetArrayElement(string name, BindingFlags bindingFlags, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        var arr = (Array?)InvokeHelper(name, BindingFlags.GetField | bindingFlags, null, CultureInfo.InvariantCulture);
        DebugEx.Assert(arr is not null, "arr should not be null");
        return arr.GetValue(indices);
    }

    /// <summary>
    /// Sets the array element using array of subscripts for each dimension.
    /// </summary>
    /// <param name="name">Name of the member.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">Value to set.</param>
    /// <param name="indices">the indices of array.</param>
    public void SetArrayElement(string name, BindingFlags bindingFlags, object value, params int[] indices)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        var arr = (Array?)InvokeHelper(name, BindingFlags.GetField | bindingFlags, null, CultureInfo.InvariantCulture);
        DebugEx.Assert(arr is not null, "arr should not be null");
        arr.SetValue(value, indices);
    }

    /// <summary>
    /// Get the field.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <returns>The field.</returns>
    public object? GetField(string name)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return GetField(name, BindToEveryThing);
    }

    /// <summary>
    /// Sets the field.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="value">value to set.</param>
    public void SetField(string name, object value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        SetField(name, BindToEveryThing, value);
    }

    /// <summary>
    /// Gets the field.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <returns>The field.</returns>
    public object? GetField(string name, BindingFlags bindingFlags)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return InvokeHelper(name, BindingFlags.GetField | bindingFlags, null, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets the field.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    public void SetField(string name, BindingFlags bindingFlags, object? value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        InvokeHelper(name, BindingFlags.SetField | bindingFlags, [value], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Get the field or property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <returns>The field or property.</returns>
    public object? GetFieldOrProperty(string name)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return GetFieldOrProperty(name, BindToEveryThing);
    }

    /// <summary>
    /// Sets the field or property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="value">value to set.</param>
    public void SetFieldOrProperty(string name, object value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        SetFieldOrProperty(name, BindToEveryThing, value);
    }

    /// <summary>
    /// Gets the field or property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <returns>The field or property.</returns>
    public object? GetFieldOrProperty(string name, BindingFlags bindingFlags)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return InvokeHelper(name, BindingFlags.GetField | BindingFlags.GetProperty | bindingFlags, null, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets the field or property.
    /// </summary>
    /// <param name="name">Name of the field or property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    public void SetFieldOrProperty(string name, BindingFlags bindingFlags, object? value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        InvokeHelper(name, BindingFlags.SetField | BindingFlags.SetProperty | bindingFlags, [value], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The property.</returns>
    public object? GetProperty(string name, params object?[]? args) => GetProperty(name, null, args);

    /// <summary>
    /// Gets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The property.</returns>
    public object? GetProperty(string name, Type[]? parameterTypes, object?[]? args) => GetProperty(name, BindToEveryThing, parameterTypes, args);

    /// <summary>
    /// Set the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="value">value to set.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetProperty(string name, object? value, params object?[]? args) => SetProperty(name, null, value, args);

    /// <summary>
    /// Set the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="value">value to set.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetProperty(string name, Type[]? parameterTypes, object? value, object?[]? args) => SetProperty(name, BindToEveryThing, value, parameterTypes, args);

    /// <summary>
    /// Gets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The property.</returns>
    public object? GetProperty(string name, BindingFlags bindingFlags, params object?[]? args) => GetProperty(name, bindingFlags, null, args);

    /// <summary>
    /// Gets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The property.</returns>
    public object? GetProperty(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        if (parameterTypes == null)
        {
            return InvokeHelper(name, bindingFlags | BindingFlags.GetProperty, args, null);
        }

        PropertyInfo? pi = RealType.GetProperty(name, bindingFlags, null, null, parameterTypes, null) ?? throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));
        return pi.GetValue(_target, args);
    }

    /// <summary>
    /// Sets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetProperty(string name, BindingFlags bindingFlags, object value, params object?[]? args) => SetProperty(name, bindingFlags, value, null, args);

    /// <summary>
    /// Sets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    /// <param name="parameterTypes">An array of <see cref="System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetProperty(string name, BindingFlags bindingFlags, object? value, Type[]? parameterTypes, object?[]? args)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));

        if (parameterTypes == null)
        {
            object?[] pass = new object[(args?.Length ?? 0) + 1];
            pass[0] = value;
            args?.CopyTo(pass, 1);
            InvokeHelper(name, bindingFlags | BindingFlags.SetProperty, pass, null);
            return;
        }

        PropertyInfo? pi = RealType.GetProperty(name, bindingFlags, null, null, parameterTypes, null)
            ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));

        pi.SetValue(_target, value, args);
    }

    #region Private Helpers

    /// <summary>
    /// Validate access string.
    /// </summary>
    /// <param name="access"> access string.</param>
    private static void ValidateAccessString(string access)
    {
        _ = access ?? throw new ArgumentNullException(nameof(access));
        if (access.Length == 0)
        {
            throw new ArgumentException(FrameworkMessages.AccessStringInvalidSyntax);
        }

        string[] arr = access.Split('.');
        foreach (string str in arr)
        {
            if ((str.Length == 0) || (str.IndexOfAny([' ', '\t', '\n']) != -1))
            {
                throw new ArgumentException(FrameworkMessages.AccessStringInvalidSyntax);
            }
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

    private void BuildGenericMethodCacheForType(Type t)
    {
        DebugEx.Assert(t != null, "type should not be null.");
        _methodCache = [];

        MethodInfo[] members = t.GetMethods(BindToEveryThing);

        foreach (MethodInfo member in members)
        {
            if (member is { IsGenericMethod: false, IsGenericMethodDefinition: false })
            {
                continue;
            }

            // automatically initialized to null
            if (!GenericMethodCache.TryGetValue(member.Name, out LinkedList<MethodInfo> listByName))
            {
                listByName = new LinkedList<MethodInfo>();
                GenericMethodCache.Add(member.Name, listByName);
            }

            DebugEx.Assert(listByName != null, "list should not be null.");
            listByName.AddLast(member);
        }
    }

    /// <summary>
    /// Extracts the most appropriate generic method signature from the current private type.
    /// </summary>
    /// <param name="methodName">The name of the method in which to search the signature cache.</param>
    /// <param name="parameterTypes">An array of types corresponding to the types of the parameters in which to search.</param>
    /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
    /// <param name="bindingFlags"><see cref="BindingFlags"/> to further filter the method signatures.</param>
    /// <returns>A method info instance.</returns>
    private MethodInfo? GetGenericMethodFromCache(string methodName, Type[] parameterTypes, Type[] typeArguments, BindingFlags bindingFlags)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(methodName), "Invalid method name.");
        DebugEx.Assert(parameterTypes != null, "Invalid parameter type array.");
        DebugEx.Assert(typeArguments != null, "Invalid type arguments array.");

        // Build a preliminary list of method candidates that contain roughly the same signature.
        LinkedList<MethodInfo> methodCandidates = GetMethodCandidates(methodName, parameterTypes, typeArguments, bindingFlags);

        // Search of ambiguous methods (methods with the same signature).
        var finalCandidates = new MethodInfo[methodCandidates.Count];
        methodCandidates.CopyTo(finalCandidates, 0);

        // TODO: Check if it's possible to have parameterTypes null here as we assert it's not null above.
        if (parameterTypes == null || parameterTypes.Length != 0)
        {
            // Now that we have a preliminary list of candidates, select the most appropriate one.
            return RuntimeTypeHelper.SelectMethod(finalCandidates, parameterTypes!) as MethodInfo;
        }

        for (int i = 0; i < finalCandidates.Length; i++)
        {
            MethodInfo methodInfo = finalCandidates[i];

            if (!RuntimeTypeHelper.CompareMethodSigAndName(methodInfo, finalCandidates[0]))
            {
                throw new AmbiguousMatchException();
            }
        }

        // All the methods have the exact same name and sig so return the most derived one.
        return RuntimeTypeHelper.FindMostDerivedNewSlotMeth(finalCandidates, finalCandidates.Length) as MethodInfo;
    }

    private LinkedList<MethodInfo> GetMethodCandidates(string methodName, Type[] parameterTypes, Type[] typeArguments, BindingFlags bindingFlags)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(methodName), "methodName should not be null.");
        DebugEx.Assert(parameterTypes != null, "parameterTypes should not be null.");
        DebugEx.Assert(typeArguments != null, "typeArguments should not be null.");

        LinkedList<MethodInfo> methodCandidates = new();

        if (!GenericMethodCache.TryGetValue(methodName, out LinkedList<MethodInfo>? methods))
        {
            return methodCandidates;
        }

        DebugEx.Assert(methods != null, "methods should not be null.");

        foreach (MethodInfo candidate in methods)
        {
            bool paramMatch = true;
            Type[] genericArgs = candidate.GetGenericArguments();
            if (genericArgs.Length != typeArguments.Length)
            {
                continue;
            }

            // Since we can't just get the correct MethodInfo from Reflection,
            // we will just match the number of parameters, their order, and their type
            MethodInfo methodCandidate = candidate;
            ParameterInfo[] candidateParams = methodCandidate.GetParameters();

            if (candidateParams.Length != parameterTypes.Length)
            {
                continue;
            }

            if ((bindingFlags & BindingFlags.ExactBinding) == 0)
            {
                methodCandidates.AddLast(methodCandidate);
                continue;
            }

            // Exact binding
            int i = 0;

            foreach (ParameterInfo candidateParam in candidateParams)
            {
                Type sourceParameterType = parameterTypes[i++];
                if (candidateParam.ParameterType.ContainsGenericParameters)
                {
                    // Since we have a generic parameter here, just make sure the IsArray matches.
                    if (candidateParam.ParameterType.IsArray != sourceParameterType.IsArray)
                    {
                        paramMatch = false;
                        break;
                    }
                }
                else
                {
                    if (candidateParam.ParameterType != sourceParameterType)
                    {
                        paramMatch = false;
                        break;
                    }
                }
            }

            if (paramMatch)
            {
                methodCandidates.AddLast(methodCandidate);
                continue;
            }
        }

        return methodCandidates;
    }

    #endregion
}
#endif
