# MSTest.Extensions.Hosting

`MSTest.Extensions.Hosting` lets an application-owned `Microsoft.Extensions.Hosting` container create MSTest test classes.

This package remains prerelease, including in MSTest RTM builds, while its `Microsoft.Testing.Extensions.Hosting` dependency is alpha.

Register the integration while building the same host that runs Microsoft Testing Platform:

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Services.AddSingleton<MyApplicationService>();
builder.Services.AddMSTestTestClassInjection();
```

Test classes can then request registered services in a public constructor:

```csharp
[TestClass]
public sealed class MyTests(MyApplicationService service, TestContext testContext)
{
    [TestMethod]
    public void UsesApplicationService()
        => Assert.IsNotNull(service);
}
```

The integration is explicit and affects only MSTest test-class construction. It creates one `IServiceScope` for every test invocation, including each data row and retry attempt. `TestInitialize`, the test method, and `TestCleanup` share the same test-class instance and scope. Static `ClassInitialize` and `ClassCleanup` run outside invocation scopes.

The application host remains caller-owned. MSTest never disposes the host or its root service provider. After `TestCleanup`, the integration disposes the test-class instance and then its invocation scope.

NativeAOT, browser WebAssembly, AOT compilation, and MSTest source generation are not supported by this first reflection-based integration. Calling `AddMSTestTestClassInjection` in one of those modes produces a deterministic build error, including when host registration is compiled into a referenced project.
