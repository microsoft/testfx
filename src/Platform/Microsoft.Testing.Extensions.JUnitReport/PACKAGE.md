# Microsoft.Testing.Extensions.JUnitReport

Microsoft.Testing.Extensions.JUnitReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that generates a JUnit XML test report at the end of a test session, compatible with Jenkins, GitLab, Azure DevOps, CircleCI and other CI systems.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.JUnitReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.JUnitReport
```

## About

> **⚠️ Experimental:** This extension is currently experimental. The API, CLI options and on-disk format may change in future releases without notice.

This package extends Microsoft.Testing.Platform with:

- **JUnit XML report**: a single `.xml` file in the de facto Jenkins / Surefire-compatible JUnit format
- **CI-friendly**: directly consumable by Jenkins (`junit` step), GitLab (`junit:` artifact reports), Azure DevOps (`PublishTestResults@2` with `testResultsFormat: 'JUnit'`), CircleCI, GitHub Actions test reporters and any other tool that understands the JUnit XML schema
- **Tree-of-tests preserved**: MTP exposes a tree of tests, not a flat list. The JUnit XML schema only defines a flat list of test suites, so the full parent chain of each test is preserved as a `<property name="testpath" value="A/B/C"/>` element inside `<testcase>` for tools that wish to reconstruct the hierarchy

Enable the report via the `--report-junit` command line option. The report file name can be overridden with `--report-junit-filename <name>.xml`.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
