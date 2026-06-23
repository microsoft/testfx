// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.SourceGeneration;

/// <summary>
/// Verifies that <see cref="SourceGeneratedReflectionOperations"/> serves the delegate-based
/// invokers published by the AOT source generator (constructor factories, method invokers,
/// property setters) so the adapter runs tests without <c>MethodInfo.Invoke</c> /
/// <c>Activator.CreateInstance</c> / <c>PropertyInfo.SetValue</c>.
/// </summary>
public sealed class SourceGeneratedReflectionOperationsTests : TestContainer
{
    public void GetTestMethodInvoker_ReturnsRegisteredDelegate_AndInvokesWithoutReflection()
    {
        MethodInfo add = typeof(Sample).GetMethod(nameof(Sample.Add))!;

        // Mirrors the source-generated contract: the invoker calls the method directly and returns a
        // non-null Task representing completion; the return value is discarded (the method's effect is
        // observed via state instead).
        Func<object?, object?[]?, object?> invoker = static (instance, args) =>
        {
            ((Sample)instance!).Add((int)args![0]!, (int)args![1]!);
            return Task.CompletedTask;
        };

        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodInvokers = { [add] = invoker },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetTestMethodInvoker(add).Should().BeSameAs(invoker);

        var sample = new Sample();
        object? result = operations.GetTestMethodInvoker(add)!(sample, [2, 3]);
        result.Should().BeAssignableTo<Task>();
        ((Task)result!).IsCompleted.Should().BeTrue();
        sample.LastSum.Should().Be(5);
    }

    public void GetTestMethodInvoker_ReturnsNull_WhenMethodNotRegistered()
    {
        MethodInfo unregistered = typeof(Sample).GetMethod(nameof(Sample.Add))!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetTestMethodInvoker(unregistered).Should().BeNull();
    }

    public void GetConstructorInvoker_CreatesInstance_WithoutActivator()
    {
        SourceGeneratedReflectionDataProvider.ConstructorInvoker[] invokers =
        [
            new SourceGeneratedReflectionDataProvider.ConstructorInvoker
            {
                Parameters = [typeof(string)],
                Invoker = static args => new Sample((string)args![0]!),
            },
        ];
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = { [typeof(Sample)] = invokers },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        Func<object?[]?, object>? constructorInvoker = operations.GetConstructorInvoker(typeof(Sample));
        constructorInvoker.Should().NotBeNull();
        ((Sample)constructorInvoker!(["hello"])).Value.Should().Be("hello");

        // CreateInstance routes through the same invoker (no Activator.CreateInstance).
        ((Sample)operations.CreateInstance(typeof(Sample), ["world"])!).Value.Should().Be("world");
    }

    public void GetConstructorInvoker_ReturnsNull_WhenTypeNotRegistered()
    {
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetConstructorInvoker(typeof(Sample)).Should().BeNull();
    }

    public void GetConstructorInvoker_FallsBackToReflection_WhenArgumentsDoNotMatchRegisteredConstructor()
    {
        // Only the (string) constructor is registered, but Sample also has a parameterless one.
        SourceGeneratedReflectionDataProvider.ConstructorInvoker[] invokers =
        [
            new SourceGeneratedReflectionDataProvider.ConstructorInvoker
            {
                Parameters = [typeof(string)],
                Invoker = static args => new Sample((string)args![0]!),
            },
        ];
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = { [typeof(Sample)] = invokers },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        // An empty argument list matches no registered constructor; the invoker must fall back to
        // reflection (the parameterless constructor) rather than throwing MissingMethodException.
        Func<object?[]?, object>? constructorInvoker = operations.GetConstructorInvoker(typeof(Sample));
        constructorInvoker.Should().NotBeNull();
        object instance = constructorInvoker!([]);
        instance.Should().BeOfType<Sample>();
        ((Sample)instance).Value.Should().BeNull();
    }

    public void GetPropertySetter_AssignsValue_WithoutSetValue()
    {
        PropertyInfo property = typeof(Sample).GetProperty(nameof(Sample.Value))!;
        Action<object?, object?> setter = static (instance, value) => ((Sample)instance!).Value = (string?)value;

        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypePropertySetters = { [property] = setter },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetPropertySetter(property).Should().BeSameAs(setter);

        var sample = new Sample();
        operations.GetPropertySetter(property)!(sample, "assigned");
        sample.Value.Should().Be("assigned");
    }

    public void GetPropertySetter_ReturnsNull_WhenPropertyNotRegistered()
    {
        PropertyInfo property = typeof(Sample).GetProperty(nameof(Sample.Value))!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetPropertySetter(property).Should().BeNull();
    }

    private sealed class Sample
    {
        public Sample()
        {
        }

        public Sample(string value) => Value = value;

        public string? Value { get; set; }

        public int LastSum { get; private set; }

        public int Add(int first, int second)
        {
            LastSum = first + second;
            return LastSum;
        }
    }
}
