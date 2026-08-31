# MSTest.Windows.UIAutomation

MSTest.Windows.UIAutomation integrates MSTest lifecycle management with the built-in Windows UI Automation API.

It launches a full-trust desktop application, discovers a window as an `AutomationElement`, and stops the application after each test.

This preview targets unpackaged Win32, WinForms, and WPF applications running in an interactive Windows session. It does not provide packaged MSIX/UWP/WinUI activation, elevated-process automation, a headless desktop, locators, automatic waits, screenshots, or a multi-window object model.

## Getting started

Enable the feature when using `MSTest.Sdk`:

```xml
<Project Sdk="MSTest.Sdk/4.4.0">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
  </PropertyGroup>
</Project>
```

Then derive your test class from `WindowTest`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[STATestClass]
public sealed class MyAppTests : WindowTest
{
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(@"C:\MyApp\MyApp.exe");

    [TestMethod]
    public void MainWindowIsAvailable()
        => Assert.IsNotNull(MainWindow);
}
```

`ApplicationTest` provides process lifecycle management. `WindowTest` additionally waits for a window and exposes it through `MainWindow`. Override `FindWindow` for launcher processes, splash screens, or applications whose desired window is not `Process.MainWindowHandle`, and override `StopApplication` for custom shutdown behavior.

Concrete test classes must declare `[STATestClass]`; MSTest test-class attributes are not inherited.

The package uses the UIA2 `System.Windows.Automation` API. Additional UI automation libraries can be layered on top when richer element interaction is needed.

## Without MSTest.Sdk

Reference `MSTest.Windows.UIAutomation` together with `MSTest.TestFramework`, `MSTest.TestAdapter`, and `Microsoft.NET.Test.Sdk`, then import `Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation`. The package version should match the MSTest framework and adapter versions.
