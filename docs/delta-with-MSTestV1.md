# MSTest V2 feature additions

For specific features and bug fixes in each version of MSTest V2, please see the [release notes](releases.md).

## High level behavior changes w.r.t MSTest V1

Listed are the difference in behavior of MSTest V2 w.r.t MSTest V1:

1. MSTest V2 sets the direction for how we intend to evolve the MSTest framework [[more...]](https://blogs.msdn.microsoft.com/devops/2016/06/17/taking-the-mstest-framework-forward-with-mstest-v2/).
2. MSTest V2 is "open source" [[more...]](https://blogs.msdn.microsoft.com/devops/2017/04/05/mstest-v2-is-open-source/).
3. Uniform app-platform support – this is a converged implementation that offers uniform app-platform support across .NET Framework, .NET Core and ASP.NET Core, and UWP [[more...]](https://blogs.msdn.microsoft.com/devops/2016/09/01/announcing-mstest-v2-framework-support-for-net-core-1-0-rtm/).
4. The implementation is fully cross platform (Windows, Linux, Mac) [[more...]](https://blogs.msdn.microsoft.com/devops/2017/04/05/mstest-v2-is-open-source/).
5. MSTest V2 support targeting .NET Framework 4.5.0+, .NET Core 1.0+ (Universal Windows Apps 10+, DNX Core 5+), and ASP.NET Core 1.0+
6. Provides a uniform, single end-user extensibility mechanism. [[more...]](https://blogs.msdn.microsoft.com/devops/2017/07/18/extending-mstest-v2/).
7. Delivered as a NuGet package [[more...]](https://www.nuget.org/packages/MSTest.TestFramework/).
8. Provides a uniform DataRow support, for all MSTest based test projects [[more...]](https://blogs.msdn.microsoft.com/devops/2017/02/25/mstest-v2-now-and-ahead/).
9. Provides the ability to place the TestCategory attribute at the level of a class or assembly [[more...]](https://blogs.msdn.microsoft.com/devops/2017/02/25/mstest-v2-now-and-ahead/).
10. Test methods from base classes defined in another assembly are now discovered and run from the derived Test class. This brings in a consistent behavior with derived test class types. If this behavior is not required for compat reasons it can be changed back using the following `runsettings`:

    ```xml
    <RunSettings>    
      <MSTest> 
        <EnableBaseClassTestMethodsFromOtherAssemblies>false</    EnableBaseClassTestMethodsFromOtherAssemblies> 
      </MSTest> 
    </RunSettings>
    ```

11. The TestCleanup method on a TestClass is invoked even if its corresponding `TestInitialize` method fails. See [#250](https://github.com/Microsoft/testfx/issues/250) for details.
12. The time taken by `AssemblyInitialize` and `ClassInitialize` are not considered as part of a tests duration thereby limiting their impact on a test timing out.
13. Test which are not runnable can be configured to be marked as failed via `MapNotRunnableToFailed` tag which is part of the adapter node in the runsettings.

    ```xml
    <RunSettings>    
      <MSTest> 
        <MapNotRunnableToFailed>true</MapNotRunnableToFailed> 
      </MSTest> 
    </RunSettings>
    ```

## Dropped features

Here are the features that are not supported:

1. Tests cannot be included into an "Ordered Test".
2. The`.testsettings` file does not support:
   * Configuring The adapter.
   * The `LegacySettings` tag.
3. The adapter does not support test lists specified as a `.vsmdi` file.
4. The `Coded UI Test Project`, and the `Web Performance and Load Test Project` types are not supported.
5. Association with a testcase item in TFS is not supported.
