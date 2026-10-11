# Custom testing framework

[`EnsureTestFramework`](EnsureTestFramework) implements a small Microsoft.Testing.Platform
test framework that reports discovered and executed test nodes.
[`MyTestProject`](MyTestProject) registers it with the test application builder in its
explicit entry point. The example demonstrates framework integration rather than MSTest tests.
