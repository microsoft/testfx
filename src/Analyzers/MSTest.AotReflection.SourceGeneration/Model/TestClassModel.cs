// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.Analyzers.Shared;

namespace MSTest.AotReflection.SourceGeneration.Model;

/// <summary>
/// A reified attribute application: the attribute class plus its ctor / named args, captured
/// from <see cref="Microsoft.CodeAnalysis.AttributeData"/> so the generator can emit the
/// equivalent <c>new TAttr(arg1, ...) { Name = ... }</c> at the call site.
/// </summary>
internal sealed record AttributeApplicationModel(
    string FullyQualifiedAttributeType,
    EquatableArray<TypedConstantModel> ConstructorArguments,
    EquatableArray<NamedArgumentModel> NamedArguments);

internal sealed record NamedArgumentModel(string Name, TypedConstantModel Value);

/// <summary>
/// Minimal projection of <see cref="Microsoft.CodeAnalysis.TypedConstant"/> that survives
/// across incremental generator runs (the real type isn't equatable).
/// </summary>
internal sealed record TypedConstantModel(
    ConstantValueKind Kind,
    string? FullyQualifiedType,
    object? PrimitiveValue,
    EquatableArray<TypedConstantModel> ArrayElements);

internal enum ConstantValueKind
{
    Primitive,
    Enum,
    Type,
    Array,
    Null,
}

internal sealed record TestParameterModel(string FullyQualifiedType, string Name);

/// <summary>
/// One row of arguments from a <c>[DataRow]</c> attribute, materialized at compile time so
/// the consumer can iterate without re-reading <c>DataRowAttribute.Data</c> via reflection.
/// </summary>
internal sealed record DataRowModel(EquatableArray<TypedConstantModel> Arguments);

internal sealed record TestMethodModel(
    string Name,
    bool IsStatic,
    bool IsAsync,
    bool ReturnsTask,
    bool ReturnsValueTask,
    bool ReturnsVoid,
    bool IsTestMethod,
    EquatableArray<TestParameterModel> Parameters,
    EquatableArray<AttributeApplicationModel> Attributes,
    EquatableArray<DataRowModel> DataRows);

internal sealed record TestPropertyModel(
    string Name,
    string FullyQualifiedType,
    bool IsStatic,
    bool HasGettableValue,
    bool HasPublicSetter,
    EquatableArray<AttributeApplicationModel> Attributes);

internal sealed record TestConstructorModel(
    EquatableArray<TestParameterModel> Parameters);

/// <summary>
/// Assembly-scoped metadata captured at compile time so the consumer never has to call
/// <see cref="System.Reflection.Assembly.GetCustomAttributes(System.Type, bool)"/> for
/// attributes declared with <c>[assembly: ...]</c> in the same compilation.
/// </summary>
internal sealed record AssemblyMetadataModel(
    EquatableArray<AttributeApplicationModel> Attributes);

internal sealed record TestClassModel(
    string FullyQualifiedTypeName,
    string ContainingNamespace,
    string TypeName,
    bool IsAbstract,
    bool IsStatic,
    EquatableArray<TestConstructorModel> Constructors,
    EquatableArray<TestMethodModel> Methods,
    EquatableArray<TestPropertyModel> Properties,
    EquatableArray<AttributeApplicationModel> Attributes,
    EquatableArray<string> BaseTypeFullyQualifiedNames);
