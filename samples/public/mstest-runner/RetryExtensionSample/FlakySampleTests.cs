namespace RetryExtensionSample;

/// <summary>
/// This sample demonstrates how to use the Microsoft.Testing.Extensions.Retry
/// extension to retry failed tests. The test will fail on the first attempt
/// and pass on the retry.
/// </summary>
[TestClass]
public class FlakySampleTests
{
    // Static field to track test execution count
    private static int _executionCount = 0;

    /// <summary>
    /// This test simulates a flaky test that fails on the first run
    /// and passes on subsequent runs. This pattern can occur in real-world
    /// scenarios like:
    /// - Network requests that might timeout
    /// - Database connections that might be temporarily unavailable
    /// - File system operations that might be locked
    /// - Race conditions in concurrent code
    /// </summary>
    [TestMethod]
    public void FlakyTest_FailsFirstTime_PassesOnRetry()
    {
        _executionCount++;
        
        Console.WriteLine($"Test execution count: {_executionCount}");
        
        // Fail on the first execution, pass on subsequent executions
        if (_executionCount == 1)
        {
            Console.WriteLine("First execution - simulating failure");
            Assert.Fail("Simulated failure on first execution");
        }
        
        Console.WriteLine("Test passed on retry!");
    }
    
    /// <summary>
    /// This test always passes to show normal test behavior.
    /// </summary>
    [TestMethod]
    public void NormalTest_AlwaysPasses()
    {
        Console.WriteLine("This test always passes");
        Assert.IsTrue(true);
    }
}
