// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies a class that provides values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
[CLSCompliant(false)]
public class CombinatorialClassDataAttribute : Attribute, ICombinatorialValuesProvider
{
    private readonly object?[] _arguments;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _valuesSourceType;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialClassDataAttribute"/> class.
    /// </summary>
    /// <param name="valuesSourceType">The type that provides values.</param>
    /// <param name="arguments">Arguments to pass to the constructor of <paramref name="valuesSourceType"/>.</param>
    public CombinatorialClassDataAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type valuesSourceType,
        params object?[]? arguments)
    {
        _valuesSourceType = valuesSourceType ?? throw new ArgumentNullException(nameof(valuesSourceType));
        if (!typeof(IEnumerable<object[]>).IsAssignableFrom(valuesSourceType))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialClassDataSourceTypeInvalid,
                    valuesSourceType,
                    typeof(IEnumerable<object[]>)));
        }

        _arguments = arguments is null ? [null] : [.. arguments];
    }

    /// <inheritdoc />
    public object?[] GetValues(ParameterInfo parameter) => GetValues(_valuesSourceType, _arguments);

    [UnconditionalSuppressMessage("Trimming", "IL2067:UnrecognizedReflectionPattern", Justification = "The Activator overload requires both public and non-public constructors in its static contract, but the binding flags restrict activation to public instance constructors preserved by valuesSourceType.")]
    private static object?[] GetValues(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type valuesSourceType,
        object?[] arguments)
    {
        IEnumerable values;
        try
        {
            values = (IEnumerable)Activator.CreateInstance(
                valuesSourceType,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding,
                binder: null,
                args: arguments,
                culture: CultureInfo.InvariantCulture)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialClassDataActivationFailed, valuesSourceType),
                ex);
        }

        return values.Cast<object[]>().SelectMany(row => row).ToArray();
    }
}
