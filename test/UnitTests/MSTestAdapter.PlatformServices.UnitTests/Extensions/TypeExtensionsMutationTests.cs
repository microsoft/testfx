// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

/// <remarks>
/// <see cref="TypeExtensions.IsFSharpModule"/> caches its result per type, so every test creates its own
/// types: a cached type would not execute the metadata inspection that these tests are about.
/// </remarks>
public class TypeExtensionsMutationTests : TestContainer
{
    // SourceConstructFlags.Module of FSharp.Core.
    private const int ModuleKind = 7;

    // A flag that is outside of SourceConstructFlags.KindMask (31).
    private const int NonPublicRepresentationFlag = 32;

    public void IsFSharpModuleShouldReturnTrueForClassesCompiledAsModules()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        Type moduleType = fSharpCore.CreateClass(fSharpCore.CompilationMapping, ModuleKind);

        moduleType.IsFSharpModule().Should().BeTrue();
    }

    public void IsFSharpModuleShouldOnlyConsiderTheKindBitsOfTheCompilationMapping()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        Type moduleType = fSharpCore.CreateClass(fSharpCore.CompilationMapping, ModuleKind | NonPublicRepresentationFlag);

        moduleType.IsFSharpModule().Should().BeTrue();
    }

    public void IsFSharpModuleShouldReturnFalseForOtherSourceConstructKinds()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        // None, sum type, record type, object type, field, exception, closure, union case, value, kinds sharing
        // bits with the module kind, and a flag that is not part of the kind.
        int[] kinds = [0, 1, 2, 3, 4, 5, 6, 8, 9, 15, 23, 31, NonPublicRepresentationFlag];
        foreach (int kind in kinds)
        {
            Type type = fSharpCore.CreateClass(fSharpCore.CompilationMapping, kind);

            type.IsFSharpModule().Should().BeFalse("source construct kind {0} is not the module kind", kind);
        }
    }

    public void IsFSharpModuleShouldReturnFalseWhenTheCompilationMappingHasNoArguments()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        Type type = fSharpCore.CreateClass(fSharpCore.CompilationMapping);

        type.IsFSharpModule().Should().BeFalse();
    }

    public void IsFSharpModuleShouldReturnFalseWhenTheTypeHasNoCompilationMapping()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        Type type = fSharpCore.CreateClass();

        type.IsFSharpModule().Should().BeFalse();
    }

    public void IsFSharpModuleShouldReturnFalseForStructsAndInterfaces()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        Type structType = fSharpCore.CreateStruct(fSharpCore.CompilationMapping, ModuleKind);
        Type interfaceType = fSharpCore.CreateInterface(fSharpCore.CompilationMapping, ModuleKind);

        structType.IsFSharpModule().Should().BeFalse();
        interfaceType.IsFSharpModule().Should().BeFalse();
    }

    public void IsFSharpModuleShouldIgnoreFSharpCoreAttributesOtherThanCompilationMapping()
    {
        var fSharpCore = new MetadataAssembly("FSharp.Core");

        Type type = fSharpCore.CreateClass(fSharpCore.OtherMapping, ModuleKind);

        type.IsFSharpModule().Should().BeFalse();
    }

    public void IsFSharpModuleShouldIgnoreCompilationMappingAttributesThatDoNotComeFromFSharpCore()
    {
        var otherAssembly = new MetadataAssembly("NotFSharp.Core");

        Type type = otherAssembly.CreateClass(otherAssembly.CompilationMapping, ModuleKind);

        type.IsFSharpModule().Should().BeFalse();
    }

    // Models the compiler metadata without a dependency on FSharp.Core. The types and the attribute types they
    // carry live in the same dynamic module, so that no cross-assembly reference between dynamic assemblies is needed.
    private sealed class MetadataAssembly
    {
        private readonly ModuleBuilder _module;

        public MetadataAssembly(string assemblyName)
        {
            var name = new AssemblyName(assemblyName);
#if NETFRAMEWORK
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif
            _module = assemblyBuilder.DefineDynamicModule("MetadataModule");
            CompilationMapping = DefineAttribute("Microsoft.FSharp.Core.CompilationMappingAttribute");
            OtherMapping = DefineAttribute("Microsoft.FSharp.Core.OtherMappingAttribute");
        }

        public Type CompilationMapping { get; }

        public Type OtherMapping { get; }

        public Type CreateClass(Type? attributeType = null, int? kind = null)
            => Bake(_module.DefineType(NewTypeName(), TypeAttributes.Public), attributeType, kind);

        public Type CreateStruct(Type? attributeType = null, int? kind = null)
        {
            TypeBuilder builder = _module.DefineType(
                NewTypeName(),
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
                typeof(ValueType));
            builder.DefineField("Value", typeof(int), FieldAttributes.Public);
            return Bake(builder, attributeType, kind);
        }

        public Type CreateInterface(Type? attributeType = null, int? kind = null)
            => Bake(_module.DefineType(NewTypeName(), TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract), attributeType, kind);

        private static string NewTypeName()
            => "FSharpMetadataFixture_" + Guid.NewGuid().ToString("N");

        private static Type Bake(TypeBuilder builder, Type? attributeType, int? kind)
        {
            if (attributeType is not null)
            {
                CustomAttributeBuilder attribute = kind is { } flags
                    ? new CustomAttributeBuilder(attributeType.GetConstructor([typeof(int)])!, [flags])
                    : new CustomAttributeBuilder(attributeType.GetConstructor(Type.EmptyTypes)!, []);
                builder.SetCustomAttribute(attribute);
            }

            return builder.CreateTypeInfo()!.AsType();
        }

        private static void DefineConstructor(TypeBuilder builder, Type[] parameterTypes)
        {
            ConstructorBuilder constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parameterTypes);
            ILGenerator il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!);
            il.Emit(OpCodes.Ret);
        }

        private Type DefineAttribute(string fullName)
        {
            TypeBuilder builder = _module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Sealed, typeof(Attribute));
            DefineConstructor(builder, Type.EmptyTypes);
            DefineConstructor(builder, [typeof(int)]);
            return builder.CreateTypeInfo()!.AsType();
        }
    }
}
