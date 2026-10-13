// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;
#if !NETFRAMEWORK
using System.Reflection.Emit;
#endif

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

#pragma warning disable MSTESTEXP // Tests exercise the experimental ambient context contract.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class TestContextTests : TestContainer
{
#if NETFRAMEWORK
    public void InvalidPropertyTypesReportTheKeyAndActualType()
    {
        // Debug.Fail must remain observable without opening an interactive assertion dialog.
        TraceListener[] previous = Debug.Listeners.Cast<TraceListener>().ToArray();
        using var listener = new AssertionListener();
        using var context = new Context();
        try
        {
            Debug.Listeners.Clear();
            Debug.Listeners.Add(listener);
            context.Properties["TestName"] = 42;
            Action read = () => _ = context.TestName;
            read.Should().ThrowExactly<InvalidCastException>().WithMessage("*TestName*System.Int32*System.String*");
#if DEBUG
            listener.Failure.Should().Be("How did an invalid value get in here?");
#endif
        }
        finally
        {
            Debug.Listeners.Clear();
            Debug.Listeners.AddRange(previous);
        }
    }

    private sealed class AssertionListener : TraceListener
    {
        public string? Failure { get; private set; }

        public override void Fail(string? message) => Failure = message;

        public override void Fail(string? message, string? detailMessage) => Failure = message;

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }
#endif

#if !NETFRAMEWORK
    public void InvalidPropertyTypesReportTheKeyAndActualType()
    {
        // DebugProvider is public in the runtime but omitted from net8/net9 reference assemblies.
        // Use the runtime's own diagnostic provider hook, not a replacement TestContext assembly.
        Type providerType = typeof(Debug).Assembly.GetType("System.Diagnostics.DebugProvider", throwOnError: true)!;
        MethodInfo setProvider = typeof(Debug).GetMethod("SetProvider", BindingFlags.Public | BindingFlags.Static)!;
        string? failure = null;
        object provider = CreateDiagnosticProvider(providerType, message => failure = message);
        object previous = setProvider.Invoke(null, [provider])!;
        using var context = new Context();
        try
        {
            context.Properties["TestName"] = 42;
            Action read = () => _ = context.TestName;
            read.Should().ThrowExactly<InvalidCastException>().WithMessage("*TestName*System.Int32*System.String*");
#if DEBUG
            failure.Should().Be("How did an invalid value get in here?");
#endif
        }
        finally
        {
            setProvider.Invoke(null, [previous]);
        }
    }

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

        MethodBuilder fail = type.DefineMethod("Fail", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [typeof(string), typeof(string)]);
        ILGenerator failBody = fail.GetILGenerator();
        failBody.Emit(OpCodes.Ldarg_0);
        failBody.Emit(OpCodes.Ldfld, callback);
        failBody.Emit(OpCodes.Ldarg_1);
        failBody.Emit(OpCodes.Callvirt, typeof(Action<string>).GetMethod("Invoke")!);
        failBody.Emit(OpCodes.Ret);
        type.DefineMethodOverride(fail, baseType.GetMethod("Fail")!);
        return Activator.CreateInstance(type.CreateType()!, record)!;
    }
#endif

    public void MissingAndNullPropertiesHaveDefinedDefaults()
    {
        using var context = new Context();
        Action className = () => _ = context.FullyQualifiedTestClassName;
        Action testName = () => _ = context.TestName;
        className.Should().ThrowExactly<InvalidOperationException>().WithMessage("*FullyQualifiedTestClassName*");
        testName.Should().ThrowExactly<InvalidOperationException>().WithMessage("*TestName*");
        context.Properties["TestName"] = null;
        testName.Should().ThrowExactly<InvalidOperationException>().WithMessage("*TestName*");
        context.CurrentTestOutcome.Should().Be(UnitTestOutcome.Unknown);
        context.TestRunCount.Should().Be(0);
#if !WIN_UI
        context.TestTempDirectory.Should().BeNull();
        context.TestRunDirectory.Should().BeNull();
        context.DeploymentDirectory.Should().BeNull();
        context.ResultsDirectory.Should().BeNull();
        context.TestRunResultsDirectory.Should().BeNull();
        context.TestResultsDirectory.Should().BeNull();
#endif
    }

#if !WIN_UI
    public void DirectoryPropertiesUseIndependentKeys()
    {
        using var context = new Context();
        context.Properties["TestRunDirectory"] = "run";
        context.Properties["DeploymentDirectory"] = "out";
        context.Properties["ResultsDirectory"] = "in";
        context.Properties["TestRunResultsDirectory"] = "machine";
        context.Properties["TestResultsDirectory"] = "results";
        context.TestRunDirectory.Should().Be("run");
        context.DeploymentDirectory.Should().Be("out");
        context.ResultsDirectory.Should().Be("in");
        context.TestRunResultsDirectory.Should().Be("machine");
        context.TestResultsDirectory.Should().Be("results");
    }
#endif

    public void MetadataAndCancellationArePerContext()
    {
        using var context = new Context();
        context.Properties["FullyQualifiedTestClassName"] = "Example.Tests";
        context.Properties["TestName"] = "Calculate";
        context.SetMetadata([3, "row"], "Calculation", new InvalidOperationException("failure"));
        context.FullyQualifiedTestClassName.Should().Be("Example.Tests");
        context.TestName.Should().Be("Calculate");
        context.TestData.Should().Equal(3, "row");
        context.TestDisplayName.Should().Be("Calculation");
        context.TestException!.Message.Should().Be("failure");
        context.CancellationToken.IsCancellationRequested.Should().BeFalse();
        context.CancellationTokenSource.Cancel();
        context.CancellationToken.IsCancellationRequested.Should().BeTrue();

        using var other = new Context();
        other.CancellationToken.IsCancellationRequested.Should().BeFalse();
        other.TestData.Should().BeNull();
        other.TestException.Should().BeNull();
        PropertyInfo runCount = typeof(TestContext).GetProperty(nameof(TestContext.TestRunCount))!;
        runCount.SetValue(context, 2);
        context.TestRunCount.Should().Be(2);
    }

    public async Task CurrentFlowsWithoutLeakingToCaller()
    {
        PropertyInfo current = typeof(TestContext).GetProperty(nameof(TestContext.Current))!;
        TestContext? previous = TestContext.Current;
        using var parent = new Context();
        using var child = new Context();
        try
        {
            current.SetValue(null, parent);
            await Task.Run(() =>
            {
                TestContext.Current.Should().BeSameAs(parent);
                current.SetValue(null, child);
                TestContext.Current.Should().BeSameAs(child);
            });
            TestContext.Current.Should().BeSameAs(parent);
            current.SetValue(null, null);
            TestContext.Current.Should().BeNull();
        }
        finally
        {
            current.SetValue(null, previous);
        }
    }

    private sealed class Context : TestContext, IDisposable
    {
        public override IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

#if NETFRAMEWORK
        public override System.Data.DataRow? DataRow => null;

        public override System.Data.Common.DbConnection? DataConnection => null;
#endif

        public void SetMetadata(object?[] data, string displayName, Exception exception)
        {
            TestData = data;
            TestDisplayName = displayName;
            TestException = exception;
        }

        public void Dispose() => CancellationTokenSource.Dispose();

        public override void AddResultFile(string fileName) => throw new NotSupportedException();

        public override void Write(string? message) => throw new NotSupportedException();

        public override void Write(string format, params object?[] args) => throw new NotSupportedException();

        public override void WriteLine(string? message) => throw new NotSupportedException();

        public override void WriteLine(string format, params object?[] args) => throw new NotSupportedException();

        public override void DisplayMessage(MessageLevel messageLevel, string message) => throw new NotSupportedException();
    }
}
