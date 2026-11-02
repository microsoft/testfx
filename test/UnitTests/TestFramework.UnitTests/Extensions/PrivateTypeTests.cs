// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
#pragma warning disable CA1304 // Exercise every public overload, including the invariant-culture forwarding overloads.
using System.Globalization;
using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class PrivateTypeTests : TestContainer
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public void TypeResolutionAndValidationUseTheShippingAssembly()
    {
        new PrivateType(typeof(Fixture)).ReferencedType.Should().Be(typeof(Fixture));
        new PrivateType(typeof(Fixture).Assembly.FullName!, typeof(Fixture).FullName!).ReferencedType.Should().Be(typeof(Fixture));
        Action nullType = () => _ = new PrivateType(null!);
        nullType.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("type");
        Action nullName = () => _ = new PrivateType("assembly", null!);
        nullName.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("typeName");
        Action nullAssembly = () => _ = new PrivateType(null!, "type");
        nullAssembly.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("assemblyName");
        Action missing = () => _ = new PrivateType(typeof(Fixture).Assembly.FullName!, "Missing.Type");
        missing.Should().ThrowExactly<TypeLoadException>();
    }

    public void StaticFieldsArraysAndPropertiesPreserveStateAndNeighbors()
    {
        var type = new PrivateType(typeof(Fixture));
        int field = Fixture.Field;
        int property = Fixture.Property;
        int[,] matrix = (int[,])Fixture.Matrix.Clone();
        try
        {
            type.GetStaticField(nameof(Fixture.Field)).Should().Be(3);
            type.SetStaticField(nameof(Fixture.Field), 11);
            Fixture.Field.Should().Be(11);
            type.GetStaticField(nameof(Fixture.Field), StaticFlags).Should().Be(11);
            type.SetStaticField(nameof(Fixture.Field), StaticFlags, 13);
            Fixture.Field.Should().Be(13);
            type.GetStaticFieldOrProperty(nameof(Fixture.Field)).Should().Be(13);
            type.SetStaticFieldOrProperty(nameof(Fixture.Property), 17);
            Fixture.Property.Should().Be(17);
            type.GetStaticFieldOrProperty(nameof(Fixture.Property), StaticFlags).Should().Be(17);
            type.SetStaticFieldOrProperty(nameof(Fixture.Field), StaticFlags, 19);
            Fixture.Field.Should().Be(19);
            type.GetStaticArrayElement(nameof(Fixture.Matrix), 1, 0).Should().Be(3);
            type.SetStaticArrayElement(nameof(Fixture.Matrix), value: 23, indices: [1, 0]);
            type.GetStaticArrayElement(nameof(Fixture.Matrix), StaticFlags, 0, 1).Should().Be(2);
            type.SetStaticArrayElement(nameof(Fixture.Matrix), bindingFlags: StaticFlags, value: 29, indices: [0, 1]);
            Fixture.Matrix.Cast<int>().Should().Equal(1, 29, 23, 4);
            type.GetStaticProperty(nameof(Fixture.Property)).Should().Be(17);
            type.SetStaticProperty(nameof(Fixture.Property), 31);
            Fixture.Property.Should().Be(31);
            type.GetStaticProperty(nameof(Fixture.Property), StaticFlags).Should().Be(31);
            // This legacy typed convenience overload supplies SetProperty without lookup visibility.
            Action insufficientFlags = () => type.SetStaticProperty(nameof(Fixture.Property), 37, Type.EmptyTypes, []);
            insufficientFlags.Should().ThrowExactly<ArgumentException>().WithMessage("*Property*");
            Fixture.Property.Should().Be(31);
            type.SetStaticProperty(nameof(Fixture.Property), 37, null, []);
            Fixture.Property.Should().Be(37);
            type.SetStaticProperty(nameof(Fixture.Property), StaticFlags, 41);
            Fixture.Property.Should().Be(41);
            type.GetStaticProperty(nameof(Fixture.Property), StaticFlags, Type.EmptyTypes, []).Should().Be(41);
            type.SetStaticProperty(nameof(Fixture.Property), StaticFlags, 43, Type.EmptyTypes, []);
            Fixture.Property.Should().Be(43);
            type.SetStaticProperty(nameof(Fixture.Property), value: 47, args: null);
            Fixture.Property.Should().Be(47);
            Action missingGet = () => type.GetStaticProperty("Missing", StaticFlags, Type.EmptyTypes, []);
            missingGet.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
            Action missingSet = () => type.SetStaticProperty("Missing", StaticFlags, 1, Type.EmptyTypes, []);
            missingSet.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
            Action outside = () => type.GetStaticArrayElement(nameof(Fixture.Matrix), -1, 0);
            outside.Should().ThrowExactly<IndexOutOfRangeException>();
        }
        finally
        {
            Fixture.Field = field;
            Fixture.Property = property;
            Fixture.Matrix = matrix;
        }
    }

    public void StaticInvocationOverloadsPreserveResultsAndOriginalFailures()
    {
        var type = new PrivateType(typeof(Fixture));
        Type[] types = [typeof(int)];
        object[] args = [7];
        type.InvokeStatic("Triple", args).Should().Be(21);
        type.InvokeStatic("Triple", types, args).Should().Be(21);
        type.InvokeStatic("Triple", args, CultureInfo.InvariantCulture).Should().Be(21);
        type.InvokeStatic("Triple", types, args, CultureInfo.InvariantCulture).Should().Be(21);
        type.InvokeStatic("Triple", StaticFlags, args).Should().Be(21);
        type.InvokeStatic("Triple", StaticFlags, types, args).Should().Be(21);
        type.InvokeStatic("Triple", StaticFlags, args, CultureInfo.InvariantCulture).Should().Be(21);
        type.InvokeStatic("Triple", StaticFlags, types, args, CultureInfo.InvariantCulture).Should().Be(21);
        type.InvokeStatic("Triple", StaticFlags, types, args, null, null).Should().Be(21);
        type.InvokeStatic("Generic", Type.EmptyTypes, [], [typeof(int)]).Should().Be("Int32");
        Action untypedFailure = () => type.InvokeStatic("Fail");
        untypedFailure.Should().ThrowExactly<InvalidOperationException>().WithMessage("static failure");
        Action typedFailure = () => type.InvokeStatic("Fail", Type.EmptyTypes, []);
        typedFailure.Should().ThrowExactly<InvalidOperationException>().WithMessage("static failure");
        Action missing = () => type.InvokeStatic("Missing", Type.EmptyTypes, []);
        missing.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
    }

    public void EveryStaticMemberOverloadRejectsNullNames()
    {
        var type = new PrivateType(typeof(Fixture));
        foreach (MethodInfo method in typeof(PrivateType).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
            {
                continue;
            }

            object?[] args = parameters.Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
            Action invoke = () => method.Invoke(type, args);
            invoke.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<ArgumentNullException>()
                .Which.ParamName.Should().Be("name");
        }
    }

    public void StaticErrorsBindingAndRefArgumentsPreserveContracts()
    {
        var type = new PrivateType(typeof(Fixture));
        type.GetStaticField("field", StaticFlags | BindingFlags.IgnoreCase).Should().Be(3);
        object[] arguments = [7];
        type.InvokeStatic("TripleByRef", [typeof(int).MakeByRefType()], arguments).Should().Be(22);
        arguments.Should().Equal(21);
        Action getter = () => type.GetStaticProperty(nameof(Fixture.ThrowingProperty));
        getter.Should().ThrowExactly<InvalidOperationException>().WithMessage("static getter failure");
        Action setter = () => type.SetStaticProperty(nameof(Fixture.ThrowingProperty), 1);
        setter.Should().ThrowExactly<InvalidOperationException>().WithMessage("static setter failure");
        Action typedGetter = () => type.GetStaticProperty(nameof(Fixture.ThrowingProperty), StaticFlags, Type.EmptyTypes, []);
        typedGetter.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("static getter failure");
        Action typedSetter = () => type.SetStaticProperty(nameof(Fixture.ThrowingProperty), StaticFlags, 1, Type.EmptyTypes, []);
        typedSetter.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("static setter failure");
        Action arrayRank = () => type.GetStaticArrayElement(nameof(Fixture.Matrix), 0);
        arrayRank.Should().ThrowExactly<ArgumentException>();
        Action invalidElement = () => type.SetStaticArrayElement(nameof(Fixture.Matrix), value: "invalid", indices: [0, 0]);
        invalidElement.Should().ThrowExactly<InvalidCastException>();
        Fixture.Matrix.Cast<int>().Should().Equal(1, 2, 3, 4);
        Action invalidConstraint = () => type.InvokeStatic("StructOnly", Type.EmptyTypes, [], [typeof(string)]);
        invalidConstraint.Should().ThrowExactly<ArgumentException>();
    }

    private static class Fixture
    {
#pragma warning disable IDE0051, SA1401 // Reflection targets intentionally include static fields and private methods.
        public static int Field = 3;
        public static int[,] Matrix =
        {
            { 1, 2 },
            { 3, 4 },
        };

        public static int Property { get; set; } = 5;

        public static int ThrowingProperty
        {
            get => throw new InvalidOperationException("static getter failure");
            set => throw new InvalidOperationException("static setter failure");
        }

        private static int Triple(int value) => 3 * value;

        private static int TripleByRef(ref int value)
        {
            value *= 3;
            return value + 1;
        }

        private static string StructOnly<T>()
            where T : struct
            => typeof(T).Name;

        private static string Generic<T>() => typeof(T).Name;

        private static void Fail() => throw new InvalidOperationException("static failure");
#pragma warning restore IDE0051, SA1401
    }
}
#endif
