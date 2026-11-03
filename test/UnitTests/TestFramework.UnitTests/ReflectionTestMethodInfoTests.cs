// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class ReflectionTestMethodInfoTests : TestContainer
{
    public void GenericInvocation_ForwardsMetadataConstraintsAndDisplayName()
    {
        MethodInfo source = typeof(DerivedFixture).GetMethod(nameof(BaseFixture.Route))!;
        var method = new ReflectionTestMethodInfo(source, "MSTest route");
        MethodInfo closed = method.MakeGenericMethod(typeof(int));
        closed.Invoke(new DerivedFixture(), BindingFlags.InvokeMethod, null, ["route", 37], CultureInfo.InvariantCulture)
            .Should().Be("route:37");
        ((ReflectionTestMethodInfo)closed).DisplayName.Should().Be("MSTest route");
        new DataRowAttribute().GetDisplayName(closed, ["route", 37]).Should().Be("MSTest route (\"route\",37)");
        new ReflectionTestMethodInfo(source, null).DisplayName.Should().Be("Route");

        method.Name.Should().Be("Route");
        method.DeclaringType.Should().Be(typeof(BaseFixture));
        method.ReflectedType.Should().Be(typeof(DerivedFixture));
        method.ContainsGenericParameters.Should().BeTrue();
        method.IsGenericMethod.Should().BeTrue();
        method.IsGenericMethodDefinition.Should().BeTrue();
        closed.ContainsGenericParameters.Should().BeFalse();
        closed.IsGenericMethodDefinition.Should().BeFalse();
        closed.GetGenericArguments().Should().Equal(typeof(int));
        closed.GetGenericMethodDefinition().Equals(typeof(BaseFixture).GetMethod(nameof(BaseFixture.Route))).Should().BeTrue();
        method.GetBaseDefinition().DeclaringType.Should().Be(typeof(BaseFixture));
        method.Attributes.Should().HaveFlag(MethodAttributes.Public).And.HaveFlag(MethodAttributes.Virtual);
        method.GetMethodImplementationFlags().Should().HaveFlag(MethodImplAttributes.NoInlining);
        MethodBase.GetMethodFromHandle(method.MethodHandle)!.Name.Should().Be("Route");

        method.GetParameters().Select(parameter => parameter.Name).Should().Equal("prefix", "value");
        method.GetParameters().Should().BeSameAs(method.GetParameters());
        method.GetCustomAttributes(false).OfType<DescriptionAttribute>().Single().Description.Should().Be("generic route metadata");
        method.GetCustomAttributes(typeof(DescriptionAttribute), true).Cast<DescriptionAttribute>().Single().Description
            .Should().Be("generic route metadata");
        method.IsDefined(typeof(DescriptionAttribute), true).Should().BeTrue();
        method.IsDefined(typeof(IgnoreAttribute), true).Should().BeFalse();
        method.ReturnTypeCustomAttributes.GetCustomAttributes(typeof(ReturnRouteAttribute), false).Cast<ReturnRouteAttribute>()
            .Single().Label.Should().Be("return route");

        Action invalidType = () => method.MakeGenericMethod(typeof(string));
        invalidType.Should().ThrowExactly<ArgumentException>();
        Action invalidArity = () => closed.Invoke(new DerivedFixture(), BindingFlags.InvokeMethod, null, ["route"], CultureInfo.InvariantCulture);
        invalidArity.Should().ThrowExactly<TargetParameterCountException>();
    }

    public void Invoke_PropagatesRefArgumentsAndOriginalTargetException()
    {
        var target = new BaseFixture();
        var mutate = new ReflectionTestMethodInfo(typeof(BaseFixture).GetMethod(nameof(BaseFixture.Double))!, "double route");
        object?[] arguments = [37];
        mutate.Invoke(target, BindingFlags.InvokeMethod, null, arguments, CultureInfo.InvariantCulture).Should().Be("mutated");
        arguments[0].Should().Be(74);

        var original = new InvalidOperationException("target contract");
        var throwing = new ReflectionTestMethodInfo(typeof(BaseFixture).GetMethod(nameof(BaseFixture.Raise))!, null);
        Action action = () => throwing.Invoke(target, BindingFlags.InvokeMethod, null, [original], CultureInfo.InvariantCulture);
        action.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeSameAs(original);
    }

    private class BaseFixture
    {
        [Description("generic route metadata")]
        [return: ReturnRoute("return route")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual string Route<T>(string prefix, T value)
            where T : struct
            => $"{prefix}:{value}";

        public string Double(ref int value)
        {
            value *= 2;
            return "mutated";
        }

        public void Raise(Exception error) => throw error;
    }

    private sealed class DerivedFixture : BaseFixture;

    [AttributeUsage(AttributeTargets.ReturnValue)]
    private sealed class ReturnRouteAttribute(string label) : Attribute
    {
        public string Label { get; } = label;
    }
}
