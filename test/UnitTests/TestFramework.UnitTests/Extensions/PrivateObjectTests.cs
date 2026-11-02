// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
#pragma warning disable CA1304 // Exercise every public overload, including the invariant-culture forwarding overloads.
using System.Globalization;
using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class PrivateObjectTests : TestContainer
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public void ConstructorsResolveAndDereferenceTargets()
    {
        var fixture = new Fixture();
        new PrivateObject(fixture).Target.Should().BeSameAs(fixture);
        new PrivateObject(fixture, new PrivateType(typeof(Fixture))).RealType.Should().Be(typeof(Fixture));
        new PrivateObject(typeof(Fixture)).GetField(nameof(Fixture.Field)).Should().Be(3);
        new PrivateObject(typeof(Fixture), [11]).GetField(nameof(Fixture.Field)).Should().Be(11);
        new PrivateObject(typeof(Fixture), [typeof(int)], [13]).GetField(nameof(Fixture.Field)).Should().Be(13);
        string assembly = typeof(Fixture).Assembly.FullName!;
        string type = typeof(Fixture).FullName!;
        new PrivateObject(assembly, type, [17]).GetField(nameof(Fixture.Field)).Should().Be(17);
        new PrivateObject(assembly, type, [typeof(int)], [19]).GetField(nameof(Fixture.Field)).Should().Be(19);
        new PrivateObject(fixture, "Child.Value").Target.Should().Be("leaf");
        new PrivateObject(new PrivateObject(fixture), "Child.Value").RealType.Should().Be(typeof(string));
    }

    public void ConstructorsRejectInvalidInputsAndPropagateFailures()
    {
        Action[] nullActions =
        [
            () => _ = new PrivateObject((object)null!),
            () => _ = new PrivateObject((object)null!, "Child"),
            () => _ = new PrivateObject(new Fixture(), (PrivateType)null!),
            () => _ = new PrivateObject(null!, (object[])[]),
            () => _ = new PrivateObject(null!, Type.EmptyTypes, []),
        ];
        foreach (Action action in nullActions)
        {
            action.Should().ThrowExactly<ArgumentNullException>();
        }

        Action nullAccess = () => _ = new PrivateObject(new Fixture(), (string)null!);
        nullAccess.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("access");
        foreach (string access in new[] { string.Empty, ".Child", "Child.", "Child..Value", "Ch ild", "Child.\tValue", "Child.\nValue" })
        {
            Action malformed = () => _ = new PrivateObject(new Fixture(), access);
            malformed.Should().ThrowExactly<ArgumentException>();
        }

        Action missingConstructor = () => _ = new PrivateObject(typeof(Fixture), [typeof(DateTime)], [DateTime.MinValue]);
        missingConstructor.Should().ThrowExactly<ArgumentException>();
        Action throwingConstructor = () => _ = new PrivateObject(typeof(Fixture), [typeof(string)], ["broken"]);
        throwingConstructor.Should().ThrowExactly<InvalidOperationException>().WithMessage("constructor:broken");
        Action unknownType = () => _ = new PrivateObject(typeof(Fixture).Assembly.FullName!, "Missing.Type");
        unknownType.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("type");
        Action missingMember = () => _ = new PrivateObject(new Fixture(), "Missing");
        missingMember.Should().ThrowExactly<MissingMethodException>();
    }

    public void EqualityHashingAndRetargetingFollowTheTarget()
    {
        var target = new Value(23);
        var accessor = new PrivateObject(target);
        accessor.Equals(accessor).Should().BeTrue();
        accessor.Equals(new PrivateObject(new Value(23))).Should().BeTrue();
        accessor.Equals(new PrivateObject(new Value(24))).Should().BeFalse();
        accessor.Equals(target).Should().BeFalse();
        accessor.Equals(null).Should().BeFalse();
        accessor!.GetHashCode().Should().Be(23);
        accessor.Target = "replacement";
        accessor.Target.Should().Be("replacement");
        accessor.RealType.Should().Be(typeof(string));
        Action nullTarget = () => accessor.Target = null!;
        nullTarget.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("value");
        accessor.Target.Should().Be("replacement");
    }

    public void FieldsAndArraysPreserveNeighborsAndRespectBinding()
    {
        var fixture = new Fixture();
        var accessor = new PrivateObject(fixture);
        accessor.GetField("_secret").Should().Be(7);
        accessor.SetField("_secret", 31);
        fixture.Secret.Should().Be(31);
        accessor.GetField(nameof(Fixture.Field), InstanceFlags).Should().Be(3);
        accessor.SetField(nameof(Fixture.Field), InstanceFlags, 37);
        fixture.Field.Should().Be(37);
        accessor.GetFieldOrProperty(nameof(Fixture.Field)).Should().Be(37);
        accessor.SetFieldOrProperty(nameof(Fixture.Property), 41);
        fixture.Property.Should().Be(41);
        accessor.GetFieldOrProperty(nameof(Fixture.Property), InstanceFlags).Should().Be(41);
        accessor.SetFieldOrProperty(nameof(Fixture.Field), InstanceFlags, 43);
        fixture.Field.Should().Be(43);
        accessor.GetArrayElement(nameof(Fixture.Matrix), 1, 0).Should().Be(3);
        accessor.SetArrayElement(nameof(Fixture.Matrix), value: 47, indices: [1, 0]);
        accessor.GetArrayElement(nameof(Fixture.Matrix), InstanceFlags, 0, 1).Should().Be(2);
        accessor.SetArrayElement(nameof(Fixture.Matrix), bindingFlags: InstanceFlags, value: 53, indices: [0, 1]);
        fixture.Matrix.Cast<int>().Should().Equal(1, 53, 47, 4);
        Action outside = () => accessor.GetArrayElement(nameof(Fixture.Matrix), 2, 0);
        outside.Should().ThrowExactly<IndexOutOfRangeException>();
        Action notArray = () => accessor.GetArrayElement(nameof(Fixture.Field), 0);
        notArray.Should().ThrowExactly<InvalidCastException>();
        Action missing = () => accessor.GetField("Missing");
        missing.Should().ThrowExactly<MissingFieldException>();
        Action publicOnly = () => accessor.GetField("_secret", BindingFlags.Public | BindingFlags.Instance);
        publicOnly.Should().ThrowExactly<MissingFieldException>();
    }

    public void PropertiesSelectAndUpdateIndexers()
    {
        var fixture = new Fixture();
        var accessor = new PrivateObject(fixture);
        accessor.GetProperty(nameof(Fixture.Property)).Should().Be(5);
        accessor.SetProperty(nameof(Fixture.Property), 59);
        fixture.Property.Should().Be(59);
        accessor.GetProperty(nameof(Fixture.Property), Type.EmptyTypes, []).Should().Be(59);
        accessor.SetProperty(nameof(Fixture.Property), Type.EmptyTypes, 61, []);
        fixture.Property.Should().Be(61);
        accessor.GetProperty(nameof(Fixture.Property), InstanceFlags).Should().Be(61);
        accessor.SetProperty(nameof(Fixture.Property), InstanceFlags, 67);
        fixture.Property.Should().Be(67);
        accessor.SetProperty(nameof(Fixture.Property), value: 69, args: null);
        fixture.Property.Should().Be(69);
        accessor.GetProperty("Item", InstanceFlags, [typeof(int)], [1]).Should().Be(20);
        accessor.SetProperty("Item", InstanceFlags, 71, [typeof(int)], [1]);
        fixture[0].Should().Be(10);
        fixture[1].Should().Be(71);
        // The untyped compatibility overload prepends the value before the index. A typed signature
        // is required for the normal indexed setter; pin exception propagation without changing production.
        Action untypedIndex = () => accessor.SetProperty("Item", 73, [0]);
        untypedIndex.Should().ThrowExactly<IndexOutOfRangeException>();
        fixture[0].Should().Be(10);
        accessor.SetProperty("Item", [typeof(int)], 73, [0]);
        fixture[0].Should().Be(73);
        accessor.GetProperty("Item", [0]).Should().Be(73);
        Action missingGet = () => accessor.GetProperty("Missing", Type.EmptyTypes, []);
        missingGet.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
        Action missingSet = () => accessor.SetProperty("Missing", Type.EmptyTypes, 1, []);
        missingSet.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
    }

    public void InvocationOverloadsPreserveResultsAndOriginalExceptions()
    {
        var accessor = new PrivateObject(new Fixture());
        Type[] types = [typeof(int)];
        object[] args = [7];
        accessor.Invoke("Double", args).Should().Be(14);
        accessor.Invoke("Double", types, args).Should().Be(14);
        accessor.Invoke("Double", args, CultureInfo.InvariantCulture).Should().Be(14);
        accessor.Invoke("Double", types, args, CultureInfo.InvariantCulture).Should().Be(14);
        accessor.Invoke("Double", InstanceFlags, args).Should().Be(14);
        accessor.Invoke("Double", InstanceFlags, types, args).Should().Be(14);
        accessor.Invoke("Double", InstanceFlags, args, CultureInfo.InvariantCulture).Should().Be(14);
        accessor.Invoke("Double", InstanceFlags, types, args, CultureInfo.InvariantCulture).Should().Be(14);
        accessor.Invoke("Double", InstanceFlags, types, args, null, null).Should().Be(14);
        Action inferredFailure = () => accessor.Invoke("Fail");
        inferredFailure.Should().ThrowExactly<InvalidOperationException>().WithMessage("instance failure");
        Action typedFailure = () => accessor.Invoke("Fail", Type.EmptyTypes, []);
        typedFailure.Should().ThrowExactly<InvalidOperationException>().WithMessage("instance failure");
        Action missing = () => accessor.Invoke("Missing", Type.EmptyTypes, []);
        missing.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
    }

    public void GenericInvocationSelectsSignaturesAndRejectsMismatches()
    {
        var accessor = new PrivateObject(new Fixture());
        accessor.Invoke("Generic", [typeof(int)], [79], [typeof(int)]).Should().Be("scalar:79");
        accessor.Invoke("Generic", InstanceFlags | BindingFlags.ExactBinding, [typeof(int[])], [new[] { 2, 3 }], null, [typeof(int)]).Should().Be("array:2");
        accessor.Invoke("Generic", [typeof(int), typeof(int)], [2, 3], [typeof(int), typeof(int)]).Should().Be("pair:2:3");
        accessor.Invoke("Generic", InstanceFlags | BindingFlags.ExactBinding, [typeof(int)], [83], null, [typeof(int)]).Should().Be("scalar:83");
        accessor.Invoke("Mixed", InstanceFlags | BindingFlags.ExactBinding, [typeof(int), typeof(string)], [89, "suffix"], null, [typeof(int)]).Should().Be("mixed:89:suffix");
        accessor.Invoke("Concrete", InstanceFlags | BindingFlags.ExactBinding, [typeof(string)], ["text"], null, [typeof(int)]).Should().Be("concrete:text");
        Action unknown = () => accessor.Invoke("Missing", [typeof(int)], [1], [typeof(int)]);
        unknown.Should().ThrowExactly<ArgumentException>().WithMessage("*Missing*");
        Action wrongArity = () => accessor.Invoke("Generic", [typeof(int)], [1], [typeof(int), typeof(int), typeof(int)]);
        wrongArity.Should().ThrowExactly<ArgumentException>();
        Action wrongShape = () => accessor.Invoke("ArrayOnly", InstanceFlags | BindingFlags.ExactBinding, [typeof(int)], [1], null, [typeof(int)]);
        wrongShape.Should().ThrowExactly<ArgumentException>();
        Action wrongConcrete = () => accessor.Invoke("Concrete", InstanceFlags | BindingFlags.ExactBinding, [typeof(int)], [1], null, [typeof(int)]);
        wrongConcrete.Should().ThrowExactly<ArgumentException>();
        Action wrongMixed = () => accessor.Invoke("Mixed", InstanceFlags | BindingFlags.ExactBinding, [typeof(int), typeof(int)], [1, 2], null, [typeof(int)]);
        wrongMixed.Should().ThrowExactly<ArgumentException>();
    }

    public void GenericCacheSelectsParameterlessMethodsAndReportsNoCandidates()
    {
        var accessor = new PrivateObject(new Fixture());
        MethodInfo cache = typeof(PrivateObject).GetMethod("GetGenericMethodFromCache", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var selected = (MethodInfo)cache.Invoke(accessor, ["NoArguments", Type.EmptyTypes, new[] { typeof(int) }, InstanceFlags])!;
        selected.MakeGenericMethod(typeof(int)).Invoke(accessor.Target, null).Should().Be("Int32");
        cache.Invoke(accessor, ["Missing", Type.EmptyTypes, new[] { typeof(int) }, InstanceFlags]).Should().BeNull();
    }

    public void EveryMemberOverloadRejectsNullNames()
    {
        var accessor = new PrivateObject(new Fixture());
        foreach (MethodInfo method in typeof(PrivateObject).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
            {
                continue;
            }

            object?[] args = parameters.Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
            Action invoke = () => method.Invoke(accessor, args);
            invoke.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<ArgumentNullException>()
                .Which.ParamName.Should().Be("name");
        }
    }

    public void AccessorErrorsBindingAndRefArgumentsPreserveContracts()
    {
        var fixture = new Fixture();
        var accessor = new PrivateObject(fixture);
        accessor.GetField("field", InstanceFlags | BindingFlags.IgnoreCase).Should().Be(3);
        object[] arguments = [7];
        accessor.Invoke("DoubleByRef", [typeof(int).MakeByRefType()], arguments).Should().Be(15);
        arguments.Should().Equal(14);
        Action arrayRank = () => accessor.GetArrayElement(nameof(Fixture.Matrix), 0);
        arrayRank.Should().ThrowExactly<ArgumentException>();
        Action outsideSet = () => accessor.SetArrayElement(nameof(Fixture.Matrix), value: 1, indices: [2, 0]);
        outsideSet.Should().ThrowExactly<IndexOutOfRangeException>();
        Action invalidElement = () => accessor.SetArrayElement(nameof(Fixture.Matrix), value: "invalid", indices: [0, 0]);
        invalidElement.Should().ThrowExactly<InvalidCastException>();
        fixture.Matrix.Cast<int>().Should().Equal(1, 2, 3, 4);
        Action getter = () => accessor.GetProperty(nameof(Fixture.ThrowingProperty));
        getter.Should().ThrowExactly<InvalidOperationException>().WithMessage("getter failure");
        Action setter = () => accessor.SetProperty(nameof(Fixture.ThrowingProperty), 1);
        setter.Should().ThrowExactly<InvalidOperationException>().WithMessage("setter failure");
        Action typedGetter = () => accessor.GetProperty(nameof(Fixture.ThrowingProperty), Type.EmptyTypes, []);
        typedGetter.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("getter failure");
        Action typedSetter = () => accessor.SetProperty(nameof(Fixture.ThrowingProperty), Type.EmptyTypes, 1, []);
        typedSetter.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("setter failure");
        Action invalidConstraint = () => accessor.Invoke("StructOnly", Type.EmptyTypes, [], [typeof(string)]);
        invalidConstraint.Should().ThrowExactly<ArgumentException>();
        var derived = new DerivedFixture();
        Action hiddenBaseField = () => new PrivateObject(derived).GetField("_baseField");
        hiddenBaseField.Should().ThrowExactly<MissingFieldException>();
        new PrivateObject(derived, new PrivateType(typeof(BaseFixture))).GetField("_baseField").Should().Be(101);
        var typeOnly = new PrivateObject((object)null!, new PrivateType(typeof(BaseFixture)));
        typeOnly.Target.Should().BeNull();
        typeOnly.RealType.Should().Be(typeof(BaseFixture));
    }

    internal sealed class Fixture
    {
#pragma warning disable IDE0051, IDE0044, IDE0032, SA1401 // Deliberately mutable fields and private members accessed through shipping reflection APIs.
        private readonly int[] _items = [10, 20];
        private int _secret = 7;

        public int Field = 3;
        public int[,] Matrix =
        {
            { 1, 2 },
            { 3, 4 },
        };

        public Fixture()
        {
        }

        private Fixture(int value) => Field = value;

        private Fixture(string value) => throw new InvalidOperationException("constructor:" + value);

        public Leaf Child { get; } = new();

        public int Property { get; set; } = 5;

        public int Secret => _secret;

        public int ThrowingProperty
        {
            get => throw new InvalidOperationException("getter failure");
            set => throw new InvalidOperationException("setter failure");
        }

        public int this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        private int Double(int value) => 2 * value;

        private int DoubleByRef(ref int value)
        {
            value *= 2;
            return value + 1;
        }

        private string StructOnly<T>()
            where T : struct
            => typeof(T).Name;

        private void Fail() => throw new InvalidOperationException("instance failure");

        private string Generic<T>(T value) => "scalar:" + value;

        private string Generic<T>(T[] values) => "array:" + values.Length;

        private string Generic<T, TOther>(T first, TOther second) => $"pair:{first}:{second}";

        private string Generic<T>(T first, T second) => $"same:{first}:{second}";

        private string Mixed<T>(T value, string suffix) => $"mixed:{value}:{suffix}";

        private string NoArguments<T>() => typeof(T).Name;

        private string ArrayOnly<T>(T[] values) => "array:" + values.Length;

        private string Concrete<T>(string value) => "concrete:" + value;
#pragma warning restore IDE0051, IDE0044, IDE0032, SA1401
    }

    internal sealed class Leaf
    {
        public string Value => "leaf";
    }

    private sealed class Value(int value)
    {
        public override bool Equals(object? obj) => obj is Value other && value == other.GetHashCode();

        public override int GetHashCode() => value;
    }

    private class BaseFixture
    {
#pragma warning disable IDE0051, IDE0044, CS0414 // The base-private field is reached only through PrivateObject's explicit declaring type.
        private int _baseField = 101;
#pragma warning restore IDE0051, IDE0044, CS0414
    }

    private sealed class DerivedFixture : BaseFixture
    {
    }
}
#endif
