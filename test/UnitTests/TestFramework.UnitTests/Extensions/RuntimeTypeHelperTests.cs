// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class RuntimeTypeHelperTests : TestContainer
{
    private static readonly Type Helper = typeof(PrivateObject).Assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.RuntimeTypeHelper", true)!;

    public void SignaturesAndHierarchyChooseTheMostDerivedDeclaration()
    {
        MethodInfo objectMethod = Method(nameof(Base.Object));
        MethodInfo stringMethod = Method(nameof(Base.String));
        MethodInfo twoMethod = Method(nameof(Base.Two));
        Call<bool>("CompareMethodSigAndName", objectMethod, stringMethod).Should().BeFalse();
        Call<bool>("CompareMethodSigAndName", objectMethod, twoMethod).Should().BeFalse();
        Call<bool>("CompareMethodSigAndName", objectMethod, Method(nameof(Base.OtherObject))).Should().BeTrue();
        Call<int>("GetHierarchyDepth", typeof(object)).Should().Be(1);
        Call<int>("GetHierarchyDepth", typeof(Base)).Should().Be(2);
        Call<int>("GetHierarchyDepth", typeof(Derived)).Should().Be(3);
        MethodInfo derived = typeof(Derived).GetMethod(nameof(Base.Object), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Call<MethodBase?>("FindMostDerivedNewSlotMeth", Array.Empty<MethodBase>(), 0).Should().BeNull();
        Call<MethodBase>("FindMostDerivedNewSlotMeth", new MethodBase[] { objectMethod, derived }, 2).Should().BeSameAs(derived);
        Call<MethodBase>("FindMostDerivedNewSlotMeth", new MethodBase[] { derived, objectMethod }, 2).Should().BeSameAs(derived);

        AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Vararg_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        TypeBuilder builder = assembly.DefineDynamicModule("Methods").DefineType("VarargMethods");
        foreach (string name in new[] { "First", "Second" })
        {
            MethodBuilder method = builder.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, CallingConventions.VarArgs, typeof(void), Type.EmptyTypes);
            method.GetILGenerator().Emit(OpCodes.Ret);
        }

        Type varargs = builder.CreateType()!;
        Action tie = () => Call<MethodBase>("FindMostDerivedNewSlotMeth", varargs.GetMethods(BindingFlags.Public | BindingFlags.Static), 2);
        tie.Should().ThrowExactly<AmbiguousMatchException>();
    }

    public void CandidateSelectionFiltersArityShapeAndAssignability()
    {
        MethodInfo obj = Method(nameof(Base.Object));
        MethodInfo str = Method(nameof(Base.String));
        MethodInfo number = Method(nameof(Base.Number));
        MethodInfo generic = Method(nameof(Base.Generic));
        MethodInfo array = Method(nameof(Base.Array));
        Call<MethodBase?>("SelectMethod", Array.Empty<MethodBase>(), new[] { typeof(string) }).Should().BeNull();
        Call<MethodBase?>("SelectMethod", new MethodBase[] { Method(nameof(Base.Two)) }, new[] { typeof(string) }).Should().BeNull();
        Call<MethodBase?>("SelectMethod", new MethodBase[] { number }, new[] { typeof(string) }).Should().BeNull();
        Call<MethodBase?>("SelectMethod", new MethodBase[] { array }, new[] { typeof(string) }).Should().BeNull();
        Call<MethodBase>("SelectMethod", new MethodBase[] { array }, new[] { typeof(string[]) }).Should().BeSameAs(array);
        Call<MethodBase>("SelectMethod", new MethodBase[] { generic }, new[] { typeof(string) }).Should().BeSameAs(generic);
        Call<MethodBase>("SelectMethod", new MethodBase[] { obj }, new[] { typeof(string) }).Should().BeSameAs(obj);
        Call<MethodBase>("SelectMethod", new MethodBase[] { Method(nameof(Base.Interface)) }, new[] { typeof(string) }).Name.Should().Be(nameof(Base.Interface));
        Call<MethodBase>("SelectMethod", new MethodBase[] { obj, str }, new[] { typeof(string) }).Should().BeSameAs(str);
        Call<MethodBase>("SelectMethod", new MethodBase[] { str, obj }, new[] { typeof(string) }).Should().BeSameAs(str);
        Action ambiguous = () => Call<MethodBase>("SelectMethod", new MethodBase[] { obj, Method(nameof(Base.OtherObject)) }, new[] { typeof(string) });
        ambiguous.Should().ThrowExactly<AmbiguousMatchException>();
    }

    public void MethodSpecificityResolvesTypesThenHierarchy()
    {
        MethodInfo obj = Method(nameof(Base.Object));
        MethodInfo str = Method(nameof(Base.String));
        MethodInfo derived = typeof(Derived).GetMethod(nameof(Base.Object), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Specific(obj, str, [typeof(string)]).Should().Be(2);
        Specific(str, obj, [typeof(string)]).Should().Be(1);
        Specific(obj, derived, [typeof(string)]).Should().Be(2);
        Specific(derived, obj, [typeof(string)]).Should().Be(1);
        Specific(obj, Method(nameof(Base.OtherObject)), [typeof(string)]).Should().Be(0);
        Specific(Method(nameof(Base.Number)), str, [typeof(object)]).Should().Be(0);
    }

    public void ParameterSpecificityHonorsParamsMissingArgumentsAndOrder()
    {
        ParameterInfo[] obj = Method(nameof(Base.Object)).GetParameters();
        ParameterInfo[] str = Method(nameof(Base.String)).GetParameters();
        ParameterInfo[] two = Method(nameof(Base.Two)).GetParameters();
        Call<int>("FindMostSpecific", obj, new[] { 0 }, typeof(object), str, new[] { 0 }, null!, new[] { typeof(string) }, null!).Should().Be(2);
        Call<int>("FindMostSpecific", obj, new[] { 0 }, null!, str, new[] { 0 }, typeof(string), new[] { typeof(string) }, null!).Should().Be(1);
        Call<int>("FindMostSpecific", obj, new[] { 0 }, null!, str, new[] { 0 }, null!, new[] { typeof(string) }, new object[] { Type.Missing }).Should().Be(0);
        Call<int>("FindMostSpecific", obj, new[] { 0 }, null!, two, new[] { 0 }, null!, new[] { typeof(object) }, new object[] { "x" }).Should().Be(1);
        Call<int>("FindMostSpecific", two, new[] { 0 }, null!, obj, new[] { 0 }, null!, new[] { typeof(object) }, new object[] { "x" }).Should().Be(2);
        Call<int>("FindMostSpecific", two, Array.Empty<int>(), null!, obj, Array.Empty<int>(), null!, Type.EmptyTypes, Array.Empty<object>()).Should().Be(0);
        ParameterInfo[] generic = Method(nameof(Base.Generic)).GetParameters();
        Call<int>("FindMostSpecific", generic, new[] { 0 }, null!, str, new[] { 0 }, null!, new[] { typeof(string) }, null!).Should().Be(0);
        Call<int>("FindMostSpecific", obj, new[] { 0 }, typeof(object), str, new[] { 0 }, typeof(string), new[] { typeof(string) }, null!).Should().Be(2);
        ParameterInfo[] left = Method(nameof(Base.Left)).GetParameters();
        ParameterInfo[] right = Method(nameof(Base.Right)).GetParameters();
        Call<int>("FindMostSpecific", left, new[] { 0, 1 }, null!, right, new[] { 0, 1 }, null!, new[] { typeof(string), typeof(string) }, null!).Should().Be(0);
        Call<int>("FindMostSpecific", left, new[] { 1, 0 }, null!, right, new[] { 0, 1 }, null!, new[] { typeof(string), typeof(string) }, null!).Should().Be(0);
    }

    public void TypeSpecificityCoversExactPrimitiveReferenceAndByRefPartitions()
    {
        (Type First, Type Second, Type Actual, int Expected)[] cases =
        [
            (typeof(string), typeof(string), typeof(object), 0),
            (typeof(string), typeof(object), typeof(string), 1),
            (typeof(object), typeof(string), typeof(string), 2),
            (typeof(object), typeof(string), typeof(Uri), 2),
            (typeof(string), typeof(object), typeof(Uri), 1),
            (typeof(int), typeof(double), typeof(short), 0),
            (typeof(string), typeof(Uri), typeof(object), 0),
            (typeof(string).MakeByRefType(), typeof(string), typeof(object), 2),
            (typeof(string), typeof(string).MakeByRefType(), typeof(object), 1),
            (typeof(object).MakeByRefType(), typeof(string).MakeByRefType(), typeof(Uri), 2),
            (typeof(object).MakeByRefType(), typeof(string), typeof(Uri), 2),
            (typeof(string), typeof(object).MakeByRefType(), typeof(Uri), 1),
        ];
        foreach ((Type First, Type Second, Type Actual, int Expected) item in cases)
        {
            Call<int>("FindMostSpecificType", item.First, item.Second, item.Actual).Should().Be(item.Expected);
        }
    }

    private static int Specific(MethodBase first, MethodBase second, Type[] types)
        => Call<int>("FindMostSpecificMethod", first, new[] { 0 }, null!, second, new[] { 0 }, null!, types, null!);

    private static MethodInfo Method(string name) => typeof(Base).GetMethod(name)!;

    private static T Call<T>(string name, params object?[] arguments)
    {
        try
        {
            return (T)Helper.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private class Base
    {
        public void Object(object value)
        {
        }

        public void OtherObject(object value)
        {
        }

        public void String(string value)
        {
        }

        public void Number(int value)
        {
        }

        public void Two(object first, object second)
        {
        }

        public void Interface(IComparable value)
        {
        }

        public void Generic<T>(T value)
        {
        }

        public void Array<T>(T[] value)
        {
        }

        public void Left(string first, object second)
        {
        }

        public void Right(object first, string second)
        {
        }
    }

    private sealed class Derived : Base
    {
        public new void Object(object value)
        {
        }
    }
}
#endif
