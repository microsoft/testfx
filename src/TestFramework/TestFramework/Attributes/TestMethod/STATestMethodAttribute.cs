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

    internal STATestMethodAttribute(TestMethodAttribute? testMethodAttribute, string callerFilePath, int callerLineNumber)
        : base(callerFilePath, callerLineNumber)
        => _testMethodAttribute = testMethodAttribute;

    /// <summary>
    /// The core execution of STA test method, which happens on the STA thread.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>The test results of the test method.</returns>
    protected virtual Task<TestResult[]> ExecuteCoreAsync(ITestMethod testMethod)
        => _testMethodAttribute is null ? base.ExecuteAsync(testMethod) : _testMethodAttribute.ExecuteAsync(testMethod);

    /// <inheritdoc />
    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return ExecuteCoreAsync(testMethod);
        }

#if !NETFRAMEWORK
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO: Throw?
            return ExecuteCoreAsync(testMethod);
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
        return Task.FromResult(results!);
    }
}
