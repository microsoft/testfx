// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

using MSTest.AotReflection.SourceGeneration.Model;

namespace MSTest.AotReflection.SourceGeneration.Generators;

/// <summary>
/// Emits the registry file (a single C# file per compilation) that mirrors what
/// <c>IReflectionOperations</c> would return at runtime.
///
/// The emitted code shape is intentionally close to what the MSTest adapter needs:
/// per-test-class records holding the materialized <c>Attribute[]</c>, parameter types,
/// and delegate-based constructor / method invokers. A future change to the adapter
/// can iterate over <c>MSTestReflectionMetadata.TestClasses</c> instead of calling
/// <c>Assembly.GetTypes()</c> + reflection.
/// </summary>
internal static class MetadataRegistryEmitter
{
    private const string GeneratedNamespace = "MSTest.SourceGenerated";
    private const string RegistryClassName = "MSTestReflectionMetadata";

    public static string EmitSupportTypes()
    {
        var sb = new IndentedStringBuilder();
        AppendHeader(sb);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        using (sb.Block($"namespace {GeneratedNamespace}"))
        {
            sb.AppendLine("/// <summary>Describes one test class as discovered at compile-time. Mirrors what <c>IReflectionOperations</c> would return at runtime.</summary>");
            using (sb.Block("internal sealed class TestClassReflectionInfo"))
            {
                sb.AppendLine("public Type Type { get; set; } = null!;");
                sb.AppendLine("public Attribute[] Attributes { get; set; } = Array.Empty<Attribute>();");
                sb.AppendLine("public IReadOnlyList<TestMethodReflectionInfo> Methods { get; set; } = Array.Empty<TestMethodReflectionInfo>();");
                sb.AppendLine("public IReadOnlyList<TestPropertyReflectionInfo> Properties { get; set; } = Array.Empty<TestPropertyReflectionInfo>();");
                sb.AppendLine("public IReadOnlyList<TestConstructorReflectionInfo> Constructors { get; set; } = Array.Empty<TestConstructorReflectionInfo>();");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class TestMethodReflectionInfo"))
            {
                sb.AppendLine("public string Name { get; set; } = string.Empty;");
                sb.AppendLine("public bool IsStatic { get; set; }");
                sb.AppendLine("public bool ReturnsTask { get; set; }");
                sb.AppendLine("public bool ReturnsValueTask { get; set; }");
                sb.AppendLine("public bool ReturnsVoid { get; set; }");
                sb.AppendLine("public Type[] ParameterTypes { get; set; } = Array.Empty<Type>();");
                sb.AppendLine("public string[] ParameterNames { get; set; } = Array.Empty<string>();");
                sb.AppendLine("public Attribute[] Attributes { get; set; } = Array.Empty<Attribute>();");
                sb.AppendLine("/// <summary>Materialized argument tuples from <c>[DataRow]</c> attributes (empty for non-data-driven tests). Each <c>object?[]</c> corresponds to one <c>[DataRow]</c> application.</summary>");
                sb.AppendLine("public IReadOnlyList<object?[]> DataRows { get; set; } = Array.Empty<object?[]>();");
                sb.AppendLine("/// <summary>Direct invoker — replaces <see cref=\"System.Reflection.MethodInfo.Invoke(object, object[])\" />. Always returns a non-null <see cref=\"Task\" /> so the caller can <c>await</c> regardless of whether the underlying test method is <c>void</c>, <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, or <c>ValueTask&lt;T&gt;</c>; the result value (if any) is discarded.</summary>");
                sb.AppendLine("public Func<object?, object?[]?, Task> Invoke { get; set; } = static (_, _) => Task.CompletedTask;");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class TestPropertyReflectionInfo"))
            {
                sb.AppendLine("public string Name { get; set; } = string.Empty;");
                sb.AppendLine("public Type PropertyType { get; set; } = typeof(object);");
                sb.AppendLine("public bool HasPublicSetter { get; set; }");
                sb.AppendLine("public Attribute[] Attributes { get; set; } = Array.Empty<Attribute>();");
                sb.AppendLine("public Func<object?, object?> Get { get; set; } = static _ => null;");
                sb.AppendLine("public Action<object?, object?> Set { get; set; } = static (_, _) => { };");
            }

            sb.AppendLine();
            using (sb.Block("internal sealed class TestConstructorReflectionInfo"))
            {
                sb.AppendLine("public Type[] ParameterTypes { get; set; } = Array.Empty<Type>();");
                sb.AppendLine("public Func<object?[]?, object> Invoke { get; set; } = static _ => throw new InvalidOperationException(\"No constructor registered.\");");
            }
        }

        return sb.ToString();
    }

    public static string EmitRegistry(string assemblyName, AssemblyMetadataModel assemblyMetadata, IReadOnlyList<TestClassModel> testClasses)
    {
        var sb = new IndentedStringBuilder();
        AppendHeader(sb);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        using (sb.Block($"namespace {GeneratedNamespace}"))
        {
            sb.AppendLine($"/// <summary>Source-generated reflection metadata for assembly <c>{assemblyName}</c>.</summary>");
            using (sb.Block($"internal static class {RegistryClassName}"))
            {
                sb.AppendLine($"public const string AssemblyName = \"{Escape(assemblyName)}\";");
                sb.AppendLine();

                // Emit assembly-level [assembly: ...] attributes so the consumer never has to call
                // Assembly.GetCustomAttributes for attributes declared in the same compilation.
                EmitAssemblyAttributesProperty(sb, assemblyMetadata.Attributes);
                sb.AppendLine();

                sb.AppendLine("public static IReadOnlyList<TestClassReflectionInfo> TestClasses { get; } = new TestClassReflectionInfo[]");
                using (sb.Block(null))
                {
                    for (int i = 0; i < testClasses.Count; i++)
                    {
                        EmitTestClass(sb, testClasses[i]);
                        if (i < testClasses.Count - 1)
                        {
                            sb.AppendLine(",");
                        }
                    }
                }

                sb.AppendLine(";");
            }
        }

        return sb.ToString();
    }

    private static void EmitAssemblyAttributesProperty(IndentedStringBuilder sb, EquatableArray<AttributeApplicationModel> attributes)
    {
        if (attributes.Length == 0)
        {
            sb.AppendLine("public static IReadOnlyList<Attribute> AssemblyAttributes { get; } = Array.Empty<Attribute>();");
            return;
        }

        sb.AppendLine("public static IReadOnlyList<Attribute> AssemblyAttributes { get; } = new Attribute[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                AttributeApplicationModel attr = attributes[i];
                sb.Append(BuildAttributeExpression(attr));
                if (i < attributes.Length - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine(";");
    }

    private static void EmitTestClass(IndentedStringBuilder sb, TestClassModel model)
    {
        string fqn = model.FullyQualifiedTypeName;
        sb.AppendLine("new TestClassReflectionInfo");
        using (sb.Block(null))
        {
            sb.AppendLine($"Type = typeof({fqn}),");
            EmitAttributesProperty(sb, "Attributes", model.Attributes);
            sb.AppendLine(",");

            EmitConstructors(sb, fqn, model);
            sb.AppendLine(",");

            EmitMethods(sb, fqn, model);
            sb.AppendLine(",");

            EmitProperties(sb, fqn, model);
        }
    }

    private static void EmitConstructors(IndentedStringBuilder sb, string fqn, TestClassModel model)
    {
        sb.AppendLine("Constructors = new TestConstructorReflectionInfo[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < model.Constructors.Length; i++)
            {
                TestConstructorModel ctor = model.Constructors[i];
                sb.AppendLine("new TestConstructorReflectionInfo");
                using (sb.Block(null))
                {
                    EmitParameterTypes(sb, ctor.Parameters);
                    sb.AppendLine(",");

                    string args = BuildArgumentsFromObjectArray(ctor.Parameters);
                    string body = model.IsStatic || model.IsAbstract
                        ? $"throw new InvalidOperationException(\"Cannot instantiate '{fqn}'.\")"
                        : $"new {fqn}({args})";
                    sb.AppendLine($"Invoke = static args => {body},");
                }

                if (i < model.Constructors.Length - 1)
                {
                    sb.AppendLine(",");
                }
            }
        }
    }

    private static void EmitMethods(IndentedStringBuilder sb, string fqn, TestClassModel model)
    {
        sb.AppendLine("Methods = new TestMethodReflectionInfo[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < model.Methods.Length; i++)
            {
                TestMethodModel method = model.Methods[i];
                sb.AppendLine("new TestMethodReflectionInfo");
                using (sb.Block(null))
                {
                    sb.AppendLine($"Name = \"{Escape(method.Name)}\",");
                    sb.AppendLine($"IsStatic = {Bool(method.IsStatic)},");
                    sb.AppendLine($"ReturnsTask = {Bool(method.ReturnsTask)},");
                    sb.AppendLine($"ReturnsValueTask = {Bool(method.ReturnsValueTask)},");
                    sb.AppendLine($"ReturnsVoid = {Bool(method.ReturnsVoid)},");
                    EmitParameterTypes(sb, method.Parameters);
                    sb.AppendLine(",");
                    EmitParameterNames(sb, method.Parameters);
                    sb.AppendLine(",");
                    EmitAttributesProperty(sb, "Attributes", method.Attributes);
                    sb.AppendLine(",");
                    EmitDataRows(sb, method.DataRows);
                    sb.AppendLine(",");
                    EmitMethodInvoker(sb, fqn, method);
                }

                if (i < model.Methods.Length - 1)
                {
                    sb.AppendLine(",");
                }
            }
        }
    }

    private static void EmitProperties(IndentedStringBuilder sb, string fqn, TestClassModel model)
    {
        sb.AppendLine("Properties = new TestPropertyReflectionInfo[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < model.Properties.Length; i++)
            {
                TestPropertyModel prop = model.Properties[i];
                sb.AppendLine("new TestPropertyReflectionInfo");
                using (sb.Block(null))
                {
                    sb.AppendLine($"Name = \"{Escape(prop.Name)}\",");
                    sb.AppendLine($"PropertyType = typeof({prop.FullyQualifiedType}),");
                    sb.AppendLine($"HasPublicSetter = {Bool(prop.HasPublicSetter)},");
                    EmitAttributesProperty(sb, "Attributes", prop.Attributes);
                    sb.AppendLine(",");

                    // Static members are accessed through the type name; instance members through
                    // the cast receiver. Indexers are filtered out earlier because the name-based
                    // Get/Set delegate shape cannot represent them.
                    string getBody = prop.HasGettableValue
                        ? prop.IsStatic
                            ? $"(object?){fqn}.{prop.Name}"
                            : $"instance is null ? null : (object?)(({fqn})instance).{prop.Name}"
                        : $"throw new InvalidOperationException(\"Property '{prop.Name}' has no accessible getter.\")";
                    sb.AppendLine($"Get = static instance => {getBody},");

                    string setTarget = prop.IsStatic ? fqn : $"(({fqn})instance!)";
                    string setBody = prop.HasPublicSetter
                        ? $"{setTarget}.{prop.Name} = ({prop.FullyQualifiedType})value!"
                        : $"throw new InvalidOperationException(\"Property '{prop.Name}' has no public setter.\")";
                    sb.AppendLine($"Set = static (instance, value) => {setBody},");
                }

                if (i < model.Properties.Length - 1)
                {
                    sb.AppendLine(",");
                }
            }
        }
    }

    private static void EmitMethodInvoker(IndentedStringBuilder sb, string classFqn, TestMethodModel method)
    {
        string target = method.IsStatic ? classFqn : $"(({classFqn})instance!)";
        string args = BuildArgumentsFromObjectArray(method.Parameters);
        string call = $"{target}.{method.Name}({args})";

        // The contract is: return a non-null Task representing the (async or sync) completion of the
        // test method, discarding any result value. This lets the caller use a single `await invoker(...)`
        // path regardless of the underlying return shape.
        //   - void / non-Task sync: invoke, return Task.CompletedTask.
        //   - Task / Task<T>: forward the returned Task (treat a `null` return as success).
        //   - ValueTask / ValueTask<T>: avoid Task allocation for the synchronously-completed fast path
        //     via IsCompletedSuccessfully, consuming the result before returning Task.CompletedTask;
        //     otherwise call AsTask().
        string body;
        if (method.ReturnsTask)
        {
            // Task<T> derives from Task, so the same forwarding code handles both. A test method that
            // *declares* a Task return type and then returns `null` is broken at runtime, but mirroring
            // reflection-Invoke we tolerate it and treat it as already-completed.
            body = $"{{ Task? __t = {call}; return __t ?? Task.CompletedTask; }}";
        }
        else if (method.ReturnsValueTask)
        {
            body = $"{{ var __vt = {call}; if (__vt.IsCompletedSuccessfully) {{ __vt.GetAwaiter().GetResult(); return Task.CompletedTask; }} return __vt.AsTask(); }}";
        }
        else if (method.ReturnsVoid)
        {
            body = $"{{ {call}; return Task.CompletedTask; }}";
        }
        else
        {
            // Non-void, non-Task return (e.g. `int Test()`). The test runner discards the value; we still
            // execute the call for its side effects and report success.
            //
            // Note: using a plain invocation statement (instead of `_ = {call}`) discards the value while
            // also handling `ref`-returning methods (e.g. `ref int M()`), where assigning the byref return
            // to a discard would not compile.
            body = $"{{ {call}; return Task.CompletedTask; }}";
        }

        sb.AppendLine($"Invoke = static (instance, args) => {body},");
    }

    private static void EmitDataRows(IndentedStringBuilder sb, EquatableArray<DataRowModel> dataRows)
    {
        if (dataRows.Length == 0)
        {
            sb.Append("DataRows = Array.Empty<object?[]>()");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("DataRows = new object?[][]");
        using (sb.Block(null))
        {
            for (int i = 0; i < dataRows.Length; i++)
            {
                EquatableArray<TypedConstantModel> args = dataRows[i].Arguments;
                if (args.Length == 0)
                {
                    sb.Append("Array.Empty<object?>()");
                }
                else
                {
                    string literals = string.Join(", ", args.AsImmutableArray().Select(BuildConstantExpression));
                    sb.Append($"new object?[] {{ {literals} }}");
                }

                if (i < dataRows.Length - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }
        }
    }

    private static void EmitParameterTypes(IndentedStringBuilder sb, EquatableArray<TestParameterModel> parameters)
    {
        if (parameters.Length == 0)
        {
            sb.Append("ParameterTypes = Array.Empty<Type>()");
            sb.AppendLine();
            return;
        }

        string typesList = string.Join(", ", parameters.AsImmutableArray().Select(p => $"typeof({p.FullyQualifiedType})"));
        sb.AppendLine($"ParameterTypes = new Type[] {{ {typesList} }}");
    }

    private static void EmitParameterNames(IndentedStringBuilder sb, EquatableArray<TestParameterModel> parameters)
    {
        if (parameters.Length == 0)
        {
            sb.Append("ParameterNames = Array.Empty<string>()");
            sb.AppendLine();
            return;
        }

        string names = string.Join(", ", parameters.AsImmutableArray().Select(p => $"\"{Escape(p.Name)}\""));
        sb.AppendLine($"ParameterNames = new string[] {{ {names} }}");
    }

    private static void EmitAttributesProperty(IndentedStringBuilder sb, string propertyName, EquatableArray<AttributeApplicationModel> attributes)
    {
        if (attributes.Length == 0)
        {
            sb.Append($"{propertyName} = Array.Empty<Attribute>()");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"{propertyName} = new Attribute[]");
        using (sb.Block(null))
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                AttributeApplicationModel attr = attributes[i];
                sb.Append(BuildAttributeExpression(attr));
                if (i < attributes.Length - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }
        }
    }

    private static string BuildAttributeExpression(AttributeApplicationModel attribute)
    {
        string ctorArgs = string.Join(", ", attribute.ConstructorArguments.AsImmutableArray().Select(BuildConstantExpression));
        string ctorCall = $"new {attribute.FullyQualifiedAttributeType}({ctorArgs})";

        if (attribute.NamedArguments.Length == 0)
        {
            return ctorCall;
        }

        string initializers = string.Join(", ", attribute.NamedArguments.AsImmutableArray()
            .Select(na => $"{na.Name} = {BuildConstantExpression(na.Value)}"));
        return $"{ctorCall} {{ {initializers} }}";
    }

    private static string BuildConstantExpression(TypedConstantModel constant)
        => constant.Kind switch
        {
            ConstantValueKind.Null => constant.FullyQualifiedType is null
                ? "null"
                : $"({constant.FullyQualifiedType})null!",
            ConstantValueKind.Type => constant.FullyQualifiedType is null
                ? "typeof(object)"
                : $"typeof({constant.FullyQualifiedType})",
            ConstantValueKind.Enum => BuildEnumLiteral(constant),
            ConstantValueKind.Array => BuildArrayLiteral(constant),
            _ => BuildPrimitiveLiteral(constant),
        };

    private static string BuildEnumLiteral(TypedConstantModel constant)
    {
        string? typeName = constant.FullyQualifiedType;
        string raw = FormatPrimitive(constant.PrimitiveValue);
        return typeName is null ? raw : $"({typeName}){raw}";
    }

    private static string BuildArrayLiteral(TypedConstantModel constant)
    {
        // The element type comes back as e.g. "global::System.Object[]" — strip the trailing [].
        string elementType = "object";
        if (constant.FullyQualifiedType is { } typeName && typeName.EndsWith("[]", System.StringComparison.Ordinal))
        {
            elementType = typeName.Substring(0, typeName.Length - 2);
        }

        if (constant.ArrayElements.Length == 0)
        {
            return $"Array.Empty<{elementType}>()";
        }

        string values = string.Join(", ", constant.ArrayElements.AsImmutableArray().Select(BuildConstantExpression));
        return $"new {elementType}[] {{ {values} }}";
    }

    private static string BuildPrimitiveLiteral(TypedConstantModel constant)
        => FormatPrimitive(constant.PrimitiveValue);

    private static string FormatPrimitive(object? value)
        => value switch
        {
            null => "null",
            string s => $"\"{Escape(s)}\"",
            bool b => Bool(b),
            char c => $"'{(c == '\'' ? "\\'" : c.ToString())}'",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
            double d => d.ToString("R", CultureInfo.InvariantCulture) + "d",
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
            uint u => u.ToString(CultureInfo.InvariantCulture) + "U",
            byte by => by.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            short sh => sh.ToString(CultureInfo.InvariantCulture),
            ushort us => us.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null",
        };

    private static string BuildArgumentsFromObjectArray(EquatableArray<TestParameterModel> parameters)
        => parameters.Length == 0
            ? string.Empty
            : string.Join(", ", parameters.AsImmutableArray()
                .Select((p, i) => $"({p.FullyQualifiedType})args![{i}]!"));

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void AppendHeader(IndentedStringBuilder sb)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by MSTest.AotReflection.SourceGeneration. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }
}
