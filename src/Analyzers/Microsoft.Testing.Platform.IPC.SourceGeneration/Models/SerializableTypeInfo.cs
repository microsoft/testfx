// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.SourceGeneration.Models;

internal sealed class SerializableTypeInfo
{
    public required string TypeName { get; init; }
    
    public required string FullyQualifiedTypeName { get; init; }
    
    public required string Namespace { get; init; }
    
    public required int SerializerId { get; init; }
    
    public required bool IsRequest { get; init; }
    
    public required PropertyInfo[] Properties { get; init; }
}

internal sealed class PropertyInfo
{
    public required string Name { get; init; }
    
    public required string TypeName { get; init; }
    
    public required string FullyQualifiedTypeName { get; init; }
    
    public required bool IsNullable { get; init; }
    
    public required bool IsArray { get; init; }
    
    public required PropertyKind Kind { get; init; }
}

internal enum PropertyKind
{
    String,
    Int,
    Long,
    UShort,
    Bool,
    Byte,
    Complex,
}
