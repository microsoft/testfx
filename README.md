# Microsoft.Testing.Platform and Microsoft Test Framework (MSTest)

[![GitHub release](https://img.shields.io/github/release/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/releases/)
[![GitHub repo size](https://img.shields.io/github/repo-size/microsoft/testfx)](https://github.com/microsoft/testfx)
[![GitHub issues-opened](https://img.shields.io/github/issues/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/issues?q=is%3Aissue+is%3Aopened)
[![GitHub issues-closed](https://img.shields.io/github/issues-closed/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/issues?q=is%3Aissue+is%3Aclosed)
[![GitHub pulls-opened](https://img.shields.io/github/issues-pr/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/pulls?q=is%3Aissue+is%3Aopened)
[![GitHub pulls-merged](https://img.shields.io/github/issues-search/microsoft/testfx?label=merged%20pull%20requests&query=is%3Apr%20is%3Aclosed%20is%3Amerged&color=darkviolet)](https://github.com/microsoft/testfx/pulls?q=is%3Apr+is%3Aclosed+is%3Amerged)
[![GitHub contributors](https://img.shields.io/github/contributors/microsoft/testfx.svg)](https://GitHub.com/microsoft/testfx/graphs/contributors/)
[![Commit Activity](https://img.shields.io/github/commit-activity/m/microsoft/testfx)](.)
[![Build Status](https://dev.azure.com/dnceng-public/public/_apis/build/status/Microsoft/testfx/microsoft.testfx?branchName=main)](https://dev.azure.com/dnceng-public/public/_build/latest?definitionId=209&branchName=main)

This repository is the home for Microsoft.Testing.Platform and extensions, as well as MSTest.

Microsoft.Testing.Platform is a lightweight alternative to VSTest. For more information, see [Microsoft.Testing.Platform overview](https://aka.ms/mtp-overview).

MSTest, Microsoft Testing Framework, is a testing framework for .NET applications. It allows you to write tests, use Test Explorer, create test suites, and apply the red, green, and refactor patterns to write code.

This is a fully supported, open source, and cross-platform test framework with which to write tests targeting .NET Framework, .NET Core, .NET, UWP, and WinUI on Windows, Linux, and Mac.

## How can I contribute?

We welcome any kind of contribution!

- [Contributing](./CONTRIBUTING.md) guides how to best contribute
- [Dev Guide](./docs/dev-guide.md) explains how to build and test
- [Documentation](docs/README.md) contains information about the history, context, and supported or unsupported features. It also gathers the various official documentation pages on learn.microsoft.com about MSTest.

## How to consume MSTest?

MSTest is shipped as NuGet packages that can be added to your projects. The following table lists all available packages.

| Name | Description | Stable version | Preview version | Dogfood version |
| ------------ | ------- | :------------: | :-------------: | :-------------: |
| MSTest | This package is a meta package that simplifies referencing all recommended MSTest packages. | [![#](https://img.shields.io/nuget/v/mstest.svg?style=flat)](http://www.nuget.org/packages/MSTest/) | [![#](https://img.shields.io/nuget/vpre/mstest.svg?style=flat)](http://www.nuget.org/packages/MSTest/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest/versions) |
| MSTest.TestFramework | This package includes the libraries for writing tests with MSTest. To ensure the discovery and execution of your tests, install the `MSTest.TestAdapter` package. | [![#](https://img.shields.io/nuget/v/mstest.testframework.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestFramework/) | [![#](https://img.shields.io/nuget/vpre/mstest.testframework.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestFramework/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.TestFramework/versions) |
| MSTest.TestAdapter | This package includes the adapter logic to discover and run tests. For access to the testing framework, install the `MSTest.TestFramework` package. | [![#](https://img.shields.io/nuget/v/mstest.testadapter.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestAdapter/) | [![#](https://img.shields.io/nuget/vpre/mstest.testadapter.svg?style=flat)](http://www.nuget.org/packages/MSTest.TestAdapter/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.TestAdapter/versions) |
| MSTest.Analyzers | This package includes code analyzers and code fixes for MSTest. | [![#](https://img.shields.io/nuget/v/mstest.analyzers.svg?style=flat)](http://www.nuget.org/packages/MSTest.Analyzers/) | [![#](https://img.shields.io/nuget/vpre/mstest.analyzers.svg?style=flat)](http://www.nuget.org/packages/MSTest.Analyzers/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.Analyzers/versions) |
| MSTest.Sdk | This is a [MSBuild Project SDK](https://learn.microsoft.com/visualstudio/msbuild/how-to-use-project-sdk) package that greatly simplifies a test project structure. See [MSTest SDK Overview](https://aka.ms/mstest/sdk) for details. | [![#](https://img.shields.io/nuget/v/mstest.svg?style=flat)](http://www.nuget.org/packages/MSTest.Sdk/) | [![#](https://img.shields.io/nuget/vpre/mstest.svg?style=flat)](http://www.nuget.org/packages/MSTest.Sdk/) | [Azure Artifacts](https://dnceng.visualstudio.com/public/_artifacts/feed/test-tools/NuGet/MSTest.Sdk/versions) |

## License

MSTest is licensed under the [MIT license](LICENSE).

The LICENSE and ThirdPartyNotices in any downloaded archives are authoritative.
