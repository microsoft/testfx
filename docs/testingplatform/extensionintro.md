# Extensions introduction

As outlined in the [architecture](architecture.md) section, the testing platform is designed to accommodate a variety of scenarios and extensibility points. The primary and essential extension is undoubtedly the [testing framework](itestframework.md) that our tests will utilize. Failing to register it will result in a startup error. **The [testing framework](itestframework.md) is the sole mandatory extension required to execute a testing session.**

To support scenarios such as generating test reports, code coverage, retrying failed tests, and other potential features, we need to provide a mechanism that allows other extensions to work in conjunction with the [testing framework](itestframework.md) to deliver these features not inherently provided by the [testing framework](itestframework.md) itself.

In essence, the [testing framework](itestframework.md) is the primary extension that supplies information about each test that makes up our test suite. It reports whether a specific test has succeeded, failed, skipped, etc., and can provide additional information about each test, such as a human-readable name (referred to as the display name), the source file, and the line where our test begins, among other things.

The extensibility point allows us to utilize the information provided by the [testing framework](itestframework.md) to generate new artifacts or enhance existing ones with additional features. A commonly used extension is the TRX report generator, which subscribes to the [TestNodeUpdateMessage](testnodeupdatemessage.md) and generates an XML report file from it.

As discussed in the [architecture](architecture.md), there are certain extension points that *cannot* operate within the same process as the [testing framework](itestframework.md). The reasons typically include:

* The need to modify the *environment variables* of the *test host*. Acting within the test host process itself is *too late*.
* The requirement to *monitor* the process from the outside because the *test host*, where our tests and user code run, might have some *user code bugs* that render the process itself *unstable*, leading to potential *hangs* or *crashes*. In such cases, the extension would crash or hang along with the *test host* process.

Due to the reasons mentioned above, the extension points are categorized into two types:

* *In-process extensions*: These extensions operate within the same process as the [testing framework](itestframework.md).

You can register *in-process extensions* via the `ITestApplicationBuilder.TestHost` property:

```cs
...
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
testApplicationBuilder.TestHost.AddXXX(...);
...
```

* *Out-of-process extensions*: These extensions function in a separate process, allowing them to monitor the test host without being influenced by the test host itself.

You can register *out-of-process extensions* via the `ITestApplicationBuilder.TestHostControllers`.

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
testApplicationBuilder.TestHostControllers.AddXXX(...);
```

Lastly, some extensions are designed to function in both scenarios. These common extensions behave identically in both *hosts*. You can register these extensions either through the *TestHost* and *TestHostController* interfaces or directly at the `ITestApplicationBuilder` level. An example of such an extension is the [ICommandLineOptionsProvider](icommandlineoptionsprovider.md).
