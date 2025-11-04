// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;

namespace TestFramework.ForTestingMSTest;

internal sealed class TestFrameworkEngine : IDataProducer
{
    private readonly TestFrameworkExtension _extension;
    private readonly ILogger _logger;

    public TestFrameworkEngine(TestFrameworkExtension extension, ILoggerFactory loggerFactory)
    {
        _extension = extension;
        _logger = loggerFactory.CreateLogger("InternalTestFrameworkEngine");
    }

    public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public async Task<bool> IsEnabledAsync() => await _extension.IsEnabledAsync();

    public async Task ExecuteRequestAsync(TestExecutionRequest testExecutionRequest, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        switch (testExecutionRequest)
        {
            case DiscoverTestExecutionRequest discoveryRequest:
                await ExecuteTestNodeDiscoveryAsync(discoveryRequest, messageBus, cancellationToken);
                break;

            case RunTestExecutionRequest runRequest:
                await ExecuteTestNodeRunAsync(runRequest, messageBus, cancellationToken);
                break;

            default:
                throw new NotSupportedException($"Unexpected request type: '{testExecutionRequest.GetType().FullName}'");
        }
    }

    private async Task ExecuteTestNodeRunAsync(RunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_DEBUG_RUNTESTS") == "1")
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }

        Assembly assembly = Assembly.GetEntryAssembly()!;
        IEnumerable<TypeInfo> assemblyTestContainerTypes = assembly.DefinedTypes.Where(IsTestContainer);

        // TODO: Handle filtering
        foreach (TypeInfo testContainerType in assemblyTestContainerTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<MethodInfo> testContainerPublicMethods = testContainerType.DeclaredMethods.Where(memberInfo =>
                memberInfo.IsPublic
                && (memberInfo.ReturnType == typeof(void) || memberInfo.ReturnType == typeof(Task))
                && memberInfo.GetParameters().Length == 0);

            ConstructorInfo setupMethod = testContainerType.GetConstructor([])!;
            MethodInfo teardownMethod = testContainerType.BaseType!.GetMethod("Dispose")!;

            foreach (MethodInfo? publicMethod in testContainerPublicMethods)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TestNodeUid testNodeUid = $"{testContainerType.FullName}.{publicMethod.Name}";
                if (request.Filter is TestNodeUidListFilter testNodeUidListFilter
                    && !testNodeUidListFilter.TestNodeUids.Contains(testNodeUid))
                {
                    continue;
                }

                _logger.LogDebug($"Starting test '{publicMethod.Name}'");
                TestNode testNode = new()
                {
                    Uid = testNodeUid,
                    DisplayName = publicMethod.Name,
                };
                testNode.Properties.Add(new TestMethodIdentifierProperty(
                    assembly.FullName!,
                    testContainerType.Namespace!,
                    testContainerType.Name,
                    publicMethod.Name,
                    publicMethod.GetGenericArguments().Length,
                    [.. publicMethod.GetParameters().Select(x => x.ParameterType.FullName!)],
                    publicMethod.ReturnType.FullName!));

                testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(testContainerType.FullName!));

                TestNode progressNode = CloneTestNode(testNode);
                progressNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
                await messageBus.PublishAsync(this, new TestNodeUpdateMessage(request.Session.SessionUid, progressNode));

                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                bool isSuccessRun = false;
                bool isSuccessTeardown = false;

                try
                {
                    object? testClassInstance = await TryRunSetupMethodAsync(testContainerType, setupMethod, testNode, startTime, PublishNodeUpdateAsync);
                    if (testClassInstance is not null)
                    {
                        isSuccessRun = await RunTestMethodAsync(testClassInstance, publicMethod, testNode, startTime, PublishNodeUpdateAsync);
                    }

                    // Always call teardown even if previous steps failed because we want to try to clean as much as we can.
                    isSuccessTeardown = await RunTestTeardownAsync(testClassInstance, testContainerType, teardownMethod, testNode, startTime, PublishNodeUpdateAsync);
                }
                finally
                {
                    testNode.Properties.Add(CreateTimingProperty(startTime));
                }

                if (isSuccessRun && isSuccessTeardown)
                {
                    testNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
                    await PublishNodeUpdateAsync(testNode);
                }
            }
        }

        // Local functions
        Task PublishNodeUpdateAsync(TestNode testNode)
            => messageBus.PublishAsync(this, new TestNodeUpdateMessage(request.Session.SessionUid, testNode));
    }

    private async Task ExecuteTestNodeDiscoveryAsync(DiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_DEBUG_DISCOVERTESTS") == "1")
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }

        Assembly assembly = Assembly.GetEntryAssembly()!;
        _logger.LogDebug($"Discovering tests in assembly '{assembly.FullName}'");

        IEnumerable<TypeInfo> assemblyTestContainerTypes = assembly.DefinedTypes.Where(IsTestContainer);

        // TODO: Fail if no container?
        foreach (TypeInfo? testContainerType in assemblyTestContainerTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug($"Discovering tests for container '{testContainerType.FullName}'");

            IEnumerable<MethodInfo> testContainerPublicMethods = testContainerType.DeclaredMethods.Where(memberInfo =>
                memberInfo.IsPublic
                && (memberInfo.ReturnType == typeof(void) || memberInfo.ReturnType == typeof(Task))
                && memberInfo.GetParameters().Length == 0);

            // TODO: Fail if no public method?
            foreach (MethodInfo? publicMethod in testContainerPublicMethods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug($"Found test '{publicMethod.Name}'");
                TestNode testNode = new()
                {
                    Uid = $"{testContainerType.FullName}.{publicMethod.Name}",
                    DisplayName = publicMethod.Name,
                };
                testNode.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
                testNode.Properties.Add(new TestMethodIdentifierProperty(
                    assembly.FullName!,
                    testContainerType.Namespace!,
                    testContainerType.Name,
                    publicMethod.Name,
                    publicMethod.GetGenericArguments().Length,
                    [.. publicMethod.GetParameters().Select(x => x.ParameterType.FullName!)],
                    publicMethod.ReturnType.FullName!));

                await messageBus.PublishAsync(this, new TestNodeUpdateMessage(request.Session.SessionUid, testNode));
            }
        }
    }

    private static bool IsTestContainer(Type typeInfo)
    {
        Type? currentType = typeInfo;
        while (currentType != null)
        {
            if (currentType == typeof(TestContainer))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }

    private async Task<object?> TryRunSetupMethodAsync(TypeInfo testContainerType, ConstructorInfo setupMethod, TestNode testNode,
        DateTimeOffset startTime, Func<TestNode, Task> publishNodeUpdateAsync)
    {
        try
        {
            _logger.LogDebug($"Executing test '{testNode.DisplayName}' setup (ctor for '{testContainerType.FullName}')");
            return setupMethod.Invoke(null);
        }
        catch (Exception ex)
        {
            Exception realException = ex.InnerException ?? ex;
            _logger.LogError("Error during test setup", realException);
            TestNode errorNode = CloneTestNode(testNode);
            errorNode.Properties.Add(CreateTimingProperty(startTime));
            errorNode.Properties.Add(new ErrorTestNodeStateProperty(ex));
            errorNode.Properties.Add(new TrxExceptionProperty(ex.Message, ex.StackTrace));
            await publishNodeUpdateAsync(errorNode);
            return null;
        }
    }

    private async Task<bool> RunTestMethodAsync(object testClassInstance, MethodInfo publicMethod, TestNode testNode,
        DateTimeOffset startTime, Func<TestNode, Task> publishNodeUpdateAsync)
    {
        try
        {
            _logger.LogDebug($"Executing test '{testNode.DisplayName}'");
            if (publicMethod.Invoke(testClassInstance, null) is Task task)
            {
                await task;
            }

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex is TargetInvocationException ? ex.InnerException! : ex;
            _logger.LogError("Error during test", realException);
            TestNode errorNode = CloneTestNode(testNode);
            errorNode.Properties.Add(CreateTimingProperty(startTime));
            errorNode.Properties.Add(new ErrorTestNodeStateProperty(realException));
            errorNode.Properties.Add(new TrxExceptionProperty(realException.Message, realException.StackTrace));
            await publishNodeUpdateAsync(errorNode);

            return false;
        }
    }

    private async Task<bool> RunTestTeardownAsync(object? testClassInstance, TypeInfo testContainerType, MethodInfo teardownMethod, TestNode testNode,
        DateTimeOffset startTime, Func<TestNode, Task> publishNodeUpdateAsync)
    {
        try
        {
            if (testClassInstance is not null)
            {
                _logger.LogDebug($"Executing test '{testNode.DisplayName}' teardown (dispose for '{testContainerType.FullName}')");
                teardownMethod.Invoke(testClassInstance, null);
            }

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex.InnerException ?? ex;
            _logger.LogError("Error during test teardown", realException);
            TestNode errorNode = CloneTestNode(testNode);
            errorNode.Properties.Add(CreateTimingProperty(startTime));
            errorNode.Properties.Add(new ErrorTestNodeStateProperty(ex));
            await publishNodeUpdateAsync(errorNode);

            return false;
        }
    }

    private static TimingProperty CreateTimingProperty(DateTimeOffset startTime)
    {
        DateTimeOffset endTime = DateTimeOffset.UtcNow;
        TimeSpan duration = endTime - startTime;
        return new TimingProperty(new TimingInfo(startTime, endTime, duration));
    }

    private static TestNode CloneTestNode(TestNode testNode)
        => new()
        {
            Uid = testNode.Uid,
            DisplayName = testNode.DisplayName,
            Properties = new(testNode.Properties.AsEnumerable()),
        };
}
