// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

internal static class FSharpModuleTestFixture
{
    internal static Type ModuleType { get; } = CreateModuleType();

    private static Type CreateModuleType()
    {
        // Model the compiler metadata without adding a dependency on FSharp.Core. Deliberately
        // include instance methods to verify that module metadata alone cannot make them valid.
        var assemblyName = new AssemblyName("FSharp.Core");
#if NETFRAMEWORK
        AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("ModuleSignatureRegressionTests");
        TypeBuilder attributeBuilder = moduleBuilder.DefineType(
            "Microsoft.FSharp.Core.CompilationMappingAttribute",
            TypeAttributes.Public | TypeAttributes.Sealed,
            typeof(Attribute));
        ConstructorBuilder attributeConstructor = attributeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(int)]);
        ILGenerator constructorIl = attributeConstructor.GetILGenerator();
        constructorIl.Emit(OpCodes.Ldarg_0);
        constructorIl.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!);
        constructorIl.Emit(OpCodes.Ret);
        Type attributeType = attributeBuilder.CreateTypeInfo()!.AsType();

        TypeBuilder typeBuilder = moduleBuilder.DefineType("ModuleSignatureRegressionTests.TestModule", TypeAttributes.Public);
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(attributeType.GetConstructor([typeof(int)])!, [7]));
        DefineMethod(typeBuilder, "InstanceMethod", isStatic: false, typeof(void));
        DefineMethod(typeBuilder, "StaticMethod", isStatic: true, typeof(void));
        DefineMethod(typeBuilder, "InstanceGenericValueTaskMethod", isStatic: false, typeof(ValueTask<int>));

        return typeBuilder.CreateTypeInfo()!.AsType();
    }

    private static void DefineMethod(TypeBuilder typeBuilder, string name, bool isStatic, Type returnType)
    {
        MethodAttributes attributes = MethodAttributes.Public;
        if (isStatic)
        {
            attributes |= MethodAttributes.Static;
        }

        MethodBuilder methodBuilder = typeBuilder.DefineMethod(name, attributes, returnType, Type.EmptyTypes);
        ILGenerator il = methodBuilder.GetILGenerator();
        if (returnType != typeof(void))
        {
            LocalBuilder result = il.DeclareLocal(returnType);
            il.Emit(OpCodes.Ldloca_S, result);
            il.Emit(OpCodes.Initobj, returnType);
            il.Emit(OpCodes.Ldloc, result);
        }

        il.Emit(OpCodes.Ret);
    }
}
