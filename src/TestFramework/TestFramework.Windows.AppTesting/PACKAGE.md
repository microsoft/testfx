# MSTest.Windows.AppTesting

MSTest.Windows.AppTesting provides base classes for end-to-end testing of Windows desktop applications.

It launches the application under test, exposes its main window through Windows UI Automation, and closes the application after each test. It supports WinForms, WPF, and other Win32 desktop applications.

## Getting started

Enable the feature when using `MSTest.Sdk`:

```xml
<Project Sdk="MSTest.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <EnableWindowsAppTesting>true</EnableWindowsAppTesting>
  </PropertyGroup>
</Project>
```

Then derive your test class from `WindowTest`:

```csharp
using Microsoft.MSTest.Windows.AppTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[STATestClass]
public class MyAppTests : WindowTest
{
    public override string ApplicationPath => @"C:\MyApp\MyApp.exe";

    [TestMethod]
    public void MainWindowIsAvailable()
        => Assert.IsNotNull(MainWindow);
}
```

The package uses the built-in `System.Windows.Automation` API. Additional UI automation libraries can be layered on top when richer element interaction is needed.
