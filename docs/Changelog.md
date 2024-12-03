# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)

## <a name="3.6.4" />[3.6.4] - 2024-12-03

See full log [here](https://github.com/microsoft/testfx/compare/v3.6.3...v3.6.4)

### Fixed

* Bump MS CC to 17.13.0 by @Evangelink in [#4212](https://github.com/microsoft/testfx/pull/4212)
* Bump vulnerable deps by @Evangelink in [#4218)(https://github.com/microsoft/testfx/pull/4218)

### Artifacts

* MSTest: [3.6.4](https://www.nuget.org/packages/MSTest/3.6.4)
* MSTest.TestFramework: [3.6.4](https://www.nuget.org/packages/MSTest.TestFramework/3.6.4)
* MSTest.TestAdapter: [3.6.4](https://www.nuget.org/packages/MSTest.TestAdapter/3.6.4)
* MSTest.Analyzers: [3.6.4](https://www.nuget.org/packages/MSTest.Analyzers/3.6.4)
* MSTest.Sdk: [3.6.4](https://www.nuget.org/packages/MSTest.Sdk/3.6.4)
* Microsoft.Testing.Extensions.CrashDump: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.3)
* Microsoft.Testing.Extensions.HangDump: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.3)
* Microsoft.Testing.Extensions.HotReload: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.3)
* Microsoft.Testing.Extensions.Retry: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.3)
* Microsoft.Testing.Extensions.TrxReport: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.3)

## <a name="3.6.3" />[3.6.3] - 2024-11-12

See full log [here](https://github.com/microsoft/testfx/compare/v3.6.2...v3.6.3)

### Fixed

* Only ship TestAdapter related resources by @nohwnd in [#4013](https://github.com/microsoft/testfx/pull//4013)

### Artifacts

* MSTest: [3.6.3](https://www.nuget.org/packages/MSTest/3.6.3)
* MSTest.TestFramework: [3.6.3](https://www.nuget.org/packages/MSTest.TestFramework/3.6.3)
* MSTest.TestAdapter: [3.6.3](https://www.nuget.org/packages/MSTest.TestAdapter/3.6.3)
* MSTest.Analyzers: [3.6.3](https://www.nuget.org/packages/MSTest.Analyzers/3.6.3)
* MSTest.Sdk: [3.6.3](https://www.nuget.org/packages/MSTest.Sdk/3.6.3)
* Microsoft.Testing.Extensions.CrashDump: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.3)
* Microsoft.Testing.Extensions.HangDump: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.3)
* Microsoft.Testing.Extensions.HotReload: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.3)
* Microsoft.Testing.Extensions.Retry: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.3)
* Microsoft.Testing.Extensions.TrxReport: [1.4.3](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.3)

## <a name="3.6.2" />[3.6.2] - 2024-10-31

See full log [here](https://github.com/microsoft/testfx/compare/v3.6.1...v3.6.2)

### Fixed

* Fix localization of test adapter messages by @nohwnd in [#3949](https://github.com/microsoft/testfx/pull/3949)
* Fix call to EqtTrace by @nohwnd in [#3952](https://github.com/microsoft/testfx/pull/3952)
* Fix concurrency issue with registering callback on TestRunCancellationToken by @Evangelink in [#3958](https://github.com/microsoft/testfx/pull/3958)
* Fix MSTEST0030 to correctly handle all methods by @Evangelink in [#3974](https://github.com/microsoft/testfx/pull/3974)
* Fix displaying inner exceptions by @Evangelink in [#3965](https://github.com/microsoft/testfx/pull/3965)
* Fix MSTEST0018 FP with IEnumerable<SomeType[]> by @Evangelink in [#3978](https://github.com/microsoft/testfx/pull/3978)

### Artifacts

* MSTest: [3.6.2](https://www.nuget.org/packages/MSTest/3.6.2)
* MSTest.TestFramework: [3.6.2](https://www.nuget.org/packages/MSTest.TestFramework/3.6.2)
* MSTest.TestAdapter: [3.6.2](https://www.nuget.org/packages/MSTest.TestAdapter/3.6.2)
* MSTest.Analyzers: [3.6.2](https://www.nuget.org/packages/MSTest.Analyzers/3.6.2)
* MSTest.Sdk: [3.6.2](https://www.nuget.org/packages/MSTest.Sdk/3.6.2)
* Microsoft.Testing.Extensions.CrashDump: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.2)
* Microsoft.Testing.Extensions.HangDump: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.2)
* Microsoft.Testing.Extensions.HotReload: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.2)
* Microsoft.Testing.Extensions.Retry: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.2)
* Microsoft.Testing.Extensions.TrxReport: [1.4.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.2)

## <a name="3.6.1" />[3.6.1] - 2024-10-03

See full log [here](https://github.com/microsoft/testfx/compare/v3.6.0...v3.6.1)

### Fixed

* MSTEST0016: Fix typo in rule message and description by @Evangelink in [#3808](https://github.com/microsoft/testfx/pull/3808)
* MSTEST0018: fix false positive with data member visibility by @Evangelink in [#3866](https://github.com/microsoft/testfx/pull/3866)
* Fix MSTEST0036 is shown for cases where no shadowing happens by @engyebrahim [#3881](https://github.com/microsoft/testfx/pull/3881)
* Bump VSTest to v17.11.1 by @Evangelink
* Fix MSTest hook to be always generated by @Evangelink in [#3889](https://github.com/microsoft/testfx/pull/3889)
* Fix CollectionAssert.AreEqual fails for list of strings using IEqualityComparer following by @engyebrahim in [#3886](https://github.com/microsoft/testfx/pull/3886)
* Fix CollectionAssert.AreEqual for collection of collections ignores even-items by @engyebrahim in [#3893](https://github.com/microsoft/testfx/pull/3893)

### Artifacts

* MSTest: [3.6.1](https://www.nuget.org/packages/MSTest/3.6.1)
* MSTest.TestFramework: [3.6.1](https://www.nuget.org/packages/MSTest.TestFramework/3.6.1)
* MSTest.TestAdapter: [3.6.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.6.1)
* MSTest.Analyzers: [3.6.1](https://www.nuget.org/packages/MSTest.Analyzers/3.6.1)
* MSTest.Sdk: [3.6.1](https://www.nuget.org/packages/MSTest.Sdk/3.6.1)
* Microsoft.Testing.Extensions.CrashDump: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.1)
* Microsoft.Testing.Extensions.HangDump: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.1)
* Microsoft.Testing.Extensions.HotReload: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.1)
* Microsoft.Testing.Extensions.Retry: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.1)
* Microsoft.Testing.Extensions.TrxReport: [1.4.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.1)

## <a name="3.6.1" />[3.6.0] - 2024-09-11

See full log [here](https://github.com/microsoft/testfx/compare/v3.5.2...v3.6.0)

### Added

* Feat: Add code suppressor for CS8618 on TestContext property by @Evangelink in [#3271](https://github.com/microsoft/testfx/pull/3271)
* Feature: add support for injecting TestContext in ctor by @Evangelink in [#3267](https://github.com/microsoft/testfx/pull/3267)
* Feat: Add `[STATestClass]` by @Evangelink in [#3278](https://github.com/microsoft/testfx/pull/3278)
* Feat: Add [STATestMethod] by @Evangelink in [#3286](https://github.com/microsoft/testfx/pull/3286)
* Feat: add support for overloaded parameterized tests by @Evangelink in [#3298](https://github.com/microsoft/testfx/pull/3298)
* Improve display name for DynamicDataAttribute by @Evangelink in [#3293](https://github.com/microsoft/testfx/pull/3293)
* Feat: allow cooperative timeout by @Evangelink in [#3314](https://github.com/microsoft/testfx/pull/3314)
* MSTEST0010: report when class is abstract and inheritance is not set by @engyebrahim in [#3347](https://github.com/microsoft/testfx/pull/3347)
* MSTEST0011: report when class is abstract and inheritance is not specified by @engyebrahim in [#3352](https://github.com/microsoft/testfx/pull/3352)
* MSTEST0010: report if InheritanceBehavior.BeforeEachDerivedClass is set on a sealed class by @engyebrahim in [#3369](https://github.com/microsoft/testfx/pull/3369)
* MSTEST0011: report if InheritanceBehavior.BeforeEachDerivedClass is set on a sealed class by @engyebrahim in [#3370](https://github.com/microsoft/testfx/pull/3370)
* MSTEST0018: DynamicData usage should be valid by @Evangelink in [#3374](https://github.com/microsoft/testfx/pull/3374)
* Add analyzer for DeploymentItem by @engyebrahim in [#3387](https://github.com/microsoft/testfx/pull/3387)
* MSTEST0004: Add code fix by @engyebrahim in [#3482](https://github.com/microsoft/testfx/pull/3482)
* MSTEST0003: Add code fix  by @engyebrahim in [#3493](https://github.com/microsoft/testfx/pull/3493)
* Warn on invalid DisableParallelization configuration entry by @engyebrahim in [#3526](https://github.com/microsoft/testfx/pull/3526)
* MSTEST0007: Add code fix by @engyebrahim in [#3540](https://github.com/microsoft/testfx/pull/3540)
* MSTEST0002: Add code fix by @engyebrahim in [#3554](https://github.com/microsoft/testfx/pull/3554)
* MSTEST0005: Add code fix by @engyebrahim in [#3571](https://github.com/microsoft/testfx/pull/3571)
* MSTest.Sdk handles library mode by @Evangelink in [#3581](https://github.com/microsoft/testfx/pull/3581)
* MSTest.Sdk warn or error on invalid or untested properties by @Evangelink in [#3603](https://github.com/microsoft/testfx/pull/3603)
* MSTEST0036: Add analyzer for when a test member is shadowing another member by @engyebrahim in [#3589](https://github.com/microsoft/testfx/pull/3589)
* Allows to cancel at multiple points before test execution by @Evangelink in [#3723](https://github.com/microsoft/testfx/pull/3723)
* Add option to order tests by name by @Evangelink in [#3796](https://github.com/microsoft/testfx/pull/3796)

### Fixed

* Fix timeout message to reflect that 0 is not allowed by @Evangelink in [#3279](https://github.com/microsoft/testfx/pull/3279)
* Fix TestInitialize and TestCleanup analyzers to allow generic class by @Evangelink in [#3280](https://github.com/microsoft/testfx/pull/3280)
* Fix test case id filtering for server mode by @MarcoRossignoli in [#3284](https://github.com/microsoft/testfx/pull/3284)
* Fix collectionAssert.AreEqual fails for list of lists by @engyebrahim in [#3275](https://github.com/microsoft/testfx/pull/3275)
* MSTEST0034: Use CleanupBehavior.EndOfClass on ClassCleanupAttribute by @engyebrahim in [#3289](https://github.com/microsoft/testfx/pull/3289)
* Fix MSTEST0029 false positive on TestInitialize/TestCleanup by @engyebrahim in [#3318](https://github.com/microsoft/testfx/pull/3318)
* Fix passing null to DataRow when signature is object[] by @Evangelink in [#3331](https://github.com/microsoft/testfx/pull/3331)
* Fix spelling issues by @Evangelink in [#3330](https://github.com/microsoft/testfx/pull/3330)
* Fix: TestCleanup is always called in case of timeout by @Evangelink in [#3334](https://github.com/microsoft/testfx/pull/3334)
* Fix cooperative cancellation for TestInit/TestCleanup by @Evangelink in [#3333](https://github.com/microsoft/testfx/pull/3333)
* Fix message when step is canceled through TestContext by @Evangelink in [#3335](https://github.com/microsoft/testfx/pull/3335)
* Fix: Skip AssemblyInitialize/Cleanup when all tests are skipped by @Evangelink in [#3339](https://github.com/microsoft/testfx/pull/3339)
* Fix XmlDataConnection by @Evangelink in [#3346](https://github.com/microsoft/testfx/pull/3346)
* Fix class initialization/cleanup calls by @Evangelink in [#3362](https://github.com/microsoft/testfx/pull/3362)
* Fix Assert.VerifyThrows to use standard message pattern by @Evangelink in [#3363](https://github.com/microsoft/testfx/pull/3363)
* Refactor TestMethod DisplayName by @Evangelink in [#3365](https://github.com/microsoft/testfx/pull/3365)
* Fix display name for parameterized tests by @Evangelink in [#3366](https://github.com/microsoft/testfx/pull/3366)
* Fix settings parsing ignore invalid values by @engyebrahim in [#3338](https://github.com/microsoft/testfx/pull/3338)
* use GetExceptionMessage instead of ex.Message in TestDataSource by @SimonCropp in [#3415](https://github.com/microsoft/testfx/pull/3415)
* Fix MSTEST0036 to report only on ordinary methods by @Evangelink in [#3711](https://github.com/microsoft/testfx/pull/3711)
* fix: TRLPlusCCancellationTokenSource doesn't cancel current TestRun by @engyebrahim in [#3672](https://github.com/microsoft/testfx/pull/3672)
* Fix running cleanup after first test method  by @nohwnd in [#3764](https://github.com/microsoft/testfx/pull/3764)

### Housekeeping

* Perf: avoid creation of tasks by @Evangelink in [#3282](https://github.com/microsoft/testfx/pull/3282)
* For fixtures in STA thread, use TCS instead of Task.Run by @Evangelink in [#3308](https://github.com/microsoft/testfx/pull/3308)
* Fix typo in initialized by @nohwnd in [#3319](https://github.com/microsoft/testfx/pull/3319)
* Highlight C# code in analyzer tests by @Evangelink in [#3327](https://github.com/microsoft/testfx/pull/3327)
* MSTest.Sdk use Playwright 1.45.1 by @Evangelink in [#3380](https://github.com/microsoft/testfx/pull/3380)
* MSTest.Sdk use aspire 8.1.0 by @Evangelink in [#3379](https://github.com/microsoft/testfx/pull/3379)
* Update description for (init/cleanup) analyzers by @engyebrahim in [#3391](https://github.com/microsoft/testfx/pull/3391)
* use SupportedOSPlatform from polyfill by @SimonCropp in [#3409](https://github.com/microsoft/testfx/pull/3409)
* use Guard from Polyfill  by @SimonCropp in [#3508](https://github.com/microsoft/testfx/pull/3508)
* Save all code files with UTF8BOM by @nohwnd in [#3536](https://github.com/microsoft/testfx/pull/3536)
* Save all project files with UTF8 (NOBOM) by @nohwnd in [#3539](https://github.com/microsoft/testfx/pull/3539)
* Reduce verbosity of internal test framework by @Evangelink in [#3537](https://github.com/microsoft/testfx/pull/3537)

### Artifacts

* MSTest: [3.6.0](https://www.nuget.org/packages/MSTest/3.6.0)
* MSTest.TestFramework: [3.6.0](https://www.nuget.org/packages/MSTest.TestFramework/3.6.0)
* MSTest.TestAdapter: [3.6.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.6.0)
* MSTest.Analyzers: [3.6.0](https://www.nuget.org/packages/MSTest.Analyzers/3.6.0)
* MSTest.Sdk: [3.6.0](https://www.nuget.org/packages/MSTest.Sdk/3.6.0)
* Microsoft.Testing.Extensions.CrashDump: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.4.0)
* Microsoft.Testing.Extensions.HangDump: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.4.0)
* Microsoft.Testing.Extensions.HotReload: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.4.0)
* Microsoft.Testing.Extensions.Retry: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.4.0)
* Microsoft.Testing.Extensions.TrxReport: [1.4.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.4.0)

## <a name="3.5.2" />[3.5.2] - 2024-08-13

See full log [here](https://github.com/microsoft/testfx/compare/v3.5.1...v3.5.2)

### Fixed

* Update dependencies from devdiv/DevDiv/vs-code-coverage by @dotnet-maestro in [#3533](https://github.com/microsoft/testfx/pull/3533)

### Artifacts

* MSTest: [3.5.2](https://www.nuget.org/packages/MSTest/3.5.2)
* MSTest.TestFramework: [3.5.2](https://www.nuget.org/packages/MSTest.TestFramework/3.5.2)
* MSTest.TestAdapter: [3.5.2](https://www.nuget.org/packages/MSTest.TestAdapter/3.5.2)
* MSTest.Analyzers: [3.5.2](https://www.nuget.org/packages/MSTest.Analyzers/3.5.2)
* MSTest.Sdk: [3.5.2](https://www.nuget.org/packages/MSTest.Sdk/3.5.2)
* Microsoft.Testing.Extensions.CrashDump: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.3.2)
* Microsoft.Testing.Extensions.HangDump: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.3.2)
* Microsoft.Testing.Extensions.HotReload: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.3.2)
* Microsoft.Testing.Extensions.Retry: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.3.2)
* Microsoft.Testing.Extensions.TrxReport: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.3.2)

## <a name="3.5.1" />[3.5.1] - 2024-08-05

See full log [here](https://github.com/microsoft/testfx/compare/v3.5.0...v3.5.1)

### Fixed

* Fix XmlDataConnection by @Evangelink in [#3346](https://github.com/microsoft/testfx/pull/3346)
* Fix timeout message to reflect that 0 is not allowed by @Evangelink in [#3279](https://github.com/microsoft/testfx/pull/3279)
* Fix Fix TestInitialize and TestCleanup analyzers to allow generic classes by @Evangelink in [#3280](https://github.com/microsoft/testfx/pull/3280)

### Artifacts

* MSTest: [3.5.1](https://www.nuget.org/packages/MSTest/3.5.1)
* MSTest.TestFramework: [3.5.1](https://www.nuget.org/packages/MSTest.TestFramework/3.5.1)
* MSTest.TestAdapter: [3.5.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.5.1)
* MSTest.Analyzers: [3.5.1](https://www.nuget.org/packages/MSTest.Analyzers/3.5.1)
* MSTest.Sdk: [3.5.1](https://www.nuget.org/packages/MSTest.Sdk/3.5.1)
* Microsoft.Testing.Extensions.CrashDump: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.3.2)
* Microsoft.Testing.Extensions.HangDump: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.3.2)
* Microsoft.Testing.Extensions.HotReload: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.3.2)
* Microsoft.Testing.Extensions.Retry: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.3.2)
* Microsoft.Testing.Extensions.TrxReport: [1.3.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.3.2)

## <a name="3.5.0" />[3.5.0] - 2024-07-15

See full log [here](https://github.com/microsoft/testfx/compare/v3.4.3...v3.5.0)

### Added

* Add overloads to `CollectionAssert.AreEquivalent`/`AreNotEquivalent` with `IEqualityComparer<T>` by @engyebrahim in [#3130](https://github.com/microsoft/testfx/pull/3130)
* Add code fix for MSTEST0017 by @engyebrahim in [#3091](https://github.com/microsoft/testfx/pull/3091)
* Add SDK samples from rel/3.4 by @nohwnd in [#3074](https://github.com/microsoft/testfx/pull/3074)
* Add suppressors for VSTHRD200 by @Evangelink in [#2926](https://github.com/microsoft/testfx/pull/2926)
* Add code fix for TestInitializeShouldBeValidAnalyzer by @Evangelink in [#2890](https://github.com/microsoft/testfx/pull/2890)
* Add code fix for ClassCleanupShouldBeValidAnalyzer by @Evangelink in [#2883](https://github.com/microsoft/testfx/pull/2883)
* Add code fix for AssemblyInitializeShouldBeValidAnalyzer by @Evangelink in [#2882](https://github.com/microsoft/testfx/pull/2882)
* Add code fix for ClassInitializeShouldBeValidAnalyzer by @Evangelink in [#2884](https://github.com/microsoft/testfx/pull/2884)
* Add code fixes for AssemblyCleanupShouldBeValidAnalyzer by @Evangelink in [#2866](https://github.com/microsoft/testfx/pull/2866)
* Add sample of MSTest runner for WinUI by @Evangelink in [#2834](https://github.com/microsoft/testfx/pull/2834)
* MSTEST0032: Always pass assertions by @engyebrahim in [#3238](https://github.com/microsoft/testfx/pull/3238)
* MSTEST0025: add support for Assert.IsNull by @engyebrahim in [#3233](https://github.com/microsoft/testfx/pull/3233)
* Update fixture analyzers to report on missing [TestClass] by @Evangelink in [#3252](https://github.com/microsoft/testfx/pull/3252)
* Customize MSTest runner banner by @Evangelink in [#3235](https://github.com/microsoft/testfx/pull/3235)
* Analyzer: Ensure to use Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute and not System.ComponentModel.DescriptionAttribute by @engyebrahim in [#3202](https://github.com/microsoft/testfx/pull/3202)
* MSTEST0030: Type containing [TestMethod] must be a [TestClass] by @engyebrahim in [#3199](https://github.com/microsoft/testfx/pull/3199)
* leverage DoesNotReturnIfAttribute for better assertion by @SimonCropp in [#3168](https://github.com/microsoft/testfx/pull/3168)
* Assertions: add support for [StringSyntax(CompositeFormat)] by @Evangelink in [#3185](https://github.com/microsoft/testfx/pull/3185)
* Display fixture (Assembly/Class Initialize/Cleanup) methods as "test" entries by @fhnaseer in [#2904](https://github.com/microsoft/testfx/pull/2904)
* Improve display name for string and char by @MichelZ in [#3082](https://github.com/microsoft/testfx/pull/3082)
* Improve display name when DataRow contains arrays by @MichelZ in [#3053](https://github.com/microsoft/testfx/pull/3053)
* TestCaseFilter with custom properties by @engyebrahim in [#3015](https://github.com/microsoft/testfx/pull/3015)
* MSTEST0029: Public methods should be test methods by @engyebrahim in [#3065](https://github.com/microsoft/testfx/pull/3065)
* Avoid conditionals inside assertions analyzer by @fhnaseer in [#2848](https://github.com/microsoft/testfx/pull/2848)
* Localize console service by @nohwnd in [#2900](https://github.com/microsoft/testfx/pull/2900)
* Code fix for TestCleanupShouldBeValidAnalyzer by @Evangelink in [#2887](https://github.com/microsoft/testfx/pull/2887)
* Run and fail tests when ITestDataSource.GetData returns empty IEnumerable by @fhnaseer in [#2865](https://github.com/microsoft/testfx/pull/2865)
* Include timeout duration in timeout message by @engyebrahim in [#2877](https://github.com/microsoft/testfx/pull/2877)
* Flow execution context across fixture methods when using timeout by @Evangelink in [#2843](https://github.com/microsoft/testfx/pull/2843)
* MSTest runner: allow overriding TestRunParameters by @Evangelink in [#3106](https://github.com/microsoft/testfx/pull/3106)

### Housekeeping

* Fix some IDEXXX warnings by @Evangelink in [#2844](https://github.com/microsoft/testfx/pull/2844)
* fix install-windows-sdk path by @SimonCropp in [#2930](https://github.com/microsoft/testfx/pull/2930)
* Fix release years in changelog by @Evangelink in [#3001](https://github.com/microsoft/testfx/pull/3001)
* Fix some diagnostics happening only in VS by @Evangelink in [#3016](https://github.com/microsoft/testfx/pull/3016)
* Add acceptance tests for info with all extensions by @Evangelink in [#3119](https://github.com/microsoft/testfx/pull/3119)
* Refactor STA pool code by @MarcoRossignoli in [#3231](https://github.com/microsoft/testfx/pull/3231)
* Make IProcess disposable by @Evangekunj in [#3120](https://github.com/microsoft/testfx/pull/3120)
* Use GetTypes() instead of DefinedTypes by @SimonCropp in [#3112](https://github.com/microsoft/testfx/pull/3112)
* Use ConcurrentDictionary for attribute cache, and avoid func by @nohwnd in [#3062](https://github.com/microsoft/testfx/pull/3062)
* Lower reflection overhead by @nohwnd in [#2839](https://github.com/microsoft/testfx/pull/2839)
* avoid array alloc in CommandLineParseResult by using IReadOnlyList by @SimonCropp in [#3027](https://github.com/microsoft/testfx/pull/3027)
* remove set for toolname on CommandLineParseResult by @SimonCropp in [#3028](https://github.com/microsoft/testfx/pull/3028)
* remove redundant array alloc in CommandLineOptionsProviderCache by @SimonCropp in [#3026](https://github.com/microsoft/testfx/pull/3026)
* use some StringSyntax Xml by @SimonCropp in [#2974](https://github.com/microsoft/testfx/pull/2974)
* Use polyfill package by @Evangelink in [#3014](https://github.com/microsoft/testfx/pull/3014)
* Use concurrent dictionary for attribute cache by @nohwnd in [#3061](https://github.com/microsoft/testfx/pull/3061)
* MSTest.Sdk: do not use IsImplictlyDefined by @Evangelink in [#2880](https://github.com/microsoft/testfx/pull/2880)

### Fixed

* Fix ResultFiles placement in TRX report by @nohwnd in [#3264](https://github.com/microsoft/testfx/pull/3264)
* Use short date for platform build date on banner by @Evangelink in [#3249](https://github.com/microsoft/testfx/pull/3249)
* Skipped tests count as not run by @Evangelink in [#3243](https://github.com/microsoft/testfx/pull/3243)
* Fix running parallelized tests in STA thread by @eengyebrahim in [#3213](https://github.com/microsoft/testfx/pull/3213)
* Fix `Assert.AreEqual` using null dynamic by @Evangelink in [#3181](https://github.com/microsoft/testfx/pull/3181)
* Fix test run footer color to be red when not successful by @Evangelink in [#3180](https://github.com/microsoft/testfx/pull/3180)
* Fix --info and --help when tools are registered by @Evangelink in [#3123](https://github.com/microsoft/testfx/pull/3123)
* Fix --info when tools are present by @Evangelink in [#3118](https://github.com/microsoft/testfx/pull/3118)
* Fix MSTEST0029 by @engyebrahim in [#3090](https://github.com/microsoft/testfx/pull/3090)
* Fix assembly resolution with DeploymentItem (#3034) by @XXX in [#3036](https://github.com/microsoft/testfx/pull/3036)
* Fix assembly resolution error by @XXX in [#2948](https://github.com/microsoft/testfx/pull/2948)
* Fix property name for enabling aspire in MSTest.Sdk by @XXX in [#2888](https://github.com/microsoft/testfx/pull/2888)
* Fix MSTEST0014 FP with arrays by @XXX in [#2857](https://github.com/microsoft/testfx/pull/2857)
* Workaround the UWP release mode issue with DataRow data by @XXX in [#3240](https://github.com/microsoft/testfx/pull/3240)
* Handle InvalidOperationException when accessing process ID by @XXX in [#3250](https://github.com/microsoft/testfx/pull/3250)
* MSTEST0004: Report only on non-abstract, non-static, classes by @XXX in [#3230](https://github.com/microsoft/testfx/pull/3230)
* Downgrade some log levels from info to debug by @Evangelink in [#3206](https://github.com/microsoft/testfx/pull/3206)
* Retrieve PID inside try/catch by @Evangelink in [#3193](https://github.com/microsoft/testfx/pull/3193)
* Shorten pipe names by @Evangelink in [#3183](https://github.com/microsoft/testfx/pull/3183)
* dispose of IProcess by @SimonCropp in [#3141](https://github.com/microsoft/testfx/pull/3141)
* ClassCleanup are not executed if testclass is ignored by @Evangelink in [#3142](https://github.com/microsoft/testfx/pull/3142)
* Ensure ValueTask test methods are awaited by @Evangelink in [#3137](https://github.com/microsoft/testfx/pull/3137)
* Print exceptions occurring during Task.WaitAll by @Evangelink in [#3128](https://github.com/microsoft/testfx/pull/3128)
* Discriminate MissingMethodException from hot reload by @Evangelink in [#3098](https://github.com/microsoft/testfx/pull/3098)
* Check that Reflect Helper caches inherited and non-inherited attributes separately by @nohwnd in [#3117](https://github.com/microsoft/testfx/pull/3117)
* Fixes displaying arity for many (N instead of int.Max) by @engyebrahim in [#3115](https://github.com/microsoft/testfx/pull/3115)

### New Contributors

* @Mertsch made their first contribution in [#3045](https://github.com/microsoft/testfx/pull/3045)
* @MichelZ made their first contribution in [#3053](https://github.com/microsoft/testfx/pull/3053)

### Artifacts

* MSTest: [3.5.0](https://www.nuget.org/packages/MSTest/3.5.0)
* MSTest.TestFramework: [3.5.0](https://www.nuget.org/packages/MSTest.TestFramework/3.5.0)
* MSTest.TestAdapter: [3.5.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.5.0)
* MSTest.Analyzers: [3.5.0](https://www.nuget.org/packages/MSTest.Analyzers/3.5.0)
* MSTest.Sdk: [3.5.0](https://www.nuget.org/packages/MSTest.Sdk/3.5.0)
* Microsoft.Testing.Extensions.CrashDump: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.3.1)
* Microsoft.Testing.Extensions.HangDump: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.3.1)
* Microsoft.Testing.Extensions.HotReload: [1.3.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.3.0)
* Microsoft.Testing.Extensions.Retry: [1.3.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.3.0)
* Microsoft.Testing.Extensions.TrxReport: [1.3.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.3.1)

## <a name="3.4.3" />[3.4.3] - 2024-05-30

See full log [here](https://github.com/microsoft/testfx/compare/v3.4.2...v3.4.3)

### Fixed

* Revert version of Code Coverage to 17.10.4 by @Evangelink in [#3048](https://github.com/microsoft/testfx/pull/3048)

### Artifacts

* MSTest: [3.4.3](https://www.nuget.org/packages/MSTest/3.4.3)
* MSTest.TestFramework: [3.4.3](https://www.nuget.org/packages/MSTest.TestFramework/3.4.3)
* MSTest.TestAdapter: [3.4.3](https://www.nuget.org/packages/MSTest.TestAdapter/3.4.3)
* MSTest.Analyzers: [3.4.3](https://www.nuget.org/packages/MSTest.Analyzers/3.4.3)
* MSTest.Sdk: [3.4.3](https://www.nuget.org/packages/MSTest.Sdk/3.4.3)
* Microsoft.Testing.Extensions.CrashDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.2.1)
* Microsoft.Testing.Extensions.HangDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.2.1)
* Microsoft.Testing.Extensions.HotReload: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.2.1)
* Microsoft.Testing.Extensions.Retry: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.2.1)
* Microsoft.Testing.Extensions.TrxReport: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.2.1)

## <a name="3.4.2" />[3.4.2] - 2024-05-30

See full log [here](https://github.com/microsoft/testfx/compare/v3.4.1...v3.4.2)

### Fixed

* Use latest released version for Playwright and Aspire by @Evangelink in [#3024](https://github.com/microsoft/testfx/pull/3024)
* Fix project samples for 3.4 by @Evangelink in [#3032](https://github.com/microsoft/testfx/pull/3032)
* Fix assembly resolution with DeploymentItem by @Evangelink in [#3034](https://github.com/microsoft/testfx/pull/3034)

### Artifacts

* MSTest: [3.4.2](https://www.nuget.org/packages/MSTest/3.4.2)
* MSTest.TestFramework: [3.4.2](https://www.nuget.org/packages/MSTest.TestFramework/3.4.2)
* MSTest.TestAdapter: [3.4.2](https://www.nuget.org/packages/MSTest.TestAdapter/3.4.2)
* MSTest.Analyzers: [3.4.2](https://www.nuget.org/packages/MSTest.Analyzers/3.4.2)
* MSTest.Sdk: [3.4.2](https://www.nuget.org/packages/MSTest.Sdk/3.4.2)
* Microsoft.Testing.Extensions.CrashDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.2.1)
* Microsoft.Testing.Extensions.HangDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.2.1)
* Microsoft.Testing.Extensions.HotReload: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.2.1)
* Microsoft.Testing.Extensions.Retry: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.2.1)
* Microsoft.Testing.Extensions.TrxReport: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.2.1)

## <a name="3.4.1" />[3.4.1] - 2024-05-27

See full log [here](https://github.com/microsoft/testfx/compare/v3.4.0...v3.4.1)

### Fixed

* Fix assembly resolution error by @Evangelink in [#2948](https://github.com/microsoft/testfx/pull/2948)

### Artifacts

* MSTest: [3.4.1](https://www.nuget.org/packages/MSTest/3.4.1)
* MSTest.TestFramework: [3.4.1](https://www.nuget.org/packages/MSTest.TestFramework/3.4.1)
* MSTest.TestAdapter: [3.4.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.4.1)
* MSTest.Analyzers: [3.4.1](https://www.nuget.org/packages/MSTest.Analyzers/3.4.1)
* MSTest.Sdk: [3.4.1](https://www.nuget.org/packages/MSTest.Sdk/3.4.1)
* Microsoft.Testing.Extensions.CrashDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.2.1)
* Microsoft.Testing.Extensions.HangDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.2.1)
* Microsoft.Testing.Extensions.HotReload: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.2.1)
* Microsoft.Testing.Extensions.Retry: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.2.1)
* Microsoft.Testing.Extensions.TrxReport: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.2.1)

## <a name="3.4.0" />[3.4.0] - 2024-05-23

See full log [here](https://github.com/microsoft/testfx/compare/v3.3.1...v3.4.0)

### Added

* MSTEST0017: Assertion arguments should be passed in the correct order by @Evangelink in [#2556](https://github.com/microsoft/testfx/pull/2556)
* Support "Central Package Management" with the MSTest.Sdk by @MarcoRossignoli in [#2581](https://github.com/microsoft/testfx/pull/2581)
* MSTEST0019: Prefer TestInitialize over ctor by @Evangelink in [#2580](https://github.com/microsoft/testfx/pull/2580)
* MSTEST0020: Prefer ctors over TestInitialize methods by @Evangelink in [#2582](https://github.com/microsoft/testfx/pull/2582)
* MSTEST0021: Prefer Dispose over TestCleanup methods by @Evangelink in [#2585](https://github.com/microsoft/testfx/pull/2585)
* MSTEST0022: Prefer 'TestCleanup' methods over Dispose by @Evangelink in [#2586](https://github.com/microsoft/testfx/pull/2586)
* MSTEST0023: Do not negate boolean assertions by @Evangelink in [#2594](https://github.com/microsoft/testfx/pull/2594)
* MSTEST0024: Do not store TestContext in static members by @Evangelink in [#2597](https://github.com/microsoft/testfx/pull/2597)
* Use System.Text.Json instead of Jsonite y @mariam-abdulla in [#2564](https://github.com/microsoft/testfx/pull/2564)
* Update MSTest.Sdk to handle playwright by @Evangelink in [#2598](https://github.com/microsoft/testfx/pull/2598)
* Add support for runner in WinUI mode by @Evangelink in [#2617](https://github.com/microsoft/testfx/pull/2617)
* Init and cleanup timeout by @engyebrahim in [#2570](https://github.com/microsoft/testfx/pull/2570)
* Add project samples for MSTest.Sdk by @Evangelink in [#2675](https://github.com/microsoft/testfx/pull/2675)
* Cache CommandLineOption from the providers by @MarcoRossignoli in [#2680](https://github.com/microsoft/testfx/pull/2680)
* Simplify Microsoft.Testing.Platform banner by @Evangelink in [#2686](https://github.com/microsoft/testfx/pull/2686)
* Add support for STA thread by @Evangelink in [#2682](https://github.com/microsoft/testfx/pull/2682)
* Improve error message when dynamic data source layout is invalid by @Evangelink in [#2690](https://github.com/microsoft/testfx/pull/2690)
* Add global using feature in MSTest.Sdk by @Varorbc in [#2701](https://github.com/microsoft/testfx/pull/2701)
* Added AssertInstanceOf overloads with out parameter by @Mrxx99 in [#2717](https://github.com/microsoft/testfx/pull/2717)
* Improve error message for mismatched data driven test by @Evangelink in [#2691](https://github.com/microsoft/testfx/pull/2691)
* SDK: add playwright default using by @Evangelink in [#2730](https://github.com/microsoft/testfx/pull/2730)
* SDK: add global usings to VSTest targets by @Evangelink in [#2731](https://github.com/microsoft/testfx/pull/2731)
* MSTest.Sdk: add support for Aspire by @Evangelink in [#2758](https://github.com/microsoft/testfx/pull/2758)
* Ensure that fixtures also support sta threading by @Evangelink in [#2769](https://github.com/microsoft/testfx/pull/2769)
* Add option to consider empty data of DynamicData as inconclusive by @engyebrahim in [#2771](https://github.com/microsoft/testfx/pull/2771)
* Add PreferAssertFailOverAlwaysFalseConditionsAnalyzer by @Youssef1313 in [#2799](https://github.com/microsoft/testfx/pull/2799)

### Fixed

* Get real exception in case of error in AssemblyInitialize by @nohwnd in [#2571](https://github.com/microsoft/testfx/pull/2571)
* Fix MSTEST0014 to handle optional parameters by @Evangelink in [#2574](https://github.com/microsoft/testfx/pull/2574)
* Get real exception for AssemblyCleanup/ClassInitialize/ClassCleanup by @Evangelink in [#2576](https://github.com/microsoft/testfx/pull/2576)
* Fix MSTEST0014 problems with arrays by @Evangelink in [#2607](https://github.com/microsoft/testfx/pull/2607)
* Fix MSTest version under testing platform by @Evangelink in [#2629](https://github.com/microsoft/testfx/pull/2629)
* Fix MSTEST0005 to report only inside test classes by @Evangelink in [#2641](https://github.com/microsoft/testfx/pull/2641)
* Fix TestHostControllersTestHost startup by @MarcoRossignoli in [#2659](https://github.com/microsoft/testfx/pull/2659)
* Fix ITestHostEnvironmentVariableProvider tests by @MarcoRossignoli in [#2660](https://github.com/microsoft/testfx/pull/2660)
* Simplify added logic for AssemblyResolution in netcore by @Evangelink in [#2654](https://github.com/microsoft/testfx/pull/2654)
* add Tests for --list-tests does not work with --filter by @engyebrahim in [#2699](https://github.com/microsoft/testfx/pull/2699)
* Fix false positive in TestClassShouldHaveTestMethodAnalyzer (MSTEST0016) for derived class by @engyebrahim in [#2715](https://github.com/microsoft/testfx/pull/2715)
* Fix ThreadOperations to handle TaskCanceledException by @Evangelink in [#2722](https://github.com/microsoft/testfx/pull/2722)
* Update PackageReferences to be properly defined by @dansiegel in [#2727](https://github.com/microsoft/testfx/pull/2727)
* Tests for --info shows incorrect version for MSTest by @engyebrahim in [#2745](https://github.com/microsoft/testfx/pull/2745)
* Fix typo by @thomhurst in [#2749](https://github.com/microsoft/testfx/pull/2749)
* Fix localappdata folder for Linux and Mac by @Evangelink in [#2765](https://github.com/microsoft/testfx/pull/2765)
* Fix deserializers for DiscoveryRequestArgs and RunRequestArgs by @mariam-abdulla in [#2768](https://github.com/microsoft/testfx/pull/2768)
* Don't start thread/task when not using timeout for fixture methods by @Evangelink in [#2825](https://github.com/microsoft/testfx/pull/2825)
* Fix parameters/arguments check for data driven tests by @nohwnd in [#2829](https://github.com/microsoft/testfx/pull/2829)
* Cleaning command line validations and adding unit tests by @fhnaseer in [#2847](https://github.com/microsoft/testfx/pull/2847)
* Fix MSTEST0014 FP with arrays by @Evangelink in [#2857](https://github.com/microsoft/testfx/pull/2857)
* Flow execution context across fixture methods when using timeout by @Evangelink [#2843](https://github.com/microsoft/testfx/pull/2843)

### Housekeeping

* Use dotnet-public instead of nuget feed for samples by @Evangelink in [#2623](https://github.com/microsoft/testfx/pull/2623)
* Update NativeAOT runner example by @nohwnd in [#2634](https://github.com/microsoft/testfx/pull/2634)
* Use Platform.MSBuild to setup nativeAot example by @nohwnd in [#2645](https://github.com/microsoft/testfx/pull/2645)
* Remove IsPackable from the DemoMSTestSdk by @Varorbc in [#2696](https://github.com/microsoft/testfx/pull/2696)
* allow rollForward of sdk to latestFeature by @SimonCropp in [#2714](https://github.com/microsoft/testfx/pull/2714)
* Onboard Central Package Management by @Evangelink in [#2728](https://github.com/microsoft/testfx/pull/2728)
* Add dependabot by @Evangelink in [#2737](https://github.com/microsoft/testfx/pull/2737)
* perf: reduce allocations and calls to ToArray by @Evangelink in [#2747](https://github.com/microsoft/testfx/pull/2747)
* Add test for resource recursion problem by @nohwnd in [#2778](https://github.com/microsoft/testfx/pull/2778)
* Opt-out from CPM in samples by @Evangelink in [#2805](https://github.com/microsoft/testfx/pull/2805)

### New Contributors

* @mariam-abdulla made their first contribution in [#2564](https://github.com/microsoft/testfx/pull/2564)
* @Varorbc made their first contribution in [#2696](https://github.com/microsoft/testfx/pull/2696)
* @skanda890 made their first contribution in [#2706](https://github.com/microsoft/testfx/pull/2706)
* @SimonCropp made their first contribution in [#2714](https://github.com/microsoft/testfx/pull/2714)
* @Mrxx99 made their first contribution in [#2717](https://github.com/microsoft/testfx/pull/2717)
* @dansiegel made their first contribution in [#2727](https://github.com/microsoft/testfx/pull/2727)
* @thomhurst made their first contribution in [#2749](https://github.com/microsoft/testfx/pull/2749)
* @Youssef1313 made their first contribution in [#2799](https://github.com/microsoft/testfx/pull/2799)

### Artifacts

* MSTest: [3.4.0](https://www.nuget.org/packages/MSTest/3.4.0)
* MSTest.TestFramework: [3.4.0](https://www.nuget.org/packages/MSTest.TestFramework/3.4.0)
* MSTest.TestAdapter: [3.4.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.4.0)
* MSTest.Analyzers: [3.4.0](https://www.nuget.org/packages/MSTest.Analyzers/3.4.0)
* MSTest.Sdk: [3.4.0](https://www.nuget.org/packages/MSTest.Sdk/3.4.0)
* Microsoft.Testing.Extensions.CrashDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.2.1)
* Microsoft.Testing.Extensions.HangDump: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.2.1)
* Microsoft.Testing.Extensions.HotReload: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.2.1)
* Microsoft.Testing.Extensions.Retry: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.2.1)
* Microsoft.Testing.Extensions.TrxReport: [1.2.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.2.1)

## <a name="3.3.1" />[3.3.1] - 2024-04-04

See full log [here](https://github.com/microsoft/testfx/compare/v3.3.0...v3.3.1)

### Fixed

* Fix `MSTest.SDK` deps by @MarcoRossignoli in [#2650](https://github.com/microsoft/testfx/pull/2650)

### Artifacts

* MSTest: [3.3.1](https://www.nuget.org/packages/MSTest/3.3.1)
* MSTest.TestFramework: [3.3.1](https://www.nuget.org/packages/MSTest.TestFramework/3.3.1)
* MSTest.TestAdapter: [3.3.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.3.1)
* MSTest.Analyzers: [3.3.1](https://www.nuget.org/packages/MSTest.Analyzers/3.3.1)
* MSTest.Sdk: [3.3.1](https://www.nuget.org/packages/MSTest.Sdk/3.3.1)
* Microsoft.Testing.Extensions.CrashDump: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.1.0)
* Microsoft.Testing.Extensions.HangDump: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.1.0)
* Microsoft.Testing.Extensions.HotReload: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.1.0)
* Microsoft.Testing.Extensions.Retry: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.1.0)
* Microsoft.Testing.Extensions.TrxReport: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.1.0)

## <a name="3.3.0" />[3.3.0] - 2024-04-03

See full log [here](https://github.com/microsoft/testfx/compare/v3.2.2...v3.3.0)

### Added

* MSTEST0007: [<\Test Attributes>] can only be set on methods marked with [TestMethod] by @cvpoienaru in [#2221](https://github.com/microsoft/testfx/pull/2221)
* MSTEST0008: `[TestInitialize]` should be valid by @engyebrahim in [#2292](https://github.com/microsoft/testfx/pull/2292)
* MSTEST0009: `[TestCleanup]` should have valid layout by @cvpoienaru in [#2312](https://github.com/microsoft/testfx/pull/2312)
* MSTEST0010: `[ClassInitialize]` should be valid by @engyebrahim in [#2354](https://github.com/microsoft/testfx/pull/2354)
* MSTEST0011: `[ClassCleanup]` should be valid  by @engyebrahim in [#2356](https://github.com/microsoft/testfx/pull/2356)
* MSTEST0012: `[AssemblyInitialize]` should be valid by @engyebrahim in [#2328](https://github.com/microsoft/testfx/pull/2328)
* MSTEST0013: `[AssemblyCleanup]` should be valid by @engyebrahim in [#2353](https://github.com/microsoft/testfx/pull/2353)
* MSTEST0014: `[DataRow]` should be valid by @cvpoienaru in [#2352](https://github.com/microsoft/testfx/pull/2352)
* MSTEST0015: Test method should not be ignored by @engyebrahim in [#2429](https://github.com/microsoft/testfx/pull/2429)
* MSTEST0016: Test class should have test method by @engyebrahim in [#2518](https://github.com/microsoft/testfx/pull/2518)
* Add support for ValueTask return type by @Evangelink in [#2208](https://github.com/microsoft/testfx/pull/2208)
* Allow IEnumerable of tuple for DynamicData source by @Evangelink in [#2226](https://github.com/microsoft/testfx/pull/2226)
* Add a console logger sample for the runner by @Evangelink in [#2246](https://github.com/microsoft/testfx/pull/2246)
* Create preview.md by @nohwnd in [#2268](https://github.com/microsoft/testfx/pull/2268)
* Use Task.Run for Assembly/Class init to allow exiting by @Evangelink in [#2265](https://github.com/microsoft/testfx/pull/2265)
* Document logging public APIs by @Evangelink in [#2290](https://github.com/microsoft/testfx/pull/2290)
* Document test framework public APIs by @Evangelink in [#2286](https://github.com/microsoft/testfx/pull/2286)
* Document messages public APIs by @Evangelink in [#2287](https://github.com/microsoft/testfx/pull/2287)
* Document command line public APIs by @Evangelink in [#2285](https://github.com/microsoft/testfx/pull/2285)
* Document requests public APIs by @Evangelink in [#2289](https://github.com/microsoft/testfx/pull/2289)
* Document builder public API by @Evangelink in [#2283](https://github.com/microsoft/testfx/pull/2283)
* Document configuration public APIs by @Evangelink in [#2291](https://github.com/microsoft/testfx/pull/2291)
* Document testhost related public APIs by @Evangelink in [#2284](https://github.com/microsoft/testfx/pull/2284)
* Document extensions/services public APIs by @Evangelink in [#2288](https://github.com/microsoft/testfx/pull/2288)
* Document messages public api by @MarcoRossignoli in [#2357](https://github.com/microsoft/testfx/pull/2357)
* Complete public api docs by @MarcoRossignoli in [#2359](https://github.com/microsoft/testfx/pull/2359)
* Assert.AreEqual allow IEquatable<\T> for actual and expected by @Evangelink in [#2381](https://github.com/microsoft/testfx/pull/2381)
* Introduce `--exit-on-process-exit` by @MarcoRossignoli in [#2434](https://github.com/microsoft/testfx/pull/2434)
* Add example for simple framework without VSTest bridge by @nohwnd in [#2446](https://github.com/microsoft/testfx/pull/2446)
* Add graph query filtering doc draft by @MarcoRossignoli in [#2460](https://github.com/microsoft/testfx/pull/2460)

### Fixed

* MSTEST0002: fix false-positive with static TestClass by @Evangelink in [#2182](https://github.com/microsoft/testfx/pull/2182)
* Fix code samples by @Evangelink in [#2217](https://github.com/microsoft/testfx/pull/2217)
* Fix setup of MSBuild Reference item entry by @Evangelink in [#2220](https://github.com/microsoft/testfx/pull/2220)
* Fix support of WinUI for net6+ by @Evangelink in [#2222](https://github.com/microsoft/testfx/pull/2222)
* Fix MSTEST0002 - Generic TestClass is valid by @Evangelink in [#2428](https://github.com/microsoft/testfx/pull/2428)
* Fix MSTEST0005 to report only inside test classes by @Evangelink in [#2642](https://github.com/microsoft/testfx/pull/2642)
* Preserve real exception and fix tests by @MarcoRossignoli in [#2272](https://github.com/microsoft/testfx/pull/2272)
* Shorten server pipe name by @MarcoRossignoli in [#2302](https://github.com/microsoft/testfx/pull/2302)
* Fix `TestMethod` should be valid analyzer description by @engyebrahim in [#2295](https://github.com/microsoft/testfx/pull/2295)
* Fix command line banner output by @MarcoRossignoli in [#2314](https://github.com/microsoft/testfx/pull/2314)
* Workaround harmless MSBuild warning in VS by @Evangelink in [#2349](https://github.com/microsoft/testfx/pull/2349)
* Fix analyzers doc link by @Evangelink in [#2361](https://github.com/microsoft/testfx/pull/2361)
* Remove the `TestingPlatformServer` if the runner is disabled by @MarcoRossignoli in [#2409](https://github.com/microsoft/testfx/pull/2409)
* Honor request.Complete() by @MarcoRossignoli in [#2448](https://github.com/microsoft/testfx/pull/2448)
* Make `RootDeploymentDirectory` name unique by @MarcoRossignoli in [#2456](https://github.com/microsoft/testfx/pull/2456)

### Housekeeping

* Reduce calls to GetTypeInfo() by @Evangelink in [#2426](https://github.com/microsoft/testfx/pull/2426)
* Improve warning message for generic non-abstract test classes by @Evangelink in [#2427](https://github.com/microsoft/testfx/pull/2427)
* Remove the `Microsoft.Testing.Extensions.Telemetry` from the package by @MarcoRossignoli in [#2454](https://github.com/microsoft/testfx/pull/2454)

### Artifacts

* MSTest: [3.3.0](https://www.nuget.org/packages/MSTest/3.3.0)
* MSTest.TestFramework: [3.3.0](https://www.nuget.org/packages/MSTest.TestFramework/3.3.0)
* MSTest.TestAdapter: [3.3.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.3.0)
* MSTest.Analyzers: [3.3.0](https://www.nuget.org/packages/MSTest.Analyzers/3.3.0)
* Microsoft.Testing.Extensions.CrashDump: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.1.0)
* Microsoft.Testing.Extensions.HangDump: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.1.0)
* Microsoft.Testing.Extensions.HotReload: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.1.0)
* Microsoft.Testing.Extensions.Retry: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.1.0)
* Microsoft.Testing.Extensions.TrxReport: [1.1.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.1.0)

## <a name="3.2.2" />[3.2.2] - 2024-02-22

See full log [here](https://github.com/microsoft/testfx/compare/v3.2.1...v3.2.2)

### Fixed

* [rel/3.2] Workaround harmless MSBuild warning in VS by @Evangelink in [#2350](https://github.com/microsoft/testfx/pull/2350)
* [rel/3.2] Fix analyzers doc link by @Evangelink in [#2362](https://github.com/microsoft/testfx/pull/2362)
* Assert.AreEqual allow IEquatable<T\> for actual and expected by @Evangelink in [#2382](https://github.com/microsoft/testfx/pull/2382)
* Fix msbuild integration (#2389) by @MarcoRossignoli in [#2395](https://github.com/microsoft/testfx/pull/2395)
* Remove the `TestingPlatformServer` if the runner is disabled (#2409) by @MarcoRossignoli in [#2410](https://github.com/microsoft/testfx/pull/2410)

### Artifacts

* MSTest: [3.2.2](https://www.nuget.org/packages/MSTest/3.2.2)
* MSTest.TestFramework: [3.2.2](https://www.nuget.org/packages/MSTest.TestFramework/3.2.2)
* MSTest.TestAdapter: [3.2.2](https://www.nuget.org/packages/MSTest.TestAdapter/3.2.2)
* MSTest.Analyzers: [3.2.2](https://www.nuget.org/packages/MSTest.Analyzers/3.2.2)
* Microsoft.Testing.Extensions.CrashDump: [1.0.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.0.2)
* Microsoft.Testing.Extensions.HangDump: [1.0.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.0.2)
* Microsoft.Testing.Extensions.HotReload: [1.0.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.0.2)
* Microsoft.Testing.Extensions.Retry: [1.0.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.0.2)
* Microsoft.Testing.Extensions.TrxReport: [1.0.2](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.0.2)

## <a name="3.2.1" />[3.2.1] - 2024-02-13

See full log [here](https://github.com/microsoft/testfx/compare/v3.2.0...v.3.2.1)

### Fixed

* MSTEST0002: fix false-positive with static TestClass (#2182) by @Evangelink in [#2199](https://github.com/microsoft/testfx/pull/2199)
* Bump version of coverage and platform by @Evangelink in [#2280](https://github.com/microsoft/testfx/pull/2280)
* [rel/3.2] Update dependencies from devdiv/DevDiv/vs-code-coverage by @dotnet-maestro in [#2315](https://github.com/microsoft/testfx/pull/2315)
* Fix command line output validation (#2314) by @MarcoRossignoli in [#2317](https://github.com/microsoft/testfx/pull/2317)
* [rel/3.2] Update dependencies from microsoft/testanywhere by @dotnet-maestro in [#2320](https://github.com/microsoft/testfx/pull/2320)
* [rel/3.2] Update dependencies from microsoft/testanywhere by @dotnet-maestro in [#2326](https://github.com/microsoft/testfx/pull/2326)

### Housekeeping

* Remove unused localization entries by @Evangelink in [#2192](https://github.com/microsoft/testfx/pull/2192)
* Remove version fixup workaround (#2209) by @Evangelink in [#2212](https://github.com/microsoft/testfx/pull/2212)

### Artifacts

* MSTest: [3.2.1](https://www.nuget.org/packages/MSTest/3.2.1)
* MSTest.TestFramework: [3.2.1](https://www.nuget.org/packages/MSTest.TestFramework/3.2.1)
* MSTest.TestAdapter: [3.2.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.2.1)
* MSTest.Analyzers: [3.2.1](https://www.nuget.org/packages/MSTest.Analyzers/3.2.1)
* Microsoft.Testing.Extensions.CrashDump: [1.0.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.0.1)
* Microsoft.Testing.Extensions.HangDump: [1.0.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.0.1)
* Microsoft.Testing.Extensions.HotReload: [1.0.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.0.1)
* Microsoft.Testing.Extensions.Retry: [1.0.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.0.1)
* Microsoft.Testing.Extensions.TrxReport: [1.0.1](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.0.1)

## <a name="3.2.0" />[3.2.0] - 2024-01-24

See full log [here](https://github.com/microsoft/testfx/compare/v3.1.1...v.3.2.0)

### Added

* MSTest runner by @Evangelink in [#1775](https://github.com/microsoft/testfx/pull/1775)
* Add analyzers by @Evangelink in [#1870](https://github.com/microsoft/testfx/pull/1870)
* Add more analyzers by @Evangelink in [#1974](https://github.com/microsoft/testfx/pull/1974)
* Rework MSTEST0002 and MSTEST0003 by @Evangelink in [#1985](https://github.com/microsoft/testfx/pull/1985)
* Update description of MSTEST0001 by @Evangelink in [#1988](https://github.com/microsoft/testfx/pull/1988)
* MSTEST0004: Public types should be test classes by @Evangelink in [#1989](https://github.com/microsoft/testfx/pull/1989)
* Add readme to MSTest.Analyzer package by @Evangelink in [#2006](https://github.com/microsoft/testfx/pull/2006)
* Add `--ignore-exit-code` and `TESTINGPLATFORM_EXITCODE_IGNORE` by @MarcoRossignoli in [#2057](https://github.com/microsoft/testfx/pull/2057)
* Add samples of runner vs dotnet test by @Evangelink in [#2063](https://github.com/microsoft/testfx/pull/2063)
* Add mstest runner samples by @nohwnd in [#2068](https://github.com/microsoft/testfx/pull/2068)
* Add comparison stats by @MarcoRossignoli in [#2069](https://github.com/microsoft/testfx/pull/2069)
* Performance measurements by @jakubch1 in [#2071](https://github.com/microsoft/testfx/pull/2071)
* Add protocol documentation by @drognanar in [#2073](https://github.com/microsoft/testfx/pull/2073)
* Cache platform command line options by @MarcoRossignoli in [#2127](https://github.com/microsoft/testfx/pull/2127)

### Fixed

* Don't stop by @nohwnd in [#1737](https://github.com/microsoft/testfx/pull/1737)
* Fix DataRowAttribute to be cls compliant by @Evangelink in [#1878](https://github.com/microsoft/testfx/pull/1878)
* Workaround issue with managed type name utility by @Evangelink in [#1876](https://github.com/microsoft/testfx/pull/1876)
* Fix path normalization by @Evangelink in [#1880](https://github.com/microsoft/testfx/pull/1880)
* MSTEST0005: TestContext should be valid by @Evangelink in [#2019](https://github.com/microsoft/testfx/pull/2019)
* MSTEST0006: Avoid '[ExpectedException]' by @Evangelink in [#2025](https://github.com/microsoft/testfx/pull/2025)
* Hide MissingMethodException when in hot reload mode by @Evangelink in [#2028](https://github.com/microsoft/testfx/pull/2028)
* Fix running tests with UWP by @Evangelink in [#2047](https://github.com/microsoft/testfx/pull/2047)
* Fix rules help link URL by @Evangelink in [#2048](https://github.com/microsoft/testfx/pull/2048)
* Fix short link to telemetry doc by @Evangelink in [#2064](https://github.com/microsoft/testfx/pull/2064)
* Avoid some allocations by @MarcoRossignoli in [#2079](https://github.com/microsoft/testfx/pull/2079)
* MSTEST0001: Set default severity to Info by @Evangelink in [#2077](https://github.com/microsoft/testfx/pull/2077)
* Remove `IEnumerable` extensions by @MarcoRossignoli in [#2090](https://github.com/microsoft/testfx/pull/2090)
* Simplify substring by @Evangelink in [#2092](https://github.com/microsoft/testfx/pull/2092)
* Fix mstest runner namespace by @Evangelink in [#2078](https://github.com/microsoft/testfx/pull/2078)
* Cleanup `ICommandLineOptionsProvider` api by @MarcoRossignoli in [#2093](https://github.com/microsoft/testfx/pull/2093)
* Update ToHumanReadableDuration by @MarcoRossignoli in [#2094](https://github.com/microsoft/testfx/pull/2094)
* [bug] Do not wait timeout duration in case of user cancellation by @Evangelink in [#2104](https://github.com/microsoft/testfx/pull/2104)
* TrimStackTrace should handle empty stacktrace by @Evangelink in [#2113](https://github.com/microsoft/testfx/pull/2113)
* Update Public API by @Evangelink in [#2116](https://github.com/microsoft/testfx/pull/2116)
* Fix issue #2121 DataTestMethodAttribute is missing a constructor by @HannoZ in [#2125](https://github.com/microsoft/testfx/pull/2125)

### Chores

* Bump and cleanup global versions by @Evangelink in [#1754](https://github.com/microsoft/testfx/pull/1754)
* Eng and global.json housekeeping by @Evangelink in [#1756](https://github.com/microsoft/testfx/pull/1756)
* Code clean-up  by @ViktorHofer in [#1757](https://github.com/microsoft/testfx/pull/1757)
* Bump StyleCop analyzers version by @Evangelink in [#1761](https://github.com/microsoft/testfx/pull/1761)
* Bump version of test dependencies by @Evangelink in [#1762](https://github.com/microsoft/testfx/pull/1762)
* Bump VSTest deps to 17.7.2 by @Evangelink in [#1769](https://github.com/microsoft/testfx/pull/1769)
* Pin Moq to 4.18.4 for security by @Evangelink in [#1770](https://github.com/microsoft/testfx/pull/1770)
* Clean nuget.config by @Evangelink in [#1772](https://github.com/microsoft/testfx/pull/1772)
* Fix warnings and cleanup infra by @Evangelink in [#1773](https://github.com/microsoft/testfx/pull/1773)
* Drop ruleset in favor of editorconfig by @Evangelink in [#1780](https://github.com/microsoft/testfx/pull/1780)
* Enable CA1001 by @Evangelink in [#1811](https://github.com/microsoft/testfx/pull/1811)
* Set analysis level to latest-recommended by @Evangelink in [#1816](https://github.com/microsoft/testfx/pull/1816)
* Use modern styles and rules by @Evangelink in [#1852](https://github.com/microsoft/testfx/pull/1852)
* Add third-parties licenses by @Evangelink in [#1955](https://github.com/microsoft/testfx/pull/1955)

### New Contributors

* @ViktorHofer made their first contribution in [#1757](https://github.com/microsoft/testfx/pull/1757)
* @jakubch1 made their first contribution in [#2071](https://github.com/microsoft/testfx/pull/2071)
* @drognanar made their first contribution in [#2073](https://github.com/microsoft/testfx/pull/2073)
* @HannoZ made their first contribution in [#2125](https://github.com/microsoft/testfx/pull/2125)

### Artifacts

* MSTest: [3.2.0](https://www.nuget.org/packages/MSTest/3.2.0)
* MSTest.TestFramework: [3.2.0](https://www.nuget.org/packages/MSTest.TestFramework/3.2.0)
* MSTest.TestAdapter: [3.2.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.2.0)
* MSTest.Analyzers: [3.2.0](https://www.nuget.org/packages/MSTest.Analyzers/3.2.0)
* Microsoft.Testing.Extensions.CrashDump: [1.0.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.0.0)
* Microsoft.Testing.Extensions.HangDump: [1.0.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.0.0)
* Microsoft.Testing.Extensions.HotReload: [1.0.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.0.0)
* Microsoft.Testing.Extensions.Retry: [1.0.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.0.0)
* Microsoft.Testing.Extensions.TrxReport: [1.0.0](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.0.0)

## <a name="3.2.0-preview.24069.3" />[3.2.0-preview.24069.3] - 2024-01-19

See full log [here](https://github.com/microsoft/testfx/compare/v3.2.0-preview.23623.1...v3.2.0-preview.24069.3)

### Added

* Add readme to MSTest.Analyzer package by @Evangelink in [#2006](https://github.com/microsoft/testfx/pull/2006)
* Add `--ignore-exit-code` and `TESTINGPLATFORM_EXITCODE_IGNORE` by @MarcoRossignoli in [#2057](https://github.com/microsoft/testfx/pull/2057)
* Add samples of runner vs dotnet test by @Evangelink in [#2063](https://github.com/microsoft/testfx/pull/2063)
* Add mstest runner samples by @nohwnd in [#2068](https://github.com/microsoft/testfx/pull/2068)
* Add comparison stats by @MarcoRossignoli in [#2069](https://github.com/microsoft/testfx/pull/2069)
* Performance measurements by @jakubch1 in [#2071](https://github.com/microsoft/testfx/pull/2071)
* Add protocol documentation by @drognanar in [#2073](https://github.com/microsoft/testfx/pull/2073)
* Cache platform command line options by @MarcoRossignoli in [#2127](https://github.com/microsoft/testfx/pull/2127)

### Fixed

* MSTEST0005: TestContext should be valid by @Evangelink in [#2019](https://github.com/microsoft/testfx/pull/2019)
* MSTEST0006: Avoid '[ExpectedException]' by @Evangelink in [#2025](https://github.com/microsoft/testfx/pull/2025)
* Hide MissingMethodException when in hot reload mode by @Evangelink in [#2028](https://github.com/microsoft/testfx/pull/2028)
* Fix running tests with UWP by @Evangelink in [#2047](https://github.com/microsoft/testfx/pull/2047)
* Fix rules help link URL by @Evangelink in [#2048](https://github.com/microsoft/testfx/pull/2048)
* Fix short link to telemetry doc by @Evangelink in [#2064](https://github.com/microsoft/testfx/pull/2064)
* Avoid some allocations by @MarcoRossignoli in [#2079](https://github.com/microsoft/testfx/pull/2079)
* MSTEST0001: Set default severity to Info by @Evangelink in [#2077](https://github.com/microsoft/testfx/pull/2077)
* Remove `IEnumerable` extensions by @MarcoRossignoli in [#2090](https://github.com/microsoft/testfx/pull/2090)
* Simplify substring by @Evangelink in [#2092](https://github.com/microsoft/testfx/pull/2092)
* Fix mstest runner namespace by @Evangelink in [#2078](https://github.com/microsoft/testfx/pull/2078)
* Cleanup `ICommandLineOptionsProvider` api by @MarcoRossignoli in [#2093](https://github.com/microsoft/testfx/pull/2093)
* Update ToHumanReadableDuration by @MarcoRossignoli in [#2094](https://github.com/microsoft/testfx/pull/2094)
* [bug] Do not wait timeout duration in case of user cancellation by @Evangelink in [#2104](https://github.com/microsoft/testfx/pull/2104)
* TrimStackTrace should handle empty stacktrace by @Evangelink in [#2113](https://github.com/microsoft/testfx/pull/2113)
* Update Public API by @Evangelink in [#2116](https://github.com/microsoft/testfx/pull/2116)
* Fix issue #2121 DataTestMethodAttribute is missing a constructor by @HannoZ in [#2125](https://github.com/microsoft/testfx/pull/2125)

### New Contributors

* @jakubch1 made their first contribution in [#2071](https://github.com/microsoft/testfx/pull/2071)
* @drognanar made their first contribution in [#2073](https://github.com/microsoft/testfx/pull/2073)
* @HannoZ made their first contribution in [#2125](https://github.com/microsoft/testfx/pull/2125)

### Artifacts

* MSTest: [3.2.0-preview.24069.3](https://www.nuget.org/packages/MSTest/3.2.0-preview.24069.3)
* MSTest.TestFramework: [3.2.0-preview.24069.3](https://www.nuget.org/packages/MSTest.TestFramework/3.2.0-preview.24069.3)
* MSTest.TestAdapter: [3.2.0-preview.24069.3](https://www.nuget.org/packages/MSTest.TestAdapter/3.2.0-preview.24069.3)
* MSTest.Analyzers: [3.2.0-preview.24069.3](https://www.nuget.org/packages/MSTest.Analyzers/3.2.0-preview.24069.3)
* Microsoft.Testing.Extensions.CrashDump: [1.0.0-preview.24068.6](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/1.0.0-preview.24068.6)
* Microsoft.Testing.Extensions.HangDump: [1.0.0-preview.24068.6](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/1.0.0-preview.24068.6)
* Microsoft.Testing.Extensions.HotReload: [1.0.0-preview.24068.6](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/1.0.0-preview.24068.6)
* Microsoft.Testing.Extensions.Retry: [1.0.0-preview.24068.6](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/1.0.0-preview.24068.6)
* Microsoft.Testing.Extensions.TrxReport: [1.0.0-preview.24068.6](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/1.0.0-preview.24068.6)

## <a name="3.2.0-preview.23623.1" />[3.2.0-preview.23623.1] - 2023-12-23

See full log [here](https://github.com/Microsoft/testfx/compare/v3.2.0-preview.23622.1...3.2.0-preview.23623.1)

### Fixed

* Add auto-generated header to generated entry point (through upgrade of `Microsoft.Testing.Platform.MSBuild` to 1.0.0-preview.23622.9)

### Artifacts

* MSTest: [3.2.0-preview.23623.1](https://www.nuget.org/packages/MSTest/3.2.0-preview.23623.1)
* MSTest.TestFramework: [3.2.0-preview.23623.1](https://www.nuget.org/packages/MSTest.TestFramework/3.2.0-preview.23623.1)
* MSTest.TestAdapter: [3.2.0-preview.23623.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.2.0-preview.23623.1)
* MSTest.Analyzers: [3.2.0-preview.23623.1](https://www.nuget.org/packages/MSTest.Analyzers/3.2.0-preview.23623.1)

## <a name="3.2.0-preview.23622.1" />[3.2.0-preview.23622.1] - 2023-12-22

See full log [here](https://github.com/Microsoft/testfx/compare/v3.1.1...v3.2.0-preview.23622.1)

### Added

* MSTest runner by @Evangelink in [#1775](https://github.com/microsoft/testfx/pull/1775)
* Add analyzers by @Evangelink in [#1870](https://github.com/microsoft/testfx/pull/1870)
* Add more analyzers by @Evangelink in [#1974](https://github.com/microsoft/testfx/pull/1974)
* Rework MSTEST0002 and MSTEST0003 by @Evangelink in [#1985](https://github.com/microsoft/testfx/pull/1985)
* Update description of MSTEST0001 by @Evangelink in [#1988](https://github.com/microsoft/testfx/pull/1988)
* MSTEST0004: Public types should be test classes by @Evangelink in [#1989](https://github.com/microsoft/testfx/pull/1989)

### Fixed

* Don't stop by @nohwnd in [#1737](https://github.com/microsoft/testfx/pull/1737)
* Fix DataRowAttribute to be cls compliant by @Evangelink in [#1878](https://github.com/microsoft/testfx/pull/1878)
* Workaround issue with managed type name utility by @Evangelink in [#1876](https://github.com/microsoft/testfx/pull/1876)
* Fix path normalization by @Evangelink in [#1880](https://github.com/microsoft/testfx/pull/1880)

### Chores

* Bump and cleanup global versions by @Evangelink in [#1754](https://github.com/microsoft/testfx/pull/1754)
* Eng and global.json housekeeping by @Evangelink in [#1756](https://github.com/microsoft/testfx/pull/1756)
* Code clean-up  by @ViktorHofer in [#1757](https://github.com/microsoft/testfx/pull/1757)
* Bump StyleCop analyzers version by @Evangelink in [#1761](https://github.com/microsoft/testfx/pull/1761)
* Bump version of test dependencies by @Evangelink in [#1762](https://github.com/microsoft/testfx/pull/1762)
* Bump VSTest deps to 17.7.2 by @Evangelink in [#1769](https://github.com/microsoft/testfx/pull/1769)
* Pin Moq to 4.18.4 for security by @Evangelink in [#1770](https://github.com/microsoft/testfx/pull/1770)
* Clean nuget.config by @Evangelink in [#1772](https://github.com/microsoft/testfx/pull/1772)
* Fix warnings and cleanup infra by @Evangelink in [#1773](https://github.com/microsoft/testfx/pull/1773)
* Drop ruleset in favor of editorconfig by @Evangelink in [#1780](https://github.com/microsoft/testfx/pull/1780)
* Enable CA1001 by @Evangelink in [#1811](https://github.com/microsoft/testfx/pull/1811)
* Set analysis level to latest-recommended by @Evangelink in [#1816](https://github.com/microsoft/testfx/pull/1816)
* Use modern styles and rules by @Evangelink in [#1852](https://github.com/microsoft/testfx/pull/1852)
* Add third-parties licenses by @Evangelink in [#1955](https://github.com/microsoft/testfx/pull/1955)

### New Contributors

* @ViktorHofer made their first contribution in [#1757](https://github.com/microsoft/testfx/pull/1757)

### Artifacts

* MSTest: [3.2.0-preview.23622.1](https://www.nuget.org/packages/MSTest/3.2.0-preview.23622.1)
* MSTest.TestFramework: [3.2.0-preview.23622.1](https://www.nuget.org/packages/MSTest.TestFramework/3.2.0-preview.23622.1)
* MSTest.TestAdapter: [3.2.0-preview.23622.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.2.0-preview.23622.1)
* MSTest.Analyzers: [3.2.0-preview.23622.1](https://www.nuget.org/packages/MSTest.Analyzers/3.2.0-preview.23622.1)

## <a name="3.1.1" />[3.1.1] - 2023-07-14

### Fixed

* Artifact `3.1.0` was corrupted during pipeline and for security reasons we cannot regenerate it.

See full log [here](https://github.com/Microsoft/testfx/compare/v3.1.0...v3.1.1)

### Artifacts

* MSTest: [3.1.1](https://www.nuget.org/packages/MSTest/3.1.1)
* MSTest.TestFramework: [3.1.1](https://www.nuget.org/packages/MSTest.TestFramework/3.1.1)
* MSTest.TestAdapter: [3.1.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.1.1)

## <a name="3.1.0" />[3.1.0] - 2023-07-14

See full log [here](https://github.com/Microsoft/testfx/compare/v3.0.4...v3.1.0)

### Added

* Add AsyncLocal warning [#1710](https://github.com/Microsoft/testfx/pull/1710)
* Adding warning that AssemblyResolution works only with .NET Frameworks [#1669](https://github.com/Microsoft/testfx/pull/1669)
* Onboarding to GitOps.ResourceManagement [#1688](https://github.com/Microsoft/testfx/pull/1688)
* Document data source configuration [#1595](https://github.com/Microsoft/testfx/pull/1595)
* Enable TestContext.AddResultFile API for WinUI [#1593](https://github.com/Microsoft/testfx/pull/1593)
* Add documentation for [DeploymentItem] attribute [#1581](https://github.com/Microsoft/testfx/pull/1581)
* Docs: add links to MS learning docs [#1577](https://github.com/Microsoft/testfx/pull/1577)
* Document LegacySettings- support as discontinued delta from MSTestV1 [#1571](https://github.com/Microsoft/testfx/pull/1571)
* Add link to MSTest element settings in the Documentation page [#1552](https://github.com/Microsoft/testfx/pull/1552)
* Add TreatDiscoveryWarningsAsErrors setting [#1547](https://github.com/Microsoft/testfx/pull/1547)

### Changed

* Rename Helper.cs into Guard.cs
* Update Changelog.md
* Bump TestPlatform to 17.6.0 [#1666](https://github.com/Microsoft/testfx/pull/1666)

### Fixed

* Avoid crash when method is not found using FQN [#1714](https://github.com/Microsoft/testfx/pull/1714)
* Prevent swallowing inner exception in async error [#1712](https://github.com/Microsoft/testfx/pull/1712)
* DeploymentItem: add test for file deployment using Windows/Linux path [#1710](https://github.com/Microsoft/testfx/pull/1710)
* Fix parallel output isolation [#1705](https://github.com/Microsoft/testfx/pull/1705)
* Fix DeploymentItem to support trailing directory separator [#1703](https://github.com/Microsoft/testfx/pull/1703)
* Fix Logger.LogMessage to not call string.Format when no arguments are provided [#1702](https://github.com/Microsoft/testfx/pull/1702)
* Update AreEqual/AreNotEqual XML documentation [#1563](https://github.com/Microsoft/testfx/pull/1563)

### New Contributors

* @unsegnor made their first contribution in <https://github.com/microsoft/testfx/pull/1104>
* @JasonMurrayCole made their first contribution in <https://github.com/microsoft/testfx/pull/1119>
* @engyebrahim made their first contribution in <https://github.com/microsoft/testfx/pull/1172>
* @johnthcall made their first contribution in <https://github.com/microsoft/testfx/pull/1547>

### Artifacts

* MSTest: [3.1.0](https://www.nuget.org/packages/MSTest/3.1.0)
* MSTest.TestFramework: [3.1.0](https://www.nuget.org/packages/MSTest.TestFramework/3.1.0)
* MSTest.TestAdapter: [3.1.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.1.0)

## <a name="3.0.4" />[3.0.4] - 2023-06-01

See full log [here](https://github.com/microsoft/testfx/compare/v3.0.3...v3.0.4)

### Fixed

* Fix out of range exception in deployment tool [#1682](https://github.com/microsoft/testfx/pull/1682)
* Fix issue/crash with deployment items and disabled app domains [#1681](https://github.com/microsoft/testfx/pull/1681)

### Artifacts

* MSTest: [3.0.4](https://www.nuget.org/packages/MSTest/3.0.4)
* MSTest.TestFramework: [3.0.4](https://www.nuget.org/packages/MSTest.TestFramework/3.0.4)
* MSTest.TestAdapter: [3.0.4](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.4)

## <a name="3.0.3" />[3.0.3] - 2023-05-24

See full log [here](https://github.com/Microsoft/testfx/compare/v3.0.2...v3.0.3)

### Changed

* Use Test Platform 17.4.1 instead of 17.4.0

### Fixed

* Revert change for timeout test execution [#1675](https://github.com/Microsoft/testfx/pull/1675)
* Fix assembly resolution error [#1670](https://github.com/Microsoft/testfx/pull/1670)
* Revert usage of System.Collections.Immutable for TypeCache
* Remove DataRowAttribute argument count limitation [#1646](https://github.com/Microsoft/testfx/pull/1646)

### Artifacts

* MSTest: [3.0.3](https://www.nuget.org/packages/MSTest/3.0.3)
* MSTest.TestFramework: [3.0.3](https://www.nuget.org/packages/MSTest.TestFramework/3.0.3)
* MSTest.TestAdapter: [3.0.3](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.3)

## <a name="3.0.2" />[3.0.2] - 2022-12-27

See full log [here](https://github.com/microsoft/testfx/compare/v3.0.1...v3.0.2)

### Fixed

* Fix issue cannot load Microsoft.TestPlatform.CoreUtilities [#1502](https://github.com/microsoft/testfx/pull/1502)

### Artifacts

* MSTest: [3.0.2](https://www.nuget.org/packages/MSTest/3.0.2)
* MSTest.TestFramework: [3.0.2](https://www.nuget.org/packages/MSTest.TestFramework/3.0.2)
* MSTest.TestAdapter: [3.0.2](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.2)

## <a name="3.0.1" />[3.0.1] - 2022-12-20

See full log [here](https://github.com/microsoft/testfx/compare/v3.0.0...v3.0.1)

### Fixed

* Fix some race condition issue [#1477](https://github.com/microsoft/testfx/pull/1477)
* Fix cleanup inheritance calls [#1475](https://github.com/microsoft/testfx/pull/1475)
* Fix class/assembly cleanups log collect and attachment [#1470](https://github.com/microsoft/testfx/pull/1470)
* Allow most APIs to accept nullable values or arguments [#1467](https://github.com/microsoft/testfx/pull/1467)
* Add console, trace and debug writeline calls to the lifecycle integration tests [#1464](https://github.com/microsoft/testfx/pull/1464)
* Revert Framework.Extension project to be CLSCompliant [#1450](https://github.com/microsoft/testfx/pull/1450)
* Fix regressions with DataRow supported arguments [#1446](https://github.com/microsoft/testfx/pull/1446)
* Add parent domain assembly resolver for netfx [#1443](https://github.com/microsoft/testfx/pull/1443)
* Remove unneeded dash in InformationalVersion for RTM builds
* Add NotNullAttribute postcondition to Assert APIs [#1441](https://github.com/microsoft/testfx/pull/1441)

### Artifacts

* MSTest: [3.0.1](https://www.nuget.org/packages/MSTest/3.0.1)
* MSTest.TestFramework: [3.0.1](https://www.nuget.org/packages/MSTest.TestFramework/3.0.1)
* MSTest.TestAdapter: [3.0.1](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.1)

## <a name="3.0.0" />[3.0.0] - 2022-12-06

See full log [here](https://github.com/microsoft/testfx/compare/v2.2.10...v3.0.0)

Breaking changes announcements [#1274](https://github.com/microsoft/testfx/issues/1274)

### Added

* Assert.AreEqual/AreNotEqual overloads with IEquatable [#1433](https://github.com/microsoft/testfx/pull/1433)
* Add DoesNotReturn attribute for Assert.Inconclusive methods [#1422](https://github.com/microsoft/testfx/pull/1422)
* Allow to override GetDisplayName method from DataRowAttribute [#1413](https://github.com/microsoft/testfx/pull/1413)
* Add computer name on the test result [#1409](https://github.com/microsoft/testfx/pull/1409)
* Add public api analyzers [#1318](https://github.com/microsoft/testfx/pull/1318)
* Enable nullable for TestAdapter project [#1370](https://github.com/microsoft/testfx/pull/1370)
* Enable nullable for Platform Services [#1366](https://github.com/microsoft/testfx/pull/1366)
* Enable nullables for Framework [#1365](https://github.com/microsoft/testfx/pull/1365)
* Enable nullable for TestFramework.Extensions [#1363](https://github.com/microsoft/testfx/pull/1363)
* [breaking change] Introduce strategies for test ID generation [#1306](https://github.com/microsoft/testfx/pull/1306)
* Add support for AsyncDisposable cleanup [#1288](https://github.com/microsoft/testfx/pull/1288)
* Add Assert.IsInstanceOfType\<T\> [#1241](https://github.com/microsoft/testfx/pull/1241)

### Changed

* [breaking change] Follow supported .NET frameworks:
  * Dropped support for .NET Framework before 4.6.2 (net462)
  * Dropped support for .NET Standard before 2.0 (netstandard2.0)
  * Dropped support for UWP before 16299
  * Dropped support for WinUI before 18362
  * Replaced support of .NET 5 by .NETCore 3.1 and .NET 6.0
* [breaking change] Assert.AreSame/AreNotSame use generic instead of object [#1430](https://github.com/microsoft/testfx/pull/1430)
* Make BeginTimer and EndTimer methods obsolete [#1425](https://github.com/microsoft/testfx/pull/1425)
* [breaking change] Unify DeploymentDirectory location across target frameworks [#1414](https://github.com/microsoft/testfx/pull/1414)
* [breaking change] Add class/assembly cleanup/init messages to first/last test [#1390](https://github.com/microsoft/testfx/pull/1390)
* Document that DeploymentItemAttribute only works for a test class with test method [#1399](https://github.com/microsoft/testfx/pull/1399)
* Use NewtonsoftJson v13.0.1 [#1361](https://github.com/microsoft/testfx/pull/1361)
* [breaking change] Merge timeout behaviors for .NET Core and .NET Framework [#1296](https://github.com/microsoft/testfx/pull/1296)
* Mark exceptions with SerializableAttribute [#1186](https://github.com/microsoft/testfx/pull/1186)

### Removed

* [breaking change] Remove Assert.AreEqual/AreNotEqual overloads with object object [#1429](https://github.com/microsoft/testfx/pull/1429)

### Fixed

* Propagate UI culture to appdomain [#1401](https://github.com/microsoft/testfx/pull/1401)
* Include localization in Test Framework NuGet [#1397](https://github.com/microsoft/testfx/pull/1397)
* [breaking change] Refactor available ctors for DataRowAttribute [#1332](https://github.com/microsoft/testfx/pull/1332)
* Fix issue causing null ref when test class has no namespace [#1283](https://github.com/microsoft/testfx/pull/1283)
* [breaking change] Unwrap real exception from TargetInvocationException [#1254](https://github.com/microsoft/testfx/pull/1254)
* Fixed the case ignoring in AreEqual() with culture parameter [#1216](https://github.com/microsoft/testfx/pull/1216)

### Artifacts

* MSTest: [3.0.0](https://www.nuget.org/packages/MSTest/3.0.0)
* MSTest.TestFramework: [3.0.0](https://www.nuget.org/packages/MSTest.TestFramework/3.0.0)
* MSTest.TestAdapter: [3.0.0](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.0)

## <a name="3.0.0-preview-20221122-01" />[3.0.0-preview-20221122-01] - 2022-11-23

See full log [here](https://github.com/microsoft/testfx/compare/v3.0.0-preview-20221110-04...v3.0.0-preview-20221122-01)

### Added

* Enable proper tooling for localization [#1393](https://github.com/microsoft/testfx/pull/1393)
* Include localization in Test Framework NuGet [#1397](https://github.com/microsoft/testfx/pull/1397)
* Propagate UI culture to appdomain [#1401](https://github.com/microsoft/testfx/pull/1401)

### Changed

* Visual Studio grouping tests by `Traits` rendering has changed. To keep something similar to the past, you will have to use group by `Traits` + `Class`. [#1634](https://github.com/microsoft/testfx/issues/1634)
* Disable again the generation of localization files
* Cleanup TestFx.Loc.props
* Update xlf files [#1387](https://github.com/microsoft/testfx/pull/1387)
* Reformat scripts
* Re-enable localization of dlls in CI [#1386](https://github.com/microsoft/testfx/pull/1386)
* Move platformservices localization files to the right folder [#1384](https://github.com/microsoft/testfx/pull/1384)
* Reformat and simplify build.ps1
* Replace other instances of Env.CurrentDir with Assembly.GetExecutingA… [#1380](https://github.com/microsoft/testfx/pull/1380)
* Update testfx repo detection mechanism [#1378](https://github.com/microsoft/testfx/pull/1378)
* Rename folder containing localization dlls in MSTest.TestAdapter NuGet package [#1398](https://github.com/microsoft/testfx/pull/1398)

### Fixed

* Fix TestContext nullabilities [#1382](https://github.com/microsoft/testfx/pull/1382)
* Fix ThrowsException methods return nullability [#1381](https://github.com/microsoft/testfx/pull/1381)
* Fix all markdown issues in releases.md
* Fix some failing debug assertions [#1379](https://github.com/microsoft/testfx/pull/1379)
* DeploymentItemAttribute only works for a test class with test method [#1399](https://github.com/microsoft/testfx/pull/1399)

### Removed

* Remove unused build switch
* Remove DependsOnTargets="TestFxLocalization" for signing
* Remove stale xlf.lcl files

### Artifacts

* MSTest: [3.0.0-preview-20221122-01](https://www.nuget.org/packages/MSTest/3.0.0-preview-20221122-01)
* MSTest.TestFramework: [3.0.0-preview-20221122-01](https://www.nuget.org/packages/MSTest.TestFramework/3.0.0-preview-20221122-01)
* MSTest.TestAdapter: [3.0.0-preview-20221122-01](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.0-preview-20221122-01)

## <a name="3.0.0-preview-20221110-04" />[3.0.0-preview-20221110-04] - 2022-11-11

See full log [here](https://github.com/microsoft/testfx/compare/v2.3.0-preview-20220810-02...v3.0.0-preview-20221110-04)

### Added

* Enable and fix CA1806 violations [#1227](https://github.com/microsoft/testfx/pull/1227)
* Enable and fix CA1822 (make member static) violations [#1226](https://github.com/microsoft/testfx/pull/1226)
* Enable and fix performance analyzers [#1223](https://github.com/microsoft/testfx/pull/1223)
* Id generator logic restored. [#1174](https://github.com/microsoft/testfx/pull/1174)
* Enable nullable for TestAdapter project [#1370](https://github.com/microsoft/testfx/pull/1370)
* Enable nullable for Platform Services [#1366](https://github.com/microsoft/testfx/pull/1366)
* Enable nullables for Framework [#1365](https://github.com/microsoft/testfx/pull/1365)
* Enable nullable for TestFramework.Extensions [#1363](https://github.com/microsoft/testfx/pull/1363)
* Add tests to document test suite messages [#1313](https://github.com/microsoft/testfx/pull/1313)
* Add support for AsyncDisposable cleanup [#1288](https://github.com/microsoft/testfx/pull/1288)
* Added version parameter to build script [#1264](https://github.com/microsoft/testfx/pull/1264)
* Add WinUI tests to PlatformServices [#1234](https://github.com/microsoft/testfx/pull/1234)
* Add net6.0 tests for PlatformServices [#1233](https://github.com/microsoft/testfx/pull/1233)
* Add Assert.IsInstanceOfType\<T\> [#1241](https://github.com/microsoft/testfx/pull/1241)
* Add some simple test framwork to allow testing MSTest [#1242](https://github.com/microsoft/testfx/pull/1242)
* Merge testfx-docs repo here and update links [#1326](https://github.com/microsoft/testfx/pull/1326)
* Install required .NET before build instead of before test [#1290](https://github.com/microsoft/testfx/pull/1290)
* Allow mirroring. **BYPASS_SECRET_SCANNING**
* List files in case Pdb2Pdb.exe is not found
* List files in case Pdb2Pdb.exe is not found
* Align MicrosoftDiaSymReaderPdb2PdbVersion with arcade one
* Use Foreach-Object to display contents
* Define and apply field naming conventions [#1198](https://github.com/microsoft/testfx/pull/1198)

### Changed

* Improve release notes generator [#1374](https://github.com/microsoft/testfx/pull/1374)
* Bump version to v3.0.0 [#1373](https://github.com/microsoft/testfx/pull/1373)
* Sort lines in public API files
* Refactor reflection helper to return strongly typed attributes [#1369](https://github.com/microsoft/testfx/pull/1369)
* Use NewtonsoftJson v13.0.1 [#1361](https://github.com/microsoft/testfx/pull/1361)
* Refactor integration tests to use FluentAssertions [#1349](https://github.com/microsoft/testfx/pull/1349)
* Revert temporary hack related to .net 6.0.10 [#1346](https://github.com/microsoft/testfx/pull/1346)
* Naming and using cleanup [#1347](https://github.com/microsoft/testfx/pull/1347)
* Update links to official MSTest documentation
* Revisit RFCs [#1342](https://github.com/microsoft/testfx/pull/1342)
* Refactor available ctors for DataRowAttribute [#1332](https://github.com/microsoft/testfx/pull/1332)
* Refactor solution items to add UnitTests folder
* Update prerequisites section
* Bring back stylecop analyzers [#1314](https://github.com/microsoft/testfx/pull/1314)
* Update version of Microsoft.CodeAnalysis.PublicApiAnalyzers
* Update GitHub issues and PR templates [#1315](https://github.com/microsoft/testfx/pull/1315)
* Update Install-DotNetCli end to use similar wording as other steps
* Update PlatformServices.Desktop.Component.Tests to use new test fwk [#1252](https://github.com/microsoft/testfx/pull/1252)
* Update comment about assembly version in TestFx.targets
* Update azure-pipelines.yml
* Update azure-pipelines.yml
* Various styling refactoring [#1334](https://github.com/microsoft/testfx/pull/1334)
* Merge extension projects into one [#1202](https://github.com/microsoft/testfx/pull/1202)
* Merge various PlatformServices.XXX.UnitTests projects together [#1232](https://github.com/microsoft/testfx/pull/1232)
* Merge interfaces into PlatformServices [#1293](https://github.com/microsoft/testfx/pull/1293)
* Merge all implementations of TestContext [#1302](https://github.com/microsoft/testfx/pull/1302)
* Merge timeout behaviors for .NET Core and .NET Framework [#1296](https://github.com/microsoft/testfx/pull/1296)
* Merge TFM specific classes together [#1209](https://github.com/microsoft/testfx/pull/1209)
* Merge TFM specific PlatformServices into a single PlatformServices [#1208](https://github.com/microsoft/testfx/pull/1208)
* Try to simplify automation.cli and support of different TFMs [#1312](https://github.com/microsoft/testfx/pull/1312)
* Don't exclude TestAssets from source control [#1298](https://github.com/microsoft/testfx/pull/1298)
* Rename projects to better match assembly [#1291](https://github.com/microsoft/testfx/pull/1291)
* Convert PlatformServices tests to use new test framework [#1249](https://github.com/microsoft/testfx/pull/1249)
* Convert test.core.unit.tests to use local test framework [#1259](https://github.com/microsoft/testfx/pull/1259)
* Convert smoke.e2e.tests and DiscoveryAndExecutionTests to use local test framework [#1261](https://github.com/microsoft/testfx/pull/1261)
* Ensure we run core tests for all supported TFMs [#1268](https://github.com/microsoft/testfx/pull/1268)
* Ensure we run adapter tests for all supported TFMs [#1267](https://github.com/microsoft/testfx/pull/1267)
* Cleanup solution and test projects [#1282](https://github.com/microsoft/testfx/pull/1282)
* Cleanup on MSTest.CoreAdapter.UnitTests [#1257](https://github.com/microsoft/testfx/pull/1257)
* Unwrap real exception from TargetInvocationException [#1254](https://github.com/microsoft/testfx/pull/1254)
* Convert MSTest.CoreAdapter.Unit.Tests to use new test framework [#1245](https://github.com/microsoft/testfx/pull/1245)
* Revert CallerArgumentExpression attribute changes [#1251](https://github.com/microsoft/testfx/pull/1251)
* Do not fail generate release task notes when skipping
* Display full packages folder
* Discard auto PRs in write-release-notes.ps1 [#1173](https://github.com/microsoft/testfx/pull/1173)
* Split Assert class per group of feature [#1238](https://github.com/microsoft/testfx/pull/1238)
* Improve assembly versions and available metadata [#1231](https://github.com/microsoft/testfx/pull/1231)
* More project/files simplification for PlatformServices [#1221](https://github.com/microsoft/testfx/pull/1221)
* Projects cleanup [#1219](https://github.com/microsoft/testfx/pull/1219)
* Factorize out some project properties [#1217](https://github.com/microsoft/testfx/pull/1217)
* Find TP package version using Versions.props [#1211](https://github.com/microsoft/testfx/pull/1211)
* Simplify Link references in csproj
* Review compiler directives [#1203](https://github.com/microsoft/testfx/pull/1203)
* Ignore commit 4bb533 from revision logs
* Use newer C# syntaxes [#1200](https://github.com/microsoft/testfx/pull/1200)
* Ignore commit "Define and apply field naming conventions"
* Ignore convert to file-scoped namespace revision
* Convert to file-scoped namespaces [#1197](https://github.com/microsoft/testfx/pull/1197)
* Target .NET 6 instead of .NET 5 [#1196](https://github.com/microsoft/testfx/pull/1196)
* Prefer specific tfm over generic ones [#1192](https://github.com/microsoft/testfx/pull/1192)
* Simplify projects dependencies and files [#1193](https://github.com/microsoft/testfx/pull/1193)
* Target netstandard2.0 as minimal netstandard [#1194](https://github.com/microsoft/testfx/pull/1194)
* Simplify UWP projects [#1191](https://github.com/microsoft/testfx/pull/1191)
* Make private fields readonly when possible [#1188](https://github.com/microsoft/testfx/pull/1188)
* Favor inline initialization over static ctor [#1189](https://github.com/microsoft/testfx/pull/1189)
* Mark exceptions with SerializableAttribute [#1186](https://github.com/microsoft/testfx/pull/1186)
* Apply modern C# features/syntaxes [#1183](https://github.com/microsoft/testfx/pull/1183)
* Rename MSTest.CoreAdapter into MSTest.TestAdapter [#1181](https://github.com/microsoft/testfx/pull/1181)
* Move test projects to SDK style [#1179](https://github.com/microsoft/testfx/pull/1179)

### Fixed

* Fix broken tests and refactor test API [#1352](https://github.com/microsoft/testfx/pull/1352)
* Fix issue causing null ref when test class has no namespace [#1283](https://github.com/microsoft/testfx/pull/1283)
* Fix localization path
* Fix .gitignore
* Fix included package [#1280](https://github.com/microsoft/testfx/pull/1280)
* Fix common.lib.ps1 to download latest patched SDKs [#1279](https://github.com/microsoft/testfx/pull/1279)
* Fix included package [#1280](https://github.com/microsoft/testfx/pull/1280)
* Fix common.lib.ps1 to download latest patched SDKs [#1279](https://github.com/microsoft/testfx/pull/1279)
* Fix broken unit tests
* Fix testasset of ComponentTests
* Fix some typos in test names
* Fix some typos
* Fix NuGet packages content + support netcoreapp3.1 [#1228](https://github.com/microsoft/testfx/pull/1228)
* Fix broken tests on main [#1265](https://github.com/microsoft/testfx/pull/1265)
* Fixed the case ignoring in AreEqual() with culture parameter [#1216](https://github.com/microsoft/testfx/pull/1216)
* Fix behavior for netcore TFMs [#1230](https://github.com/microsoft/testfx/pull/1230)
* Fixed package restore.

### Removed

* Remove class/assembly initialization messages from logs. [#1339](https://github.com/microsoft/testfx/pull/1339)
* Remove unexpected dll in target [#1308](https://github.com/microsoft/testfx/pull/1308)

### Artifacts

* MSTest: [3.0.0-preview-20221110-04](https://www.nuget.org/packages/MSTest/3.0.0-preview-20221110-04)
* MSTest.TestFramework: [3.0.0-preview-20221110-04](https://www.nuget.org/packages/MSTest.TestFramework/3.0.0-preview-20221110-04)
* MSTest.TestAdapter: [3.0.0-preview-20221110-04](https://www.nuget.org/packages/MSTest.TestAdapter/3.0.0-preview-20221110-04)

## <a name="2.3.0-preview-20220810-02" />[2.3.0-preview-20220810-02] 2022-08-10

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.10...v2.3.0-preview-20220810-02)

### Added

* [Add whitespace editorconfig and run dotnet format whitespace](https://github.com/microsoft/testfx/pull/1090)
* [Adding Microsoft SECURITY.MD](https://github.com/microsoft/testfx/pull/1109)

### Changed

* [Better messages for XXXInitialize and XXXCleanup](https://github.com/microsoft/testfx/pull/1147)
* [TestResults folder names are now cross platform compatible, as per #678](https://github.com/microsoft/testfx/pull/1119)
* Bumped up version to 2.3.0
* [Assert failure messages](https://github.com/microsoft/testfx/pull/1172)
* [Ensure assertions do not fail with FormatException](https://github.com/microsoft/testfx/pull/1126)
* [Prevent format exceptions when parameters array is empty](https://github.com/microsoft/testfx/pull/1124)
* [\[main\] Update dependencies from dotnet/arcade](https://github.com/microsoft/testfx/pull/1098)

### Fixed

* [Fixed issues with SDK style projects.](https://github.com/microsoft/testfx/pull/1171)

### Removed

* [Remove unused classes](https://github.com/microsoft/testfx/pull/1089)

### Artifacts

* MSTest: [2.3.0-preview-20220810-02](https://www.nuget.org/packages/MSTest/2.3.0-preview-20220810-02)
* MSTest.TestFramework: [2.3.0-preview-20220810-02](https://www.nuget.org/packages/MSTest.TestFramework/2.3.0-preview-20220810-02)
* MSTest.TestAdapter: [2.3.0-preview-20220810-02](https://www.nuget.org/packages/MSTest.TestAdapter/2.3.0-preview-20220810-02)

## <a name="2.2.10" />[2.2.10] - 2022-04-26

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.10-preview-20220414-01...v2.2.10)

### Added

* [Added more fail paths for data serialization.](https://github.com/microsoft/testfx/pull/1084)
* [Added MSTest meta-package.](https://github.com/microsoft/testfx/pull/1076)
* [Static init of StackTraceHelper.typesToBeExcluded](https://github.com/microsoft/testfx/pull/1055)

### Changed

* [Update description of the Nuget packages](https://github.com/microsoft/testfx/pull/981)
* [Converted files to utf-8 so they can be diffed.](https://github.com/microsoft/testfx/pull/1070)
* [Update dependencies from https://github.com/dotnet/arcade build 20220425.6](https://github.com/microsoft/testfx/pull/1087)
* [Run dotnet format whitespace](https://github.com/microsoft/testfx/pull/1085)

### Fixed

* [Test execution bugs in specific TFMs addressed.](https://github.com/microsoft/testfx/pull/1071)

### Artifacts

* MSTest: [2.2.10](https://www.nuget.org/packages/MSTest/2.2.10)
* MSTest.TestFramework: [2.2.10](https://www.nuget.org/packages/MSTest.TestFramework/2.2.10)
* MSTest.TestAdapter: [2.2.10](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.10)

## <a name="2.2.10-preview-20220414-01" />[2.2.10-preview-20220414-01] - 2022-04-14

### Fixed

* [Fix write conflicts in parallel output](https://github.com/microsoft/testfx/pull/1068)
* [Fixed test run executable files.](https://github.com/microsoft/testfx/pull/1064)
* [\[UITestMethod\] should invoke test method with null](https://github.com/microsoft/testfx/pull/1045)

### Artifacts

* MSTest.TestFramework: [2.2.10-preview-20220414-01](https://www.nuget.org/packages/MSTest.TestFramework/2.2.10-preview-20220414-01)
* MSTest.TestAdapter: [2.2.10-preview-20220414-01](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.10-preview-20220414-01)

## <a name="2.2.9" />[2.2.9] 2022-04-08

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.8...v2.2.9)

### Parallel output

> 🙇 Shout out to @SimonCropp, for bringing this functionality to XUnit in his <https://github.com/SimonCropp/XunitContext> project. And being an inspiration for implementing this.

MSTest 2.2.9 captures all Console output and attaches it to the correct test, even if you are running tests in parallel. This output is captured from your test code as well as from the tested code. And it requires no special setup.

#### Before

In 2.2.8, test output is scattered among tests, in our example, one unlucky test gets all the output of other tests just mixed together:

![image](https://user-images.githubusercontent.com/5735905/162252520-0572d932-c798-4b7e-8961-44f39b5a32b9.png)

#### After

With 2.2.9, each output is correctly attached to the test that produced it:

![image](https://user-images.githubusercontent.com/5735905/162252738-2dae4ff3-d7bf-473a-9304-66cf25510a89.png)
![image](https://user-images.githubusercontent.com/5735905/162252762-4304b9c0-1e60-4089-83e3-e8f341cb9329.png)

Also notice that we are also capturing debug, trace and error. And we are not awaiting the FastChild method, and the output is still assigned correctly.  [Souce code.](https://gist.github.com/nohwnd/2936753d94301d7991059660d1d63a8a)

#### Limitations

Due to the way that class and assembly initialize, and cleanup are invoked, their output will end up in the first test that run (or last for cleanup). This is unfortunately not easily fixable.

### Artifacts

* MSTest.TestFramework: [2.2.9](https://www.nuget.org/packages/MSTest.TestFramework/2.2.9)
* MSTest.TestAdapter: [2.2.9](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.9)

## <a name="2.2.8" />[2.2.8] - 2021-11-23

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.7...v2.2.8)

### Added

* [Added internal versioning](https://github.com/microsoft/testfx/pull/1012)
* [Added 500ms overhead to parallel execution tests.](https://github.com/microsoft/testfx/pull/962)
* [Downgrade uwp](https://github.com/microsoft/testfx/pull/1008)
* [Add DoesNotReturnIf to Assert.IsTrue/Assert.IsFalse](https://github.com/microsoft/testfx/pull/1005)
* [Implement Class Cleanup Lifecycle selection](https://github.com/microsoft/testfx/pull/968)

### Changed

* Dependency version updates.
* [Added 500ms overhead to parallel execution tests.](https://github.com/microsoft/testfx/pull/962)
* [Updated to WindowsAppSDK 1.0.0 GA](https://github.com/microsoft/testfx/pull/1009)
* [Updated to WindowsAppSDK 1.0.0-preview1](https://github.com/microsoft/testfx/pull/985)
* [Cherry-picking the changes from 2.2.7](https://github.com/microsoft/testfx/pull/958)

### Fixed

* [Fixed .nuspec files to mitigate NU5050 error.](https://github.com/microsoft/testfx/pull/1011)
* [Fix concurrent issues in DataSerializationHelper](https://github.com/microsoft/testfx/pull/998)
* [Fix for incorrect Microsoft.TestPlatform.AdapterUtilities.dll for net45 target (#980)](https://github.com/microsoft/testfx/pull/988)
* [CVE-2017-0247 fixed](https://github.com/microsoft/testfx/pull/976)

### Artifacts

* MSTest.TestFramework: [2.2.8](https://www.nuget.org/packages/MSTest.TestFramework/2.2.8)
* MSTest.TestAdapter: [2.2.8](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.8)

## <a name="2.2.7" />[2.2.7] - 2021-09-03

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.6...v2.2.7)

### Changed

* [Resolve dependencies from GAC](https://github.com/microsoft/testfx/pull/951)

### Fixed

* [Fixed missing strong-name and Authenticode signatures](https://github.com/microsoft/testfx/pull/956)

### Artifacts

* MSTest.TestFramework: [2.2.7](https://www.nuget.org/packages/MSTest.TestFramework/2.2.7)
* MSTest.TestAdapter: [2.2.7](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.7)

## <a name="2.2.6" />[2.2.6] - 2021-08-25

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.5...v2.2.6)

### Changed

* [Enable internal testclass discovery (#937)](https://github.com/microsoft/testfx/pull/944)
* Allow opting-out of ITestDataSource test discovery.

### Fixed

* [Fix DateTime looses significant digits in DynamicData (#875)](https://github.com/microsoft/testfx/pull/907)

### Artifacts

* MSTest.TestFramework: [2.2.6](https://www.nuget.org/packages/MSTest.TestFramework/2.2.6)
* MSTest.TestAdapter: [2.2.6](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.6)

## <a name="2.2.5" />[2.2.5] - 2021-06-28

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.4...v2.2.5)

### Added

* [Added missing framework references for WinUI](https://github.com/microsoft/testfx/pull/890)

### Changed

* [Upgraded winui to 0.8.0](https://github.com/microsoft/testfx/pull/888)
* [Replaced license file with an expression.](https://github.com/microsoft/testfx/pull/846)

### Fixed

* [Fixes #799 by testing logged messages against "null or whitespace" instead of "null or empty"](https://github.com/microsoft/testfx/pull/892)
* [Fixed a bug in `ITestDataSource` data deserialization](https://github.com/microsoft/testfx/pull/864)
* [Fixed DataSource deserialization.](https://github.com/microsoft/testfx/pull/859)
* [Fixed a serialization issue with DataRows.](https://github.com/microsoft/testfx/pull/847)

### Artifacts

* MSTest.TestFramework: [2.2.5](https://www.nuget.org/packages/MSTest.TestFramework/2.2.5)
* MSTest.TestAdapter: [2.2.5](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.5)

## <a name="2.2.4" />[2.2.4] - 2021-05-25

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/0b95a26282eae17f896d732381e5c77b9a603382...v2.2.4)

### Artifacts

* MSTest.TestFramework: [2.2.4](https://www.nuget.org/packages/MSTest.TestFramework/2.2.4)
* MSTest.TestAdapter: [2.2.4](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.4)

## <a name="2.2.4-preview-20210331-02" />[2.2.4-preview-20210331-02] - 2021-04-02

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.3...v2.2.4-preview-20210331-02)

### Added

* [Added basic WinUI3 support.](https://github.com/microsoft/testfx/pull/782)

### Changed

* [Some code clean-up and refactoring](https://github.com/microsoft/testfx/pull/800)

### Fixed

* [Fix StackOverflowException in StringAssert.DoesNotMatch](https://github.com/microsoft/testfx/pull/806)
* [MSBuild scripts fixed.](https://github.com/microsoft/testfx/pull/801)

### Artifacts

* MSTest.TestFramework: [2.2.4-preview-20210331-02](https://www.nuget.org/packages/MSTest.TestFramework/2.2.4-preview-20210331-02)
* MSTest.TestAdapter: [2.2.4-preview-20210331-02](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.4-preview-20210331-02)

## <a name="2.2.3" />[2.2.3] - 2021-03-16

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.2...v2.2.3)

### Added

* [Added missing library to the NuGet package.](https://github.com/microsoft/testfx/pull/798)

### Artifacts

* MSTest.TestFramework: [2.2.3](https://www.nuget.org/packages/MSTest.TestFramework/2.2.3)
* MSTest.TestAdapter: [2.2.3](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.3)

## <a name="2.2.2" />[2.2.2] - 2021-03-15

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.1...v2.2.2)

### Added

* [Missing assembly added to TestAdapter package](https://github.com/microsoft/testfx/pull/796)

### Fixed

* [NuGet package dependencies fixed.](https://github.com/microsoft/testfx/pull/797)
* [Unit test display name issue fixed.](https://github.com/microsoft/testfx/pull/795)
* [Fix infinite iteration in Matches method](https://github.com/microsoft/testfx/pull/792)

### Artifacts

* MSTest.TestFramework: [2.2.2](https://www.nuget.org/packages/MSTest.TestFramework/2.2.2)
* MSTest.TestAdapter: [2.2.2](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.2)

## <a name="2.2.1" />[2.2.1] - 2021-03-01

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.0-preview-20210115-03...v2.2.1)

### Added

* [Merge parameters safely](https://github.com/microsoft/testfx/pull/778)
* [Merge settings safely](https://github.com/microsoft/testfx/pull/771)

### Changed

* [Prepend MSTest to log messages, without formatting](https://github.com/microsoft/testfx/pull/785)
* [TestPlatform version updated to v16.9.1](https://github.com/microsoft/testfx/pull/784)
* [Forward logs to EqtTrace on netcore](https://github.com/microsoft/testfx/pull/776)
* [ManagedNames impl. refactored.](https://github.com/microsoft/testfx/pull/766)

### Fixed

* [Fixed concurrency issues in the TypeCache class.](https://github.com/microsoft/testfx/pull/758)

### Removed

* [WIP: Remove .txt extension from LICENSE file](https://github.com/microsoft/testfx/pull/781)

### Artifacts

* MSTest.TestFramework: [2.2.1](https://www.nuget.org/packages/MSTest.TestFramework/2.2.1)
* MSTest.TestAdapter: [2.2.1](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.1)

## <a name="2.2.0-preview-20210115-03" />[2.2.0-preview-20210115-03] - 2021-01-20

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.0-preview-20201126-03...v2.2.0-preview-20210115-03)

### Changed

* [Updates](https://github.com/microsoft/testfx/pull/755)
* [Refactored `TypesToLoadAttribute` into `TestExtensionTypesAttribute`](https://github.com/microsoft/testfx/pull/754)

### Fixed

* [Fixing pdb2pdb package](https://github.com/microsoft/testfx/pull/760)
* [Fixing nugets](https://github.com/microsoft/testfx/pull/759)
* [Fixed TypesToLoadAttribute compatibility](https://github.com/microsoft/testfx/pull/753)
* [BugFix: WorkItemAttribute not extracted](https://github.com/microsoft/testfx/pull/749)
* [Pdb2Pbp path fix](https://github.com/microsoft/testfx/pull/761)

### Removed

* [Removed unnecessary whitespace](https://github.com/microsoft/testfx/pull/752)
* [Removed MyGet references from README.md](https://github.com/microsoft/testfx/pull/751)

### Artifacts

* MSTest.TestFramework: [2.2.0-preview-20210115-03](https://www.nuget.org/packages/MSTest.TestFramework/2.2.0-preview-20210115-03)
* MSTest.TestAdapter: [2.2.0-preview-20210115-03](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.0-preview-20210115-03)

## <a name="2.2.0-preview-20201126-03" />[2.2.0-preview-20201126-03] - 2020-11-26

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.2...v2.2.0-preview-20201126-03)

### Added

* [Added support for ManagedType and ManagedClass](https://github.com/microsoft/testfx/pull/737)
* [Add nullable-annotated Assert.IsNotNull](https://github.com/microsoft/testfx/pull/744)
* [Add support to treat class/assembly warnings as errors](https://github.com/microsoft/testfx/pull/717)
* [Added StringComparison to StringAssert Contains(), EndsWith(), and StartsWith()](https://github.com/microsoft/testfx/pull/691)

### Changed

* [Replaced deprecated certificate](https://github.com/microsoft/testfx/pull/742)
* [Assert.IsTrue() & False() to handle nullable bools](https://github.com/microsoft/testfx/pull/690)

### Fixed

* [Fix XML doc comments (code -> c)](https://github.com/microsoft/testfx/pull/730)
* [Fix null ref bug when base class cleanup fails when there is no derived class cleanup method](https://github.com/microsoft/testfx/pull/716)

### Removed

* [Load specific types from adapter](https://github.com/microsoft/testfx/pull/746)

### Artifacts

* MSTest.TestFramework: [2.2.0-preview-20201126-03](https://www.nuget.org/packages/MSTest.TestFramework/2.2.0-preview-20201126-03)
* MSTest.TestAdapter: [2.2.0-preview-20201126-03](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.0-preview-20201126-03)

## <a name="2.1.2" />[2.1.2] - 2020-06-08

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.1...v2.1.2)

### Changed

* [Set IsClassInitializeExecuted=true after base class init to avoid repeated class init calls](https://github.com/microsoft/testfx/pull/705)
* [Change NuGet package to use `None` ItemGroup to copy files to output directory](https://github.com/microsoft/testfx/pull/703)
* [Improve CollectionAssert.Are*Equal docs (#711)](https://github.com/microsoft/testfx/pull/712)
* [enhance documentation on when the TestCleanup is executed](https://github.com/microsoft/testfx/pull/709)
* [Make AssemblyCleanup/ClassCleanup execute even if Initialize fails.](https://github.com/microsoft/testfx/pull/696)

### Fixed

* [Fixed documentation for the TestMethodAttribute](https://github.com/microsoft/testfx/pull/715)

### Artifacts

* MSTest.TestFramework: [2.1.2](https://www.nuget.org/packages/MSTest.TestFramework/2.1.2)
* MSTest.TestAdapter: [2.1.2](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.2)

## <a name="2.1.1" />[2.1.1] - 2020-04-01

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.0...v2.1.1)

### Added

* [Add FSharp E2E test](https://github.com/microsoft/testfx/pull/683)
* [Create Write() in TestContext](https://github.com/microsoft/testfx/pull/686)

### Changed

* [switch arguments for expected and actual in Assert.AreEquals in multiple tests](https://github.com/microsoft/testfx/pull/685)

### Fixed

* [fix blog link](https://github.com/microsoft/testfx/pull/677)
* [Spelling / conventions and grammar fixes](https://github.com/microsoft/testfx/pull/688)

### Removed

* [remove unused usings](https://github.com/microsoft/testfx/pull/694)

### Artifacts

* MSTest.TestFramework: [2.1.1](https://www.nuget.org/packages/MSTest.TestFramework/2.1.1)
* MSTest.TestAdapter: [2.1.1](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.1)

## <a name="2.1.0" />[2.1.0] - 2020-02-03

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.0-beta2...v2.1.0)

### Changed

* [Record test start/end events for data driven tests](https://github.com/microsoft/testfx/pull/631)

### Fixed

* [Fix parameters in tests](https://github.com/microsoft/testfx/pull/680)
* [Fix bugs in parent class init/cleanup logic](https://github.com/microsoft/testfx/pull/660)

### Artifacts

* MSTest.TestFramework: [2.1.0](https://www.nuget.org/packages/MSTest.TestFramework/2.1.0)
* MSTest.TestAdapter: [2.1.0](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.0)

## <a name="2.1.0-beta2" />[2.1.0-beta2] - 2019-12-18

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v2.1.0-beta...v2.1.0-beta2)

### Changed

* [Friendly test names](https://github.com/microsoft/testfx/pull/466)

### Artifacts

* MSTest.TestFramework: [2.1.0-beta2](https://www.nuget.org/packages/MSTest.TestFramework/2.1.0-beta2)
* MSTest.TestAdapter: [2.1.0-beta2](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.0-beta2)

## <a name="2.1.0-beta" />[2.1.0-beta] - 2019-11-28

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v2.0.0...v2.1.0-beta)

### Fixed

* [Fix incompatibility between multiple versions of mstest adapter present in a solution](https://github.com/Microsoft/testfx/pull/659)
* [Build script fix to work with VS2019](https://github.com/Microsoft/testfx/pull/641)

### Artifacts

* MSTest.TestFramework: [2.1.0-beta](https://www.nuget.org/packages/MSTest.TestFramework/2.1.0-beta)
* MSTest.TestAdapter: [2.1.0-beta](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.0-beta)

## <a name="2.0.0" />[2.0.0] 2019-09-03

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v2.0.0-beta4...v2.0.0)

### Added

* [Implemented 'AddResultFile' for NetCore TestContext](https://github.com/Microsoft/testfx/pull/609)
* [Implemented Initialize Inheritance for ClassInitialize attribute](https://github.com/Microsoft/testfx/issues/577)

### Changed

* [Apply TestCategory from derived class on inherited test methods](https://github.com/Microsoft/testfx/issues/513)
* [Setting MapNotRunnableToFailed to true by default](https://github.com/Microsoft/testfx/issues/610)
* [Datarow tests - support methods with optional parameters](https://github.com/Microsoft/testfx/pull/604)

### Fixed

* [Fixed IsNotInstanceOfType failing when objected being asserted on is null](https://github.com/Microsoft/testfx/issues/622)

### Artifacts

* MSTest.TestFramework: [2.0.0](https://www.nuget.org/packages/MSTest.TestFramework/2.0.0)
* MSTest.TestAdapter: [2.0.0](https://www.nuget.org/packages/MSTest.TestAdapter/2.0.0)

## <a name="2.0.0-beta4" />[2.0.0-beta4] - 2019-04-10

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/2.0.0-beta2...v2.0.0-beta4)

### Changed

* [Deployment Item support in .NET Core](https://github.com/Microsoft/testfx/pull/565)
* [Support for CancellationTokenSource in TestContext to help in timeout scenario](https://github.com/Microsoft/testfx/pull/585)
* [Correcting error message when DynamicData doesn't have any data](https://github.com/Microsoft/testfx/issues/443)

### Artifacts

* MSTest.TestFramework: [2.0.0-beta4](https://www.nuget.org/packages/MSTest.TestFramework/2.0.0-beta4)
* MSTest.TestAdapter: [2.0.0-beta4](https://www.nuget.org/packages/MSTest.TestAdapter/2.0.0-beta4)

## <a name="2.0.0-beta2" />[2.0.0-beta2] - 2019-02-15

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/1.4.0...2.0.0-beta2)

### Changed

* (BREAKING CHANGE) [TestContext Properties type fixed to be IDictionary](https://github.com/Microsoft/testfx/pull/563)
* [Base class data rows should not be executed](https://github.com/Microsoft/testfx/pull/546)
* [Setting option for marking not runnable tests as failed](https://github.com/Microsoft/testfx/pull/524)

### Artifacts

* MSTest.TestFramework: [2.0.0-beta2](https://www.nuget.org/packages/MSTest.TestFramework/2.0.0-beta2)
* MSTest.TestAdapter: [2.0.0-beta2](https://www.nuget.org/packages/MSTest.TestAdapter/2.0.0-beta2)

## <a name="1.4.0" />[1.4.0] - 2018-11-26

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/1.4.0-beta...1.4.0)

### Added

* [Added new runsettings configuration to deploy all files from test source location i.e. DeployTestSourceDependencies](https://github.com/Microsoft/testfx/pull/391) [enhancement]

### Changed

* (BREAKING CHANGE) [Description, WorkItem, CssIteration, CssProjectStructure Attributes will not be treated as traits](https://github.com/Microsoft/testfx/pull/482)
* [Allow test methods returning Task to run without suppling async keyword](https://github.com/Microsoft/testfx/pull/510) [Contributed by [Paul Spangler](https://github.com/spanglerco)]

### Removed

* [Removed Test discovery warnings in Test Output pane](https://github.com/Microsoft/testfx/pull/480) [Contributed by [Carlos Parra](https://github.com/parrainc)]

### Artifacts

* MSTest.TestFramework: [1.4.0](https://www.nuget.org/packages/MSTest.TestFramework/1.4.0)
* MSTest.TestAdapter: [1.4.0](https://www.nuget.org/packages/MSTest.TestAdapter/1.4.0)

## <a name="1.4.0-beta" />[1.4.0-beta] 2018-10-17

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/1.3.2...1.4.0-beta)

### Added

* [Adding appropriate error message for TestMethods expecting parameters but parameters not provided](https://github.com/Microsoft/testfx/pull/457)

### Changed

* [Enabling Tfs properties in test context object](https://github.com/Microsoft/testfx/pull/472) [enhancement]
* [Description, WorkItem, CssIteration, CssProjectStructure Attributes should not be treated as traits](https://github.com/Microsoft/testfx/pull/482)

### Artifacts

* MSTest.TestFramework: [1.4.0-beta](https://www.nuget.org/packages/MSTest.TestFramework/1.4.0-beta)
* MSTest.TestAdapter: [1.4.0-beta](https://www.nuget.org/packages/MSTest.TestAdapter/1.4.0-beta)

## <a name="1.3.2" />[1.3.2] - 2018-06-06

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.3.1...v1.3.2)

### Changed

* [Hierarchical view support for data-driven tests](https://github.com/Microsoft/testfx/pull/417)

### Artifacts

* MSTest.TestFramework: [1.3.2](https://www.nuget.org/packages/MSTest.TestFramework/1.3.2)
* MSTest.TestAdapter: [1.3.2](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.2)

## <a name="1.3.1" />[1.3.1] - 2018-05-25

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.3.0...v1.3.1)

### Changed

* [AppDomain creation should honor runsettings](https://github.com/Microsoft/testfx/pull/427)
* [Don't delete resource folder while clean/rebuild](https://github.com/Microsoft/testfx/pull/424)

### Artifacts

* MSTest.TestFramework: [1.3.1](https://www.nuget.org/packages/MSTest.TestFramework/1.3.1)
* MSTest.TestAdapter: [1.3.1](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.1)

## <a name="1.3.0" />[1.3.0] - 2018-05-11

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.1...v1.3.0)

### Changed

* [Run Class Cleanup in sync with Class Initialize](https://github.com/Microsoft/testfx/pull/372)
* [TestTimeout configurable via RunSettings](https://github.com/Microsoft/testfx/pull/403) [enhancement]
* [Consistent behavior of GenericParameterHelper's while running and debugging](https://github.com/Microsoft/testfx/issues/362) [Contributed by [walterlv](https://github.com/walterlv)]
* [Customize display name for DynamicDataAttribute](https://github.com/Microsoft/testfx/pull/373) [Contributed by [Brad Stoney](https://github.com/bstoney)] [enhancement]

### Fixed

* [Fix incompatibility between multiple versions of mstest adapter present in a solution](https://github.com/Microsoft/testfx/pull/404)
* [Fix multiple results not returning for custom TestMethod](https://github.com/Microsoft/testfx/pull/363) [Contributed by [Cédric Bignon](https://github.com/bignoncedric)]
* [Fix to show right error message on assembly load exception during test run](https://github.com/Microsoft/testfx/issues/395)

### Artifacts

* MSTest.TestFramework: [1.3.0](https://www.nuget.org/packages/MSTest.TestFramework/1.3.0)
* MSTest.TestAdapter: [1.3.0](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.0)

## <a name="1.3.0-beta2" />[1.3.0-beta2] - 2018-01-15

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.0...v1.3.0-beta2)

### Added

* [Add information about which assembly failed to discover test](https://github.com/Microsoft/testfx/pull/299) [Contributed by [Andrey Kurdyumov](https://github.com/kant2002)]
* [Adding warning message for vsmdi file](https://github.com/Microsoft/testfx/issues/61)
* [Add missing Microsoft.Internal.TestPlatform.ObjectModel](https://github.com/Microsoft/testfx/pull/301) [Contributed by [Andrey Kurdyumov](https://github.com/kant2002)]

### Changed

* [Update File version for adapter and framework dlls](https://github.com/Microsoft/testfx/issues/268)
* [In-Assembly Parallel Feature](https://github.com/Microsoft/testfx/pull/296)

### Fixed

* [Fixing Key collision for test run parameters](https://github.com/Microsoft/testfx/issues/298)
* [Fix for csv x64 scenario](https://github.com/Microsoft/testfx/issues/325)
* [DataRow DisplayName Fix in .Net framework](https://github.com/Microsoft/testfx/issues/284)

### Artifacts

* MSTest.TestFramework: [1.3.0-beta2](https://www.nuget.org/packages/MSTest.TestFramework/1.3.0-beta2)
* MSTest.TestAdapter: [1.3.0-beta2](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.0-beta2)

## <a name="1.2.1" />[1.2.1] - 2018-04-05

### Changed

* [Don't call Class Cleanup if Class Init not called](https://github.com/Microsoft/testfx/pull/372)

### Removed

* [Fixing Key collision for test run parameters](https://github.com/Microsoft/testfx/pull/328)
* [Fix masking assembly load failure error message](https://github.com/Microsoft/testfx/pull/382)
* [Fix UWP tests discovery](https://github.com/Microsoft/testfx/pull/332)

### Artifacts

* MSTest.TestFramework: [1.2.1](https://www.nuget.org/packages/MSTest.TestFramework/1.2.1)
* MSTest.TestAdapter: [1.2.1](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.1)

## <a name="1.2.0" />[1.2.0] - 2017-10-11

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.0-beta3...v1.2.0)

### Added

* [Adding support for DiaNavigation in UWP test adapter](https://github.com/Microsoft/testfx/pull/258)
* [Adding filtering support at discovery](https://github.com/Microsoft/testfx/pull/271)
* [DataSourceAttribute Implementation](https://github.com/Microsoft/testfx/pull/238)

### Changed

* [Improve handling of Assert.Inconclusive](https://github.com/Microsoft/testfx/pull/277)
* [Arguments order for ArgumentException](https://github.com/Microsoft/testfx/pull/262)

### Artifacts

* MSTest.TestFramework: [1.2.0](https://www.nuget.org/packages/MSTest.TestFramework/1.2.0)
* MSTest.TestAdapter: [1.2.0](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.0)

## <a name="1.2.0-beta3" />[1.2.0-beta3] - 2017-08-09

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.0-beta...v1.2.0-beta3)

### Added

* [Added Mapping for TestOutcome.None to the UnitTestOutcome Enum to achieve NotExecuted behaviour in VSTS](https://github.com/Microsoft/testfx/issues/217) [Contributed By [Irguzhav](https://github.com/irguzhav)] [enhancement]

### Changed

* [All the Assert constructor's has been made private and the classes sealed](https://github.com/Microsoft/testfx/issues/223)
* [Adapter is not sending TestCategory traits in Testcase object to Testhost](https://github.com/Microsoft/testfx/issues/189)
* [TestMethod failures masked by TestCleanUp exceptions](https://github.com/Microsoft/testfx/issues/58)
* [Multiple copies added for same test on running multiple times in IntelliTest](https://github.com/Microsoft/testfx/issues/92)

### Artifacts

* MSTest.TestFramework: [1.2.0-beta3](https://www.nuget.org/packages/MSTest.TestFramework/1.2.0-beta3)
* MSTest.TestAdapter: [1.2.0-beta3](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.0-beta3)

## <a name="1.2.0-beta" />[1.2.0-beta] - 2017-06-29

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.18...v1.2.0-beta)

### Changed

* [Support for Dynamic Data Attribute](https://github.com/Microsoft/testfx/issues/141) [extensibility]
* [Make discovering test methods from base classes defined in another assembly the default](https://github.com/Microsoft/testfx/issues/164) [enhancement]
* [CollectSourceInformation awareness to query source information](https://github.com/Microsoft/testfx/issues/119) [enhancement]

### Artifacts

* MSTest.TestFramework: [1.2.0-beta](https://www.nuget.org/packages/MSTest.TestFramework/1.2.0-beta)
* MSTest.TestAdapter: [1.2.0-beta](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.0-beta)

## <a name="1.1.18" />[1.1.18] - 2017-06-01

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.17...v1.1.18)

### Changed

* [Ability to provide a reason for Ignored tests](https://github.com/Microsoft/testfx/issues/126) [enhancement]
* [VB unit test project templates that ship in VS 2017 do not reference MSTest V2 nuget packages](https://github.com/Microsoft/testfx/issues/132) [enhancement]
* [Assert.IsInstanceOf passes on value null](https://github.com/Microsoft/testfx/issues/178) [Contributed By [LarsCelie](https://github.com/larscelie)]
* [Test methods in a base class defined in a different assembly are not navigable in Test Explorer](https://github.com/Microsoft/testfx/issues/163) [Contributed By [ajryan](https://github.com/ajryan)]
* [Enable MSTest framework based tests targeting .NET Core to be run and debugged from within VSCode](https://github.com/Microsoft/testfx/issues/182)
* [Web project templates that ship in VS 2017 do not reference MSTest V2 nuget packages](https://github.com/Microsoft/testfx/issues/167)

### Artifacts

* MSTest.TestFramework: [1.1.18](https://www.nuget.org/packages/MSTest.TestFramework/1.1.18)
* MSTest.TestAdapter: [1.1.18](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.18)

## <a name="1.1.17" />[1.1.17] - 2017-04-21

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.14...v1.1.17)

### Changed

* [Console.WriteLine support for .NetCore Projects](https://github.com/Microsoft/testfx/issues/18) [enhancement]
* [Inheritance support for base classes that resides in different assemblies](https://github.com/Microsoft/testfx/issues/23) [enhancement]
* [TestContext.Writeline does not output messages](https://github.com/Microsoft/testfx/issues/120)
* [Logger.LogMessage logs a message mutliple times](https://github.com/Microsoft/testfx/issues/114)
* [TestContext.CurrentTestOutcome is always InProgress in the TestCleanup method](https://github.com/Microsoft/testfx/issues/89)
* [An inconclusive in a test initialize fails the test if it has an ExpectedException](https://github.com/Microsoft/testfx/issues/136)

### Artifacts

* MSTest.TestFramework: [1.1.17](https://www.nuget.org/packages/MSTest.TestFramework/1.1.17)
* MSTest.TestAdapter: [1.1.17](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.17)

## <a name="1.1.14" />[1.1.14] - 2017-03-31

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.13...v1.1.14)

### Changed

* [Ability to add custom assertions](https://github.com/Microsoft/testfx/issues/116) [enhancement]

### Fixed

* [Problems with null in DataRow](https://github.com/Microsoft/testfx/issues/70)

### Artifacts

* MSTest.TestFramework: [1.1.14](https://www.nuget.org/packages/MSTest.TestFramework/1.1.14)
* MSTest.TestAdapter: [1.1.14](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.14)

## <a name="1.1.13" />[1.1.13] - 2017-03-10

This is also the first release from GitHub and with source code building against Dev15 tooling.

### Changed

* [Tests with Deployment Item do not run](https://github.com/Microsoft/testfx/issues/91)
* [Run tests fail intermittently with a disconnected from server exception](https://github.com/Microsoft/testfx/issues/28)
* [Templates and Wizards vsix should be built with RC3 tooling](https://github.com/Microsoft/testfx/issues/77)

### Artifacts

* MSTest.TestFramework: [1.1.13](https://www.nuget.org/packages/MSTest.TestFramework/1.1.13)
* MSTest.TestAdapter: [1.1.13](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.13)

## <a name="1.1.11" />[1.1.11] - 2017-02-17

Initial release.

### Artifacts

* MSTest.TestFramework: [1.1.11](https://www.nuget.org/packages/MSTest.TestFramework/1.1.11)
* MSTest.TestAdapter: [1.1.11](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.11)
