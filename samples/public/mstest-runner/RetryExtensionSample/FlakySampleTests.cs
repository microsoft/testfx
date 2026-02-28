namespace RetryExtensionSample;

[TestClass]
public class FlakySampleTests
{
    [TestMethod]
    public void FlakyTest_FailsFirstTime_PassesOnRetry()
    {
        if (Environment.GetCommandLineArgs().Any(arg => arg.Contains("Retries") && !arg.EndsWith("2")))
        {
            Assert.Fail("Simulated failure on first execution");
        }
    }

    [TestMethod]
    public void NormalTest_AlwaysPasses()
    {
    }
}
