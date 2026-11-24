// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoExecutionMutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";
    private const string TestCleanupMethodName = "TestCleanup";
    private const string TestMethodCallsCounter = "TestMethodCalls";
    private const string TestCleanupCallsCounter = "TestCleanupCalls";

    public async Task ExecuteInternalReportsTheConfiguredTimeoutWhenTheTimeoutTokenWasCancelled()
    {
        using var harness = new ExecutionHarness(typeof(CancelingFixture), TestMethodName);
        harness.Subject.TimeoutInfo = TimeoutInfo.FromTimeout(1234);
        using var timeoutTokenSource = new CancellationTokenSource();
        timeoutTokenSource.Cancel();

        TestResult result = await harness.ExecuteInternalAsync(timeoutTokenSource);

        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
            string.Format(CultureInfo.InvariantCulture, Resource.Execution_Test_Timeout, TestMethodName, 1234));
    }

    public async Task ExecuteInternalPropagatesTheExceptionsThatEscapeTheTestFailureHandling()
    {
        using var harness = new ExecutionHarness(typeof(DisposingFixture), TestMethodName);

        // Disposing the cancellation source makes the handling of the test failure itself throw.
        Func<Task> execute = () => harness.ExecuteInternalAsync(null);

        await execute.Should().ThrowAsync<ObjectDisposedException>();
    }

    public async Task ExecuteInternalRunsTheTestAndTheCleanupOfAnFSharpModuleWithoutAnInstance()
    {
        Type moduleType = CreateRecordingFSharpModule();
        using var harness = new ExecutionHarness(moduleType, TestMethodName);
        harness.ClassInfo.TestCleanupMethod = moduleType.GetMethod(TestCleanupMethodName);

        TestResult result = await harness.ExecuteInternalAsync(null);

        result.TestFailureException.Should().BeNull();
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        ReadCounter(moduleType, TestMethodCallsCounter).Should().Be(1);
        ReadCounter(moduleType, TestCleanupCallsCounter).Should().Be(1);
    }

    private static int ReadCounter(Type moduleType, string counterName)
        => (int)moduleType.GetField(counterName)!.GetValue(null)!;

    private static Type CreateRecordingFSharpModule()
    {
        // Model the compiler metadata of an F# module without adding a dependency on FSharp.Core.
        var assemblyName = new AssemblyName("FSharp.Core");
#if NETFRAMEWORK
        AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("ExecutionMutationTests");
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

        TypeBuilder typeBuilder = moduleBuilder.DefineType("ExecutionMutationTests.RecordingModule", TypeAttributes.Public);
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(attributeType.GetConstructor([typeof(int)])!, [7]));
        DefineRecordingMethod(typeBuilder, TestMethodName, TestMethodCallsCounter);
        DefineRecordingMethod(typeBuilder, TestCleanupMethodName, TestCleanupCallsCounter);

        return typeBuilder.CreateTypeInfo()!.AsType();
    }

    private static void DefineRecordingMethod(TypeBuilder typeBuilder, string methodName, string counterName)
    {
        FieldBuilder counter = typeBuilder.DefineField(counterName, typeof(int), FieldAttributes.Public | FieldAttributes.Static);
        MethodBuilder methodBuilder = typeBuilder.DefineMethod(methodName, MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        ILGenerator il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, counter);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stsfld, counter);
        il.Emit(OpCodes.Ret);
    }

    private sealed class ExecutionHarness : IDisposable
    {
        private static readonly MethodInfo ExecuteInternalMethod = typeof(TestMethodInfo).GetMethod("ExecuteInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly IPlatformServiceProvider _previousProvider;

        public ExecutionHarness(Type fixtureType, string methodName)
        {
            _previousProvider = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = services;

            Context = new TestContextImplementation(null, fixtureType.FullName, new Dictionary<string, object?>(), null, null);
            var assemblyInfo = new TestAssemblyInfo(typeof(ExecutionHarness).Assembly);
            ClassInfo = new TestClassInfo(fixtureType, fixtureType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), assemblyInfo);
            Subject = new TestMethodInfo(fixtureType.GetMethod(methodName)!, ClassInfo) { TestContext = Context };
        }

        public TestContextImplementation Context { get; }

        public TestClassInfo ClassInfo { get; }

        public TestMethodInfo Subject { get; }

        public Task<TestResult> ExecuteInternalAsync(CancellationTokenSource? timeoutTokenSource)
            => (Task<TestResult>)ExecuteInternalMethod.Invoke(Subject, [null, timeoutTokenSource, null])!;

        public void Dispose()
            => PlatformServiceProvider.Instance = _previousProvider;
    }

    private sealed class CancelingFixture
    {
        public TestContext? TestContext { get; set; }

        public void TestMethod()
            => throw new OperationCanceledException(TestContext!.CancellationTokenSource.Token);
    }

    private sealed class DisposingFixture
    {
        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
            TestContext!.CancellationTokenSource.Dispose();
            throw new InvalidOperationException("The test failed.");
        }
    }
}
