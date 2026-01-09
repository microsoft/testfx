// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test class attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class STATestMethodAttribute : TestMethodAttribute
{
    private readonly TestMethodAttribute? _testMethodAttribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="STATestMethodAttribute"/> class.
    /// </summary>
    public STATestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="STATestMethodAttribute"/> class.
    /// This constructor is intended to be called by <see cref="STATestClassAttribute"/> (or derived) to wrap an existing test method attribute.
    /// It can also be called by a derived <see cref="STATestMethodAttribute"/> which will be called by a derived <see cref="STATestClassAttribute"/>.
    /// </summary>
    /// <param name="testMethodAttribute">The wrapped test method.</param>
    public STATestMethodAttribute(TestMethodAttribute testMethodAttribute)
        : base(testMethodAttribute.DeclaringFilePath, testMethodAttribute.DeclaringLineNumber ?? -1)
        => _testMethodAttribute = testMethodAttribute;

    /// <summary>
    ///  Gets or sets a value indicating whether the attribute will set a <see cref="SynchronizationContext"/> that preserves the same
    ///  STA thread for async continuations.
    ///  The default is <see langword="false"/>.
    /// </summary>
    public bool UseSTASynchronizationContext { get; set; }

    /// <summary>
    /// The core execution of STA test method, which happens on the STA thread.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>The test results of the test method.</returns>
    protected virtual Task<TestResult[]> ExecuteCoreAsync(ITestMethod testMethod)
        => _testMethodAttribute is null ? base.ExecuteAsync(testMethod) : _testMethodAttribute.ExecuteAsync(testMethod);

    /// <inheritdoc />
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (UseSTASynchronizationContext)
        {
            SynchronizationContext? originalContext = SynchronizationContext.Current;
            try
            {
                var syncContext = new SingleThreadedSTASynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);

                // The yield ensures that we switch to the STA thread created by SingleThreadedSTASynchronizationContext.
                await Task.Yield();
                TestResult[] testResults = await ExecuteCoreAsync(testMethod).ConfigureAwait(false);
                syncContext.Complete();
                return testResults;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalContext);
            }
        }

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return await ExecuteCoreAsync(testMethod).ConfigureAwait(false);
        }

#if !NETFRAMEWORK
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO: Throw?
            return await ExecuteCoreAsync(testMethod).ConfigureAwait(false);
        }
#endif

        TestResult[]? results = null;
        var t = new Thread(() => results = ExecuteCoreAsync(testMethod).GetAwaiter().GetResult())
        {
            Name = "STATestMethodAttribute thread",
        };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        return results!;
    }
}
