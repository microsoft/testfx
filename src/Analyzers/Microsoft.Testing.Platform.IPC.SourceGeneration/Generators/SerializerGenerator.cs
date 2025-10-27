// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Platform.IPC.SourceGeneration.Helpers;
using Microsoft.Testing.Platform.IPC.SourceGeneration.Models;

namespace Microsoft.Testing.Platform.IPC.SourceGeneration;

[Generator]
internal sealed class SerializerGenerator : IIncrementalGenerator
{
    private const string GenerateSerializerAttributeName = "Microsoft.Testing.Platform.IPC.GenerateSerializerAttribute";
    private const string IRequestInterfaceName = "Microsoft.Testing.Platform.IPC.IRequest";
    private const string IResponseInterfaceName = "Microsoft.Testing.Platform.IPC.IResponse";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Emit the marker attribute
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "GenerateSerializerAttribute.g.cs",
            SourceText.From(GenerateMarkerAttribute(), Encoding.UTF8)));

        // Find all types marked with [GenerateSerializer]
        IncrementalValuesProvider<SerializableTypeInfo?> serializableTypesProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateSerializerAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (context, cancellationToken) => ExtractTypeInfo(context, cancellationToken))
            .Where(static info => info is not null);

        // Generate serializers for each type
        context.RegisterSourceOutput(serializableTypesProvider, (spc, typeInfo) =>
        {
            if (typeInfo is null)
            {
                return;
            }

            GenerateSerializer(spc, typeInfo);
        });
    }

    private static SerializableTypeInfo? ExtractTypeInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // Get the serializer ID from the attribute
        AttributeData? generateSerializerAttribute = context.Attributes.FirstOrDefault();
        if (generateSerializerAttribute is null || generateSerializerAttribute.ConstructorArguments.Length == 0)
        {
            return null;
        }

        if (generateSerializerAttribute.ConstructorArguments[0].Value is not int serializerId)
        {
            return null;
        }

        // Check if it implements IRequest or IResponse
        bool isRequest = ImplementsInterface(typeSymbol, IRequestInterfaceName);
        bool isResponse = ImplementsInterface(typeSymbol, IResponseInterfaceName);

        if (!isRequest && !isResponse)
        {
            return null;
        }

        // Extract properties
        var properties = ExtractProperties(typeSymbol);

        return new SerializableTypeInfo
        {
            TypeName = typeSymbol.Name,
            FullyQualifiedTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            SerializerId = serializerId,
            IsRequest = isRequest,
            Properties = properties.ToArray(),
        };
    }

    private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, string interfaceName)
    {
        return typeSymbol.AllInterfaces.Any(i => i.ToDisplayString() == interfaceName);
    }

    private static ImmutableArray<Models.PropertyInfo> ExtractProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = ImmutableArray.CreateBuilder<Models.PropertyInfo>();

        // Get all properties
        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol propertySymbol || propertySymbol.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            PropertyKind kind = GetPropertyKind(propertySymbol.Type);
            bool isArray = propertySymbol.Type is IArrayTypeSymbol;
            
            properties.Add(new Models.PropertyInfo
            {
                Name = propertySymbol.Name,
                TypeName = propertySymbol.Type.Name,
                FullyQualifiedTypeName = propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsNullable = propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated,
                IsArray = isArray,
                Kind = kind,
            });
        }

        return properties.ToImmutable();
    }

    private static PropertyKind GetPropertyKind(ITypeSymbol typeSymbol)
    {
        // Handle array types
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return GetPropertyKind(arrayType.ElementType);
        }

        // Handle nullable types
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated && typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 1)
        {
            return GetPropertyKind(namedType.TypeArguments[0]);
        }

        string typeName = typeSymbol.ToDisplayString();
        return typeName switch
        {
            "string" or "System.String" => PropertyKind.String,
            "int" or "System.Int32" => PropertyKind.Int,
            "long" or "System.Int64" => PropertyKind.Long,
            "ushort" or "System.UInt16" => PropertyKind.UShort,
            "bool" or "System.Boolean" => PropertyKind.Bool,
            "byte" or "System.Byte" => PropertyKind.Byte,
            _ => PropertyKind.Complex,
        };
    }

    private static void GenerateSerializer(SourceProductionContext context, SerializableTypeInfo typeInfo)
    {
        var sb = new IndentedStringBuilder();
        sb.AppendAutoGeneratedHeader();
        sb.AppendLine();

        using (sb.AppendBlock($"namespace {typeInfo.Namespace}"))
        {
            string serializerClassName = $"{typeInfo.TypeName}Serializer";

            using (sb.AppendBlock($"internal sealed class {serializerClassName} : BaseSerializer, INamedPipeSerializer"))
            {
                // Id property
                sb.AppendLine($"public int Id => {typeInfo.SerializerId};");
                sb.AppendLine();

                // Deserialize method
                GenerateDeserializeMethod(sb, typeInfo);
                sb.AppendLine();

                // Serialize method
                GenerateSerializeMethod(sb, typeInfo);
            }
        }

        context.AddSource($"{typeInfo.TypeName}Serializer.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateDeserializeMethod(IndentedStringBuilder sb, SerializableTypeInfo typeInfo)
    {
        using (sb.AppendBlock("public object Deserialize(Stream stream)"))
        {
            if (typeInfo.Properties.Length == 0)
            {
                sb.AppendLine($"return new {typeInfo.TypeName}();");
                return;
            }

            // For simple types with ordered properties, deserialize in order
            if (!AreAllPropertiesSimple(typeInfo.Properties))
            {
                sb.AppendLine("// TODO: Implement complex deserialization logic");
                sb.AppendLine($"throw new NotImplementedException(\"Deserialization for {typeInfo.TypeName} needs manual implementation\");");
                return;
            }

            var propertyReads = new List<string>();
            foreach (var property in typeInfo.Properties)
            {
                propertyReads.Add(GetReadExpression(property));
            }

            sb.AppendLine($"return new {typeInfo.TypeName}(");
            sb.IndentationLevel++;
            for (int i = 0; i < propertyReads.Count; i++)
            {
                string comma = i < propertyReads.Count - 1 ? "," : ");";
                sb.AppendLine($"{propertyReads[i]}{comma}");
            }
            sb.IndentationLevel--;
        }
    }

    private static void GenerateSerializeMethod(IndentedStringBuilder sb, SerializableTypeInfo typeInfo)
    {
        using (sb.AppendBlock("public void Serialize(object objectToSerialize, Stream stream)"))
        {
            if (typeInfo.Properties.Length == 0)
            {
                sb.AppendLine("// Empty request/response - nothing to serialize");
                return;
            }

            sb.AppendLine($"var request = ({typeInfo.TypeName})objectToSerialize;");
            
            if (!AreAllPropertiesSimple(typeInfo.Properties))
            {
                sb.AppendLine("// TODO: Implement complex serialization logic");
                sb.AppendLine($"throw new NotImplementedException(\"Serialization for {typeInfo.TypeName} needs manual implementation\");");
                return;
            }

            foreach (var property in typeInfo.Properties)
            {
                GenerateWriteStatement(sb, property);
            }
        }
    }

    private static bool AreAllPropertiesSimple(Models.PropertyInfo[] properties)
    {
        return properties.All(p => !p.IsArray && p.Kind != PropertyKind.Complex);
    }

    private static string GetReadExpression(Models.PropertyInfo property)
    {
        return property.Kind switch
        {
            PropertyKind.String => "ReadString(stream)",
            PropertyKind.Int => "ReadInt(stream)",
            PropertyKind.Long => "ReadLong(stream)",
            PropertyKind.UShort => "ReadUShort(stream)",
            PropertyKind.Bool => "ReadBool(stream)",
            PropertyKind.Byte => "ReadByte(stream)",
            _ => $"/* TODO: Read {property.TypeName} */"
        };
    }

    private static void GenerateWriteStatement(IndentedStringBuilder sb, Models.PropertyInfo property)
    {
        string writeCall = property.Kind switch
        {
            PropertyKind.String => property.IsNullable 
                ? $"WriteString(stream, request.{property.Name} ?? string.Empty);" 
                : $"WriteString(stream, request.{property.Name});",
            PropertyKind.Int => $"WriteInt(stream, request.{property.Name});",
            PropertyKind.Long => $"WriteLong(stream, request.{property.Name});",
            PropertyKind.UShort => $"WriteUShort(stream, request.{property.Name});",
            PropertyKind.Bool => $"WriteBool(stream, request.{property.Name});",
            PropertyKind.Byte => $"WriteByte(stream, request.{property.Name});",
            _ => $"// TODO: Write {property.TypeName}"
        };

        sb.AppendLine(writeCall);
    }

    private static string GenerateMarkerAttribute()
    {
        return @"// <auto-generated/>

#nullable enable

namespace Microsoft.Testing.Platform.IPC
{
    /// <summary>
    /// Marks a Request or Response type for automatic serializer generation.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class GenerateSerializerAttribute : System.Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref=""GenerateSerializerAttribute""/> class.
        /// </summary>
        /// <param name=""serializerId"">The unique ID for the serializer.</param>
        public GenerateSerializerAttribute(int serializerId)
        {
            SerializerId = serializerId;
        }

        /// <summary>
        /// Gets the unique ID for the serializer.
        /// </summary>
        public int SerializerId { get; }
    }
}
";
    }
}
