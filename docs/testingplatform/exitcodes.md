# Exit codes

[`int ITestApplication.RunAsync()`](architecture.md) exit codes:

| Exit code | Details |
|-----|----------|
| `0` | The `0` exit code indicates success. All tests that were chosen to run ran to completion and there were no errors. |
| `1` | The `1` exit code indicates unknown errors and acts as a _catch all_. To find additional error information and details, look in the output. |
| `2` | An exit code of `2` is used to indicate that there was at least one test failure. |
| `3` | The exit code `3` indicates that the test session was aborted. A session can be aborted using <kbd>Ctrl</kbd>+<kbd>C</kbd>, as an example. |
| `4` | The exit code `4` indicates that the setup of used extensions is invalid and the tests session cannot run. |
| `5` | The exit code `5` indicates that the command line arguments passed to the test app are invalid. |
| `6` | The exit code `6` indicates that the test session is using a nonimplemented feature. |
| `7` | The exit code `7` indicates that a test session was unable to complete successfully, and likely crashed. It's possible that this was caused by a test session that was run via a test controller's extension point. |
| `8` | The exit code `8` indicates that the test session ran zero tests. |
| `9` | The exit code `9` indicates that the minimum execution policy for the executed tests was violated. |
| `10` | The exit code `10` indicates that the test adapter, Testing.Platform Test Framework, MSTest, NUnit, or xUnit, failed to run tests for an infrastructure reason unrelated to the test's self. An example is failing to create a fixture needed by tests. |
