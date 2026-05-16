# Microsoft.Testing.Extensions.HtmlReport

Microsoft.Testing.Extensions.HtmlReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that generates a self-contained HTML test report at the end of a test session.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.HtmlReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.HtmlReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **Self-contained HTML report**: a single `.html` file with all CSS/JS/data inlined; can be archived, shared as a CI artifact, attached to PR comments or e-mailed without any external dependency
- **Triage-friendly UX**: failed tests first, outcome filter, free-text search, sort by name / duration / outcome, expandable per-test detail panel with error message, stack trace, standard output and standard error
- **Light and dark theme** that follows the user's system preference
- **Performance**: pagination keeps the report usable even for very large runs

Enable the report via the `--report-html` command line option. The report file name can be overridden with `--report-html-filename <name>.html`.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
