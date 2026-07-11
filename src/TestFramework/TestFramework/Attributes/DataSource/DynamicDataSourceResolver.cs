// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// <b>Infrastructure.</b> Registry that the MSTest source generator uses to publish compile-time
/// accessors for <see cref="DynamicDataAttribute"/> data sources and display-name methods, so that at
/// runtime MSTest reads dynamic data without reflecting over the declaring type.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is not intended to be used directly from application code.</b> It is public only because
/// the MSTest source generator emits a <c>[ModuleInitializer]</c> in the test assembly that calls it
/// across the assembly boundary, and module initializers cannot use <c>internal</c> APIs from another
/// assembly. The signature and behaviour are implementation details that may evolve with the generator;
/// do not hand-roll calls to <see cref="RegisterDataProvider"/> / <see cref="RegisterDisplayNameProvider"/>.
/// </para>
/// <para>
/// When a source is not registered here (reflection mode, or a source the generator could not resolve),
/// <see cref="DynamicDataAttribute"/> falls back to reflecting over the declaring type. That fallback is
/// annotated with <see cref="System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute"/>, so it
/// preserves the required members and remains trim / Native AOT safe on its own. The source-generated
/// registrations are an optimization that lets <c>[DynamicData]</c> read its data without reflecting at all.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DynamicDataSourceResolver
{
    private static readonly Dictionary<DataSourceKey, Func<object?[], object?>> DataProviders = [];
    private static readonly Dictionary<DisplayNameKey, Func<MethodInfo, object?[]?, string?>> DisplayNameProviders = [];

#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif

    /// <summary>
    /// <b>Infrastructure.</b> Registers a compile-time accessor that returns the raw data object for the
    /// <see cref="DynamicDataAttribute"/> source named <paramref name="sourceName"/> on
    /// <paramref name="declaringType"/>. The delegate receives the attribute's data-source arguments (empty
    /// for property/field sources) and returns the property/field value or the method's return value.
    /// </summary>
    /// <param name="declaringType">The type declaring the data-source member.</param>
    /// <param name="sourceName">The property, method, or field name that supplies the data.</param>
    /// <param name="dataProvider">
    /// A delegate that produces the raw data object from the data-source arguments. The last registration
    /// for a given (<paramref name="declaringType"/>, <paramref name="sourceName"/>) pair wins.
    /// </param>
    /// <remarks>Do not call from hand-written code; invoked only from the generator's module initializer.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterDataProvider(Type declaringType, string sourceName, Func<object?[], object?> dataProvider)
    {
        if (declaringType is null)
        {
            throw new ArgumentNullException(nameof(declaringType));
        }

        if (sourceName is null)
        {
            throw new ArgumentNullException(nameof(sourceName));
        }

        if (dataProvider is null)
        {
            throw new ArgumentNullException(nameof(dataProvider));
        }

        lock (Lock)
        {
            DataProviders[new DataSourceKey(declaringType, sourceName)] = dataProvider;
        }
    }

    /// <summary>
    /// <b>Infrastructure.</b> Registers a compile-time accessor for the custom display-name method named
    /// <paramref name="methodName"/> on <paramref name="declaringType"/> (see
    /// <see cref="DynamicDataAttribute.DynamicDataDisplayName"/>).
    /// </summary>
    /// <param name="declaringType">The type declaring the display-name method.</param>
    /// <param name="methodName">The display-name method name.</param>
    /// <param name="displayNameProvider">
    /// A delegate that invokes the display-name method with the test <see cref="MethodInfo"/> and the row
    /// data, returning the computed display name. The last registration for a given
    /// (<paramref name="declaringType"/>, <paramref name="methodName"/>) pair wins.
    /// </param>
    /// <remarks>Do not call from hand-written code; invoked only from the generator's module initializer.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterDisplayNameProvider(Type declaringType, string methodName, Func<MethodInfo, object?[]?, string?> displayNameProvider)
    {
        if (declaringType is null)
        {
            throw new ArgumentNullException(nameof(declaringType));
        }

        if (methodName is null)
        {
            throw new ArgumentNullException(nameof(methodName));
        }

        if (displayNameProvider is null)
        {
            throw new ArgumentNullException(nameof(displayNameProvider));
        }

        lock (Lock)
        {
            DisplayNameProviders[new DisplayNameKey(declaringType, methodName)] = displayNameProvider;
        }
    }

    /// <summary>
    /// Tries to obtain the raw data object for a source from the source-generated registrations, avoiding
    /// reflection. Returns <see langword="false"/> when no accessor was registered (reflection fallback).
    /// </summary>
    internal static bool TryGetData(Type declaringType, string sourceName, object?[] arguments, out object? data)
    {
        Func<object?[], object?>? provider;
        lock (Lock)
        {
            if (!DataProviders.TryGetValue(new DataSourceKey(declaringType, sourceName), out provider))
            {
                data = null;
                return false;
            }
        }

        data = provider(arguments);
        return true;
    }

    /// <summary>
    /// Tries to compute a custom display name from the source-generated registrations, avoiding reflection.
    /// Returns <see langword="false"/> when no accessor was registered (reflection fallback).
    /// </summary>
    internal static bool TryGetDisplayName(Type declaringType, string methodName, MethodInfo testMethodInfo, object?[]? data, out string? displayName)
    {
        Func<MethodInfo, object?[]?, string?>? provider;
        lock (Lock)
        {
            if (!DisplayNameProviders.TryGetValue(new DisplayNameKey(declaringType, methodName), out provider))
            {
                displayName = null;
                return false;
            }
        }

        displayName = provider(testMethodInfo, data);
        return true;
    }

    private readonly struct DataSourceKey : IEquatable<DataSourceKey>
    {
        private readonly Type _declaringType;
        private readonly string _sourceName;

        public DataSourceKey(Type declaringType, string sourceName)
        {
            _declaringType = declaringType;
            _sourceName = sourceName;
        }

        public bool Equals(DataSourceKey other)
            => _declaringType == other._declaringType && string.Equals(_sourceName, other._sourceName, StringComparison.Ordinal);

        public override bool Equals(object? obj)
            => obj is DataSourceKey other && Equals(other);

        public override int GetHashCode()
            => (_declaringType.GetHashCode() * 397) ^ _sourceName.GetHashCode();
    }

    private readonly struct DisplayNameKey : IEquatable<DisplayNameKey>
    {
        private readonly Type _declaringType;
        private readonly string _methodName;

        public DisplayNameKey(Type declaringType, string methodName)
        {
            _declaringType = declaringType;
            _methodName = methodName;
        }

        public bool Equals(DisplayNameKey other)
            => _declaringType == other._declaringType && string.Equals(_methodName, other._methodName, StringComparison.Ordinal);

        public override bool Equals(object? obj)
            => obj is DisplayNameKey other && Equals(other);

        public override int GetHashCode()
            => (_declaringType.GetHashCode() * 397) ^ _methodName.GetHashCode();
    }
}
