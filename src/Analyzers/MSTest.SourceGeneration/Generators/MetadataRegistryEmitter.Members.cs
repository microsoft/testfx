// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

internal static partial class MetadataRegistryEmitter
{
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
                    sb.AppendLine($"DeclaringType = typeof({method.DeclaringTypeFullyQualifiedName}),");
                    sb.AppendLine($"IsTestMethod = {Bool(method.IsTestMethod)},");
                    sb.AppendLine($"IsStatic = {Bool(method.IsStatic)},");
                    sb.AppendLine($"IsAsync = {Bool(method.IsAsync)},");
                    sb.AppendLine($"ReturnsTask = {Bool(method.ReturnsTask)},");
                    sb.AppendLine($"ReturnsValueTask = {Bool(method.ReturnsValueTask)},");
                    sb.AppendLine($"ReturnsVoid = {Bool(method.ReturnsVoid)},");
                    sb.AppendLine($"IsDescriptorSupported = {Bool(method.IsDescriptorSupported)},");
                    EmitParameterTypes(sb, method.Parameters);
                    sb.AppendLine(",");
                    EmitAttributesProperty(sb, "Attributes", method.Attributes);
                    sb.AppendLine(",");
                    sb.AppendLine($"AreAttributesComplete = {Bool(method.AreAttributesComplete)},");
                    EmitDynamicDataSources(sb, method.DynamicDataSources);
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
                    string getBody = (prop.HasGettableValue, prop.IsStatic) switch
                    {
                        (false, _) => $"throw new InvalidOperationException(\"Property '{prop.Name}' has no accessible getter.\")",
                        (true, true) => $"(object?){fqn}.{prop.Name}",
                        (true, false) => $"instance is null ? null : (object?)(({fqn})instance).{prop.Name}",
                    };

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
}
