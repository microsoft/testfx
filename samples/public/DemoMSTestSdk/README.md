# MSTest.Sdk configurations

These projects show how to configure different test scenarios with `MSTest.Sdk`.
The [solution](DemoMSTestSdk.sln) includes the first five projects below; the Windows UI
Automation project is also available in this directory.

| Project | What it demonstrates |
| --- | --- |
| [`ProjectUsingMSTestRunner`](ProjectUsingMSTestRunner) | The default MSTest runner configuration. |
| [`ProjectUsingVSTest`](ProjectUsingVSTest) | Opting into VSTest with `UseVSTest`. |
| [`ProjectWithNativeAOT`](ProjectWithNativeAOT) | Enabling Native AOT publishing with `PublishAot`. |
| [`ProjectUsingPlaywright`](ProjectUsingPlaywright) | Enabling Playwright support with `EnablePlaywright`. |
| [`ProjectUsingAspire`](ProjectUsingAspire) | Enabling Aspire testing with `EnableAspireTesting`. |
| [`ProjectUsingWindowsUIAutomation`](ProjectUsingWindowsUIAutomation) | Enabling Windows UI Automation with `EnableWindowsUIAutomation` (Windows only). |

Each project file also shows the equivalent configuration without `MSTest.Sdk` in a comment.
