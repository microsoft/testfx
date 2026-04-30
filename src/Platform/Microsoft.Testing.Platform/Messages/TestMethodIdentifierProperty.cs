// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Property that uniquely identifies a test method. Values are ECMA-335 compliant.
/// </summary>
public sealed class TestMethodIdentifierProperty : IProperty, IEquatable<TestMethodIdentifierProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodIdentifierProperty"/> class.
    /// </summary>
    /// <param name="assemblyFullName">Assembly full name.</param>
    /// <param name="namespace">Namespace.</param>
    /// <param name="typeName">Type name in metadata format, not including the namespace. Generics are represented by backtick followed by arity. Nested types are represented by <c>+</c>.</param>
    /// <param name="methodName">Method name in metadata format. This is simply the method name, it doesn't include backtick followed by arity.</param>
    /// <param name="methodArity">The number of generic parameters of the method.</param>
    /// <param name="parameterTypeFullNames">Parameter type full names in metadata format.</param>
    /// <param name="returnTypeFullName">Return type full name in metadata format.</param>
    public TestMethodIdentifierProperty(
        string assemblyFullName,
        string @namespace,
        string typeName,
        string methodName,
        int methodArity,
        string[] parameterTypeFullNames,
        string returnTypeFullName)
    {
        AssemblyFullName = assemblyFullName;
        Namespace = @namespace;
        TypeName = typeName;
        MethodName = methodName;
        MethodArity = methodArity;
        ParameterTypeFullNames = parameterTypeFullNames;
        ReturnTypeFullName = returnTypeFullName;
    }

    /// <summary>
    /// Gets the assembly full name.
    /// </summary>
    public string AssemblyFullName { get; }

    /// <summary>
    /// Gets the namespace.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the type name in metadata format, not including the namespace.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the method name in metadata format.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the number of generic parameters of the method.
    /// </summary>
    public int MethodArity { get; }

    /// <summary>
    /// Gets the parameter type full names in metadata format.
    /// </summary>
    public string[] ParameterTypeFullNames { get; }

    /// <summary>
    /// Gets the return type full name in metadata format.
    /// </summary>
    public string ReturnTypeFullName { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestMethodIdentifierProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(AssemblyFullName)} = ");
        builder.Append(AssemblyFullName);
        builder.Append($", {nameof(Namespace)} = ");
        builder.Append(Namespace);
        builder.Append($", {nameof(TypeName)} = ");
        builder.Append(TypeName);
        builder.Append($", {nameof(MethodName)} = ");
        builder.Append(MethodName);
        builder.Append($", {nameof(MethodArity)} = ");
        builder.Append(MethodArity);
        builder.Append($", {nameof(ParameterTypeFullNames)} = [");

        for (int i = 0; i < ParameterTypeFullNames.Length; i++)
        {
            builder.Append(ParameterTypeFullNames[i]);
            if (i < ParameterTypeFullNames.Length - 1)
            {
                builder.Append(", ");
            }
        }

        builder.Append($"], {nameof(ReturnTypeFullName)} = ");
        builder.Append(ReturnTypeFullName);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestMethodIdentifierProperty);

    /// <inheritdoc />
    public bool Equals(TestMethodIdentifierProperty? other)
        => other is not null && AssemblyFullName == other.AssemblyFullName && Namespace == other.Namespace && TypeName == other.TypeName && MethodName == other.MethodName && MethodArity == other.MethodArity && ParameterTypeFullNames.SequenceEqual(other.ParameterTypeFullNames) && ReturnTypeFullName == other.ReturnTypeFullName;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(AssemblyFullName, Namespace, TypeName, MethodName, MethodArity, StructuralComparisons.StructuralEqualityComparer.GetHashCode(ParameterTypeFullNames), ReturnTypeFullName);
}
