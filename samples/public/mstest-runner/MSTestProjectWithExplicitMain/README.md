# MSTest runner with an explicit entry point

This project disables generated entry-point creation and uses
[`Program.cs`](MSTestProjectWithExplicitMain/Program.cs) to register MSTest, code coverage,
TRX reporting, and telemetry with the test application builder before running the tests.
