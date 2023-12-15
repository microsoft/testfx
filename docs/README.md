# MSTest Documentation

The following official [learn.microsoft.com website](https://learn.microsoft.com/visualstudio/test/unit-test-basics) contains all the information about writing unit tests, unit test frameworks, integration with CLI and Visual Studio.

This [blog post](https://devblogs.microsoft.com/devops/mstest-v2-now-and-ahead/) announce the vision for MSTest V2.

For API documentation refer [here](https://docs.microsoft.com/dotnet/api/microsoft.visualstudio.testtools.unittesting).

## Contributing

- See [CONTRIBUTING.md](../CONTRIBUTING.md) for details about how you can contribute.
- See [dev-guide.md](dev-guide.md) for more details on configurations for building the codebase. In practice, you only really need to run `build.cmd`/`build.sh`.

## Features

You can find the main differences with MSTest v1 in [Deltas w.r.t MSTest V1](delta-with-MSTestV1.md).

You can find detailed examples and explanation of MSTest features at

- [MSTest element via runsettings](https://learn.microsoft.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#mstest-element)
- [Use the MSTest framework in unit tests](https://learn.microsoft.com/visualstudio/test/using-microsoft-visualstudio-testtools-unittesting-members-in-unit-tests)
- [Create a data-driven unit test](https://learn.microsoft.com/visualstudio/test/how-to-create-a-data-driven-unit-test)
- [Run selected unit tests](https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests?pivots=mstest)
- [Upgrade from MSTestV1 to MSTestV2](https://learn.microsoft.com/visualstudio/test/mstest-update-to-mstestv2)
- [Using a configuration file to define a data source](https://learn.microsoft.com/visualstudio/test/walkthrough-using-a-configuration-file-to-define-a-data-source)
- [MSTest Runner](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-runner-intro)

For technical reasoning and implementation details, you can refer to the list of RFCs:

- [Writing your first test with mstest](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-mstest)
- [Framework Extensibility Trait Attributes](RFCs/001-Framework-Extensibility-Trait-Attributes.md)
- [Framework Extensibility for Custom Assertions](RFCs/002-Framework-Extensibility-Custom-Assertions.md)
- [Customize Running tests](RFCs/003-Customize-Running-Tests.md)
- [In-assembly parallel execution](RFCs/004-In-Assembly-Parallel-Execution.md)
- [Framework Extensibility for Custom Test Data Source](RFCs/005-Framework-Extensibility-Custom-DataSource.md)
- [DynamicData Attribute for Data Driven Tests](RFCs/006-DynamicData-Attribute.md)
- [DataSource Attribute Vs ITestDataSource](RFCs/007-DataSource-Attribute-VS-ITestDataSource.md)
- [Test case timeout via runsettings](RFCs/008-TestCase-Timeout.md)

## Releases

You can find all features and bugs fixed in all our releases by looking at [releases.md](releases.md).
