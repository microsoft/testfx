//namespace Microsoft.Testing.Platform.UI;

//public static class Program
//{
//    public static void Main()
//    {
//        new Simulator().Run();
//    }
//}

//public class Simulator
//{
//    public void Run()
//    {
//        ConsoleLogger logger = new ConsoleLogger();

//        logger.TestExecutionStarted(new TestRunStartedUpdate(workerCount: 20));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("abc.dll", 200, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("def.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("a.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("b.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("c.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("d.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("e.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("f.dll", 100, "net9.0", "x64"));
//        logger.AssemblyRunStarted(new AssemblyRunStartedUpdate("g.dll", 100, "net9.0", "x64"));

//        Exception error;
//        try
//        {
//            throw new InvalidOperationException("Oh my this is failing");
//        }
//        catch (Exception ex)
//        {
//            error = ex;
//        }
//        foreach (var i in Enumerable.Range(0, 200))
//        {
//            var errorChance = 10;
//            foreach  (var j in Enumerable.Range(1, 2))
//            {
//                if (j == 2 && i == 100)
//                {
//                    logger.AssemblyRunCompleted(new AssemblyRunCompletedUpdate("def.dll", "net9.0", "x64"));
//                    continue;
//                }
//                if (j == 2 && i > 100)
//                {
//                    continue;
//                }
//                var testCompleted = new TestUpdate
//                {
//                    Assembly = j == 1 ? "abc.dll" : "def.dll",
//                    TargetFramework = "net9.0",
//                    Architecture = "x64",
//                    Name = $"MyTest_{i}",
//                    Outcome = i % errorChance > 1 ? 1 : 0,
//                    Error = i % errorChance > 1 ? null : error,
//                };

//             Thread.Sleep(1001);
//                logger.TestCompleted(testCompleted);
//            }
//        }

//        logger.AssemblyRunCompleted(new AssemblyRunCompletedUpdate("abc.dll", "net9.0", "x64"));
//        logger.AssemblyRunCompleted(new AssemblyRunCompletedUpdate("def.dll", "net9.0", "x64"));

//        logger.TestExecutionCompleted(new TestExecutionCompleted { Timestamp = DateTime.UtcNow });
//    }
//}
