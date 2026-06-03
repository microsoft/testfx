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
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
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
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="value">value to set.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetProperty(string name, Type[]? parameterTypes, object? value, object?[]? args) => SetProperty(name, BindToEveryThing, value, parameterTypes, args);

    /// <summary>
    /// Gets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The property.</returns>
    public object? GetProperty(string name, BindingFlags bindingFlags, params object?[]? args) => GetProperty(name, bindingFlags, null, args);

    /// <summary>
    /// Gets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    /// <returns>The property.</returns>
    public object? GetProperty(string name, BindingFlags bindingFlags, Type[]? parameterTypes, object?[]? args)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        if (parameterTypes == null)
        {
            return InvokeHelper(name, bindingFlags | BindingFlags.GetProperty, args, null);
        }

        PropertyInfo pi = RealType.GetProperty(name, bindingFlags, null, null, parameterTypes, null)
                          ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));
        return pi.GetValue(_target, args);
    }

    /// <summary>
    /// Sets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    /// <param name="args">Arguments to pass to the member to invoke.</param>
    public void SetProperty(string name, BindingFlags bindingFlags, object value, params object?[]? args) => SetProperty(name, bindingFlags, value, null, args);

    /// <summary>
    /// Sets the property.
    /// </summary>
    /// <param name="name">Name of the property.</param>
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
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

        PropertyInfo pi = RealType.GetProperty(name, bindingFlags, null, null, parameterTypes, null)
            ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.PrivateAccessorMemberNotFound, name));

        pi.SetValue(_target, value, args);
    }
}
#endif
