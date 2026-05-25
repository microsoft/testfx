// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestClassInfo object.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - not important to dispose the SemaphoreSlim, we don't access AvailableWaitHandle.
internal sealed partial class TestClassInfo
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly SemaphoreSlim _testClassExecuteSyncSemaphore = new(1, 1);

    private TestResult? _classInitializeResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestClassInfo"/> class.
    /// </summary>
    /// <param name="type">Underlying test class type.</param>
    /// <param name="constructor">Constructor for the test class.</param>
    /// <param name="isParameterlessConstructor">Whether or not the test class constructor has no parameters.</param>
    /// <param name="classAttribute">Test class attribute.</param>
    /// <param name="parent">Parent assembly info.</param>
    internal TestClassInfo(
        Type type,
        ConstructorInfo constructor,
        bool isParameterlessConstructor,
        TestClassAttribute classAttribute,
        TestAssemblyInfo parent)
    {
        ClassType = type;
        Constructor = constructor;
        IsParameterlessConstructor = isParameterlessConstructor;
        TestContextProperty = ResolveTestContext(type);
        Parent = parent;
        ClassAttribute = classAttribute;
    }

    /// <summary>
    /// Gets the class attribute.
    /// </summary>
    public TestClassAttribute ClassAttribute { get; }

    /// <summary>
    /// Gets the class type.
    /// </summary>
    public Type ClassType { get; }

    /// <summary>
    /// Gets the constructor.
    /// </summary>
    public ConstructorInfo Constructor { get; }

    internal bool IsParameterlessConstructor { get; }

    /// <summary>
    /// Gets the test context property.
    /// </summary>
    public PropertyInfo? TestContextProperty { get; }

    /// <summary>
    /// Gets the parent <see cref="TestAssemblyInfo"/>.
    /// </summary>
    public TestAssemblyInfo Parent { get; }

    /// <summary>
    /// Gets or sets the class initialize method.
    /// </summary>
    public MethodInfo? ClassInitializeMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassInit, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the timeout for the class initialize methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> ClassInitializeMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets the timeout for the class cleanup methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> ClassCleanupMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets the timeout for the test initialize methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> TestInitializeMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets the timeout for the test cleanup methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> TestCleanupMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether class initialize has executed.
    /// </summary>
    public bool IsClassInitializeExecuted { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether class cleanup has executed.
    /// </summary>
    public bool IsClassCleanupExecuted { get; internal set; }

    internal List<MethodInfo> BaseClassInitMethods { get; } = [];

    internal List<MethodInfo> BaseClassCleanupMethods { get; } = [];

    /// <summary>
    /// Gets or sets the exception thrown during <see cref="ClassInitializeAttribute"/> method invocation.
    /// </summary>
    public Exception? ClassInitializationException { get; internal set; }

    /// <summary>
    /// Gets or sets the exception thrown during <see cref="ClassCleanupAttribute"/> method invocation.
    /// </summary>
    public Exception? ClassCleanupException { get; internal set; }

    /// <summary>
    /// Gets a snapshot of <see cref="TestContext.Properties"/> captured after the
    /// <c>ClassInitialize</c> method completes. Used to flow properties set during
    /// <c>ClassInitialize</c> into subsequent contexts (test execution, class cleanup).
    /// <see langword="null"/> if no <c>ClassInitialize</c> method was registered or it has
    /// not yet executed successfully.
    /// <para>
    /// When the test class inherits from a base class that has a <c>ClassInitialize</c>
    /// method declared with <see cref="InheritanceBehavior.BeforeEachDerivedClass"/>, all
    /// class-init bodies in the chain run against the same context, so the captured snapshot
    /// includes properties set by both base and derived class-init methods.
    /// </para>
    /// <para>
    /// The snapshot is shallow: reference-type values stored in the bag are shared (aliased)
    /// across every context the snapshot is merged into.
    /// </para>
    /// <para>
    /// Reads and writes use <see cref="Volatile"/> so that callers on the cached-result fast
    /// path of <see cref="GetResultOrRunClassInitializeAsync"/> (which intentionally bypasses
    /// <see cref="_testClassExecuteSyncSemaphore"/>) safely observe the snapshot published by
    /// the thread that ran <c>ClassInitialize</c>. The publishing thread writes this snapshot
    /// before publishing <c>_classInitializeResult</c>, and both writes go through
    /// <see cref="Volatile"/>, so any reader that observes the cached result via
    /// <see cref="TryGetClonedCachedClassInitializeResult"/> is guaranteed to also see the
    /// published snapshot.
    /// </para>
    /// </summary>
    internal IReadOnlyDictionary<string, object?>? PostClassInitProperties
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    /// <summary>
    /// Gets or sets the class cleanup method.
    /// </summary>
    public MethodInfo? ClassCleanupMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassClean, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this class info has a executable cleanup method.
    /// </summary>
    public bool HasExecutableCleanupMethod
    {
        get
        {
            // If class has a cleanup method, then it is executable.
            if (ClassCleanupMethod is not null)
            {
                return true;
            }

            // Otherwise, if any base cleanups were pushed to the stack we need to run them
            return BaseClassCleanupMethods.Count != 0;
        }
    }

    /// <summary>
    /// Gets or sets the test initialize method.
    /// </summary>
    public MethodInfo? TestInitializeMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiInit, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the test cleanup method.
    /// </summary>
    public MethodInfo? TestCleanupMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClean, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    internal ExecutionContext? ExecutionContext { get; set; }

    /// <summary>
    /// Gets a queue of test initialize methods to call for this type.
    /// </summary>
    public Queue<MethodInfo> BaseTestInitializeMethodsQueue { get; } = new();

    /// <summary>
    /// Gets a queue of test cleanup methods to call for this type.
    /// </summary>
    public Queue<MethodInfo> BaseTestCleanupMethodsQueue { get; } = new();
}
