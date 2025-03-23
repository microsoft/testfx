using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject57
{
    public class MySynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback d, object? state)> _queue = new();

        public MySynchronizationContext()
        {
            new Thread(() =>
            {
                SetSynchronizationContext(this);
                while (true)
                {
                    if (_queue.TryDequeue(out var result))
                    {
                        result.d(result.state);
                    }
                }
            })
            {
                Name = "Sync context thread",
            }.Start();
        }
        public override SynchronizationContext CreateCopy()
            => new MySynchronizationContext();

        public override void Post(SendOrPostCallback d, object? state)
        {
            _queue.Enqueue((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
            => Post(d, state);
    }

    public class MyTestMethodAttribute : TestMethodAttribute
    {
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            TestResult result = null;
            var tcs = new TaskCompletionSource();
            var context = new MySynchronizationContext();
            context.Post(_ =>
            {
                // This Invoke is called on the "Sync context thread" thread.
                // When we "Invoke", MSTest will do GetAwaiter().GetResult() in InvokeAsSynchronousTask.
                // So, we are blocking the "Sync context thread" thread here.
                result = testMethod.Invoke(null);
                tcs.TrySetResult();
            }, null);

            tcs.Task.GetAwaiter().GetResult();
            return new TestResult[] { result };
        }
    }

    [TestClass]
    public sealed class Test1
    {
        [MyTestMethod]
        public async Task TestMethod1()
        {
            Console.WriteLine("1");
            await Task.Delay(100);
            // The continuation is handed to the MySynchronizationContext queue.
            // But InvokeAsSynchronousTask has already blocked the "Sync context thread" thread.
            // So nothing can progress here. The "Sync context thread" is blocked until this task completes,
            // But for the task to complete, it needs to do work on the blocked thread.
            Console.WriteLine("2");
            await Task.Delay(100);
            Console.WriteLine("3");
        }
    }
}
