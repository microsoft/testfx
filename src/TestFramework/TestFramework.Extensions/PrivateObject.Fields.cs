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
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
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
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
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
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
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
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
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
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
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
    /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <param name="value">value to set.</param>
    public void SetFieldOrProperty(string name, BindingFlags bindingFlags, object? value)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        InvokeHelper(name, BindingFlags.SetField | BindingFlags.SetProperty | bindingFlags, [value], CultureInfo.InvariantCulture);
    }
}
#endif
