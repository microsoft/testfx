// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;
#if !NETFRAMEWORK
using System.Reflection.Emit;
#endif

namespace Microsoft.Testing.TestInfrastructure;

internal sealed class DiagnosticAssertionObserver : IDisposable
{
#if NETFRAMEWORK
    private readonly TraceListener[] _previous;
    private readonly AssertionListener _listener;
#else
    private readonly MethodInfo _setProvider;
    private readonly object _previous;
#endif

    internal DiagnosticAssertionObserver(Action<string?> record)
    {
#if NETFRAMEWORK
        _previous = Debug.Listeners.Cast<TraceListener>().ToArray();
        _listener = new AssertionListener(record);
        Debug.Listeners.Clear();
        Debug.Listeners.Add(_listener);
#else
        // This public runtime hook is omitted from the net8/net9 reference assemblies.
        Type providerType = typeof(Debug).Assembly.GetType("System.Diagnostics.DebugProvider", throwOnError: true)!;
        _setProvider = typeof(Debug).GetMethod("SetProvider", BindingFlags.Public | BindingFlags.Static)!;
        _previous = _setProvider.Invoke(null, [CreateDiagnosticProvider(providerType, record)])!;
#endif
    }

    public void Dispose()
    {
#if NETFRAMEWORK
        Debug.Listeners.Clear();
        Debug.Listeners.AddRange(_previous);
        _listener.Dispose();
#else
        _setProvider.Invoke(null, [_previous]);
#endif
    }

#if NETFRAMEWORK
    private sealed class AssertionListener(Action<string?> record) : TraceListener
    {
        public override void Fail(string? message) => record(message);

        public override void Fail(string? message, string? detailMessage) => record(message);

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }
#else
    private static object CreateDiagnosticProvider(Type baseType, Action<string?> record)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DiagnosticObserver_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        TypeBuilder type = assembly.DefineDynamicModule("Observer").DefineType("AssertionObserver", TypeAttributes.Public | TypeAttributes.Sealed, baseType);
        FieldBuilder callback = type.DefineField("_callback", typeof(Action<string>), FieldAttributes.Private | FieldAttributes.InitOnly);
        ConstructorBuilder constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Action<string>)]);
        ILGenerator body = constructor.GetILGenerator();
        body.Emit(OpCodes.Ldarg_0);
        body.Emit(OpCodes.Call, baseType.GetConstructor(Type.EmptyTypes)!);
        body.Emit(OpCodes.Ldarg_0);
        body.Emit(OpCodes.Ldarg_1);
        body.Emit(OpCodes.Stfld, callback);
        body.Emit(OpCodes.Ret);

        MethodBuilder fail = type.DefineMethod(nameof(Debug.Fail), MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [typeof(string), typeof(string)]);
        ILGenerator failBody = fail.GetILGenerator();
        failBody.Emit(OpCodes.Ldarg_0);
        failBody.Emit(OpCodes.Ldfld, callback);
        failBody.Emit(OpCodes.Ldarg_1);
        failBody.Emit(OpCodes.Callvirt, typeof(Action<string>).GetMethod(nameof(Action<>.Invoke))!);
        failBody.Emit(OpCodes.Ret);
        type.DefineMethodOverride(fail, baseType.GetMethod(nameof(Debug.Fail))!);
        return Activator.CreateInstance(type.CreateType()!, record)!;
    }
#endif
}
