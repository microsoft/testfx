# Microsoft Test Framework

[![GitHub release](https://img.shields.io/github/release/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/releases/)
[![GitHub repo size](https://img.shields.io/github/repo-size/microsoft/testfx)](https://github.com/microsoft/testfx)
[![GitHub issues-opened](https://img.shields.io/github/issues/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/issues?q=is%3Aissue+is%3Aopened)
[![GitHub issues-closed](https://img.shields.io/github/issues-closed/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/issues?q=is%3Aissue+is%3Aclosed)
[![GitHub pulls-opened](https://img.shields.io/github/issues-pr/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/pulls?q=is%3Aissue+is%3Aopened)
[![GitHub pulls-merged](https://img.shields.io/github/issues-search/microsoft/testfx?label=merged%20pull%20requests&query=is%3Apr%20is%3Aclosed%20is%3Amerged&color=darkviolet)](https://github.com/microsoft/testfx/pulls?q=is%3Apr+is%3Aclosed+is%3Amerged)
[![GitHub contributors](https://img.shields.io/github/contributors/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/graphs/contributors/)
[![Commit Activity](https://img.shields.io/github/commit-activity/m/microsoft/testfx)](.)
[![Build Status](https://dev.azure.com/dnceng-public/public/_apis/build/status/Microsoft/testfx/microsoft.testfx?branchName=main)](https://dev.azure.com/dnceng-public/public/_build/latest?definitionId=209&branchName=main)

Welcome to the MSTest repository (starting from v2), home of the Microsoft Test Framework and Adapter. MSTest is currently in use in a variety of scenarios including:

- in the relevant in-box unit test project templates (Visual Studio 2017 Preview 4 onwards)
- in the Create Unit Tests wizard (Visual Studio 2017 Preview 4 onwards)
- in the Create IntelliTest wizard (Visual Studio 2017 Preview 4 onwards)

This is a fully supported, open source and cross-platform implementation of the MSTest test framework with which to write tests targeting .NET Framework, .NET Core and ASP.NET Core on Windows, Linux, and Mac.

## Documentation

See [Documentation](docs/README.md) for full documentation about how to contribute, build, test MSTest. This documentation also contains information about history, context and supported or unsupported features.

## NuGet packages

| Name | Description | Stable version | Preview version | Dogfood version |
|--------------|---------|:--------------:|:---------------:|:---------------:|
MSTest | This package is a meta package that simplifies referencing all recommended MSTest packages. | [![#](https://img.shields.io/nuget/v/mstest.svg?style=flat)](http://www.nuget.org/packages/MSTest/) | [![#](https://img.shields.io/nuget/vpre/mstest.svg?style=flat)](http://www.nuget.org/packages/MSTest/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest/versions) |
MSTest.TestFramework | This package includes the libraries for writing tests with MSTest. To ensure discovery and execution of your tests, install the `MSTest.TestAdapter`` package. | [![#](https://img.shields.io/nuget/v/mstest.testframework.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestFramework/) | [![#](https://img.shields.io/nuget/vpre/mstest.testframework.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestFramework/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.TestFramework/versions) |
MSTest.TestAdapter | This package includes the adapter logic to discover and run tests. For access to the testing framework, install the `MSTest.TestFramework` package. | [![#](https://img.shields.io/nuget/v/mstest.testadapter.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestAdapter/) | [![#](https://img.shields.io/nuget/vpre/mstest.testadapter.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestAdapter/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.TestAdapter/versions) |
MSTest.Analyzers | This package includes code analyzers and code fixes for MSTest. | [![#](https://img.shields.io/nuget/v/mstest.analyzers.svg?style=flat)](http://www.nuget.org/packages/MSTest.Analyzers/) | [![#](https://img.shields.io/nuget/vpre/mstest.analyzers.svg?style=flat)](http://www.nuget.org/packages/MSTest.Analyzers/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.Analyzers/versions) |

## License

MSTest is licensed under the [MIT license](LICENSE).

The LICENSE and ThirdPartyNotices in any downloaded archives are authoritative.
