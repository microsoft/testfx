# Releases

# 2.3.0-preview-20220810-02 (August 2022)

- [x] [Fixed issues with SDK style projects.](https://github.com/microsoft/testfx/pull/1171)

- [x] [Assert failure messages](https://github.com/microsoft/testfx/pull/1172)

- [x] [Better messages for XXXInitialize and XXXCleanup](https://github.com/microsoft/testfx/pull/1147)

- [x] [TestResults folder names are now cross platform compatible, as per #678](https://github.com/microsoft/testfx/pull/1119)

- [x] [Ensure assertions do not fail with FormatException](https://github.com/microsoft/testfx/pull/1126)

- [x] [Prevent format exceptions when parameters array is empty](https://github.com/microsoft/testfx/pull/1124)

- [x] [Adding Microsoft SECURITY.MD](https://github.com/microsoft/testfx/pull/1109)

- [x] [[main] Update dependencies from dotnet/arcade](https://github.com/microsoft/testfx/pull/1098)

- [x] [Remove unused classes](https://github.com/microsoft/testfx/pull/1089)

- [x] [Add whitespace editorconfig and run dotnet format whitespace](https://github.com/microsoft/testfx/pull/1090)

- [x] Bumped up version to 2.3.0

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.10...v2.3.0-preview-20220810-02)

### Builds
* MSTest: [2.3.0-preview-20220810-02](https://www.nuget.org/packages/MSTest/2.3.0-preview-20220810-02)
* MSTest.TestFramework: [2.3.0-preview-20220810-02](https://www.nuget.org/packages/MSTest.TestFramework/2.3.0-preview-20220810-02)
* MSTest.TestAdapter: [2.3.0-preview-20220810-02](https://www.nuget.org/packages/MSTest.TestAdapter/2.3.0-preview-20220810-02)

# 2.2.10 (April 2022)

- [x] [Run dotnet format whitespace](https://github.com/microsoft/testfx/pull/1085)

- [x] [Update dependencies from https://github.com/dotnet/arcade build 20220425.6](https://github.com/microsoft/testfx/pull/1087)

- [x] [Added more fail paths for data serialization.](https://github.com/microsoft/testfx/pull/1084)

- [x] [Added MSTest meta-package.](https://github.com/microsoft/testfx/pull/1076)

- [x] [Test execution bugs in specific TFMs addressed.](https://github.com/microsoft/testfx/pull/1071)

- [x] [Static init of StackTraceHelper.typesToBeExcluded](https://github.com/microsoft/testfx/pull/1055)

- [x] [Update description of the Nuget packages](https://github.com/microsoft/testfx/pull/981)

- [x] [Converted files to utf-8 so they can be diffed.](https://github.com/microsoft/testfx/pull/1070)


A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.10-preview-20220414-01...v2.2.10)

### Builds
* MSTest: [2.2.10](https://www.nuget.org/packages/MSTest/2.2.10)
* MSTest.TestFramework: [2.2.10](https://www.nuget.org/packages/MSTest.TestFramework/2.2.10)
* MSTest.TestAdapter: [2.2.10](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.10)

# 2.2.10-preview-20220414-01 (April 2022)
- [x] [Fix write conflicts in parallel output](https://github.com/microsoft/testfx/pull/1068)
- [x] [Fixed test run executable files.](https://github.com/microsoft/testfx/pull/1064)
- [x] [[UITestMethod] should invoke test method with null](https://github.com/microsoft/testfx/pull/1045)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.9...v2.2.10-preview-20220414-01)

### Builds
* MSTest.TestFramework: [2.2.10-preview-20220414-01](https://www.nuget.org/packages/MSTest.TestFramework/2.2.10-preview-20220414-01)
* MSTest.TestAdapter: [2.2.10-preview-20220414-01](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.10-preview-20220414-01)

# 2.2.9 (April 2022)

## Parallel output

> 🙇 Shout out to @SimonCropp, for bringing this functionality to XUnit in his https://github.com/SimonCropp/XunitContext project. And being an inspiration for implementing this.

MSTest 2.2.9 captures all Console output and attaches it to the correct test, even if you are running tests in parallel. This output is captured from your test code as well as from the tested code. And it requires no special setup. 

### Before
In 2.2.8, test output is scattered among tests, in our example, one unlucky test gets all the output of other tests just mixed together:

![image](https://user-images.githubusercontent.com/5735905/162252520-0572d932-c798-4b7e-8961-44f39b5a32b9.png)

### After

With 2.2.9, each output is correctly attached to the test that produced it:

![image](https://user-images.githubusercontent.com/5735905/162252738-2dae4ff3-d7bf-473a-9304-66cf25510a89.png)
![image](https://user-images.githubusercontent.com/5735905/162252762-4304b9c0-1e60-4089-83e3-e8f341cb9329.png)

Also notice that we are also capturing debug, trace and error. And we are not awaiting the FastChild method, and the output is still assigned correctly.  [Souce code.](https://gist.github.com/nohwnd/2936753d94301d7991059660d1d63a8a)

#### Limitations
Due to the way that class and assembly initialize, and cleanup are invoked, their output will end up in the first test that run (or last for cleanup). This is unfortunately not easily fixable.

### Builds
* MSTest.TestFramework: [2.2.9](https://www.nuget.org/packages/MSTest.TestFramework/2.2.9)
* MSTest.TestAdapter: [2.2.9](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.9)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.8...v2.2.9)

# 2.2.8 (November 2021)
- [x] Dependency version updates.
- [x] [Added internal versioning](https://github.com/microsoft/testfx/pull/1012)
- [x] [Fixed .nuspec files to mitigate NU5050 error.](https://github.com/microsoft/testfx/pull/1011)
- [x] [Updated to WindowsAppSDK 1.0.0 GA](https://github.com/microsoft/testfx/pull/1009)
- [x] [Downgrade uwp](https://github.com/microsoft/testfx/pull/1008)
- [x] [Add DoesNotReturnIf to Assert.IsTrue/Assert.IsFalse](https://github.com/microsoft/testfx/pull/1005)
- [x] [Implement Class Cleanup Lifecycle selection](https://github.com/microsoft/testfx/pull/968)
- [x] [Fix concurrent issues in DataSerializationHelper](https://github.com/microsoft/testfx/pull/998)
- [x] [Fix for incorrect Microsoft.TestPlatform.AdapterUtilities.dll for net45 target (#980)](https://github.com/microsoft/testfx/pull/988)
- [x] [Updated to WindowsAppSDK 1.0.0-preview1](https://github.com/microsoft/testfx/pull/985)
- [x] [CVE-2017-0247 fixed](https://github.com/microsoft/testfx/pull/976)
- [x] [Added 500ms overhead to parallel execution tests.](https://github.com/microsoft/testfx/pull/962)
- [x] [Cherry-picking the changes from 2.2.7](https://github.com/microsoft/testfx/pull/958)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.7...v2.2.8)

### Builds
* MSTest.TestFramework: [2.2.8](https://www.nuget.org/packages/MSTest.TestFramework/2.2.8)
* MSTest.TestAdapter: [2.2.8](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.8)

# 2.2.7 (September 2021)

- [x] [Fixed missing strong-name and Authenticode signatures](https://github.com/microsoft/testfx/pull/956)
- [x] [Resolve dependencies from GAC](https://github.com/microsoft/testfx/pull/951)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.6...v2.2.7)

### Builds
* MSTest.TestFramework: [2.2.7](https://www.nuget.org/packages/MSTest.TestFramework/2.2.7)
* MSTest.TestAdapter: [2.2.7](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.7)

# 2.2.6 (August 2021)
- [x] Allow opting-out of ITestDataSource test discovery.
- [x] [Enable internal testclass discovery (#937)](https://github.com/microsoft/testfx/pull/944)
- [x] [Fix DateTime looses significant digits in DynamicData (#875)](https://github.com/microsoft/testfx/pull/907)


A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.5...v2.2.6)

### Builds
* MSTest.TestFramework: [2.2.6](https://www.nuget.org/packages/MSTest.TestFramework/2.2.6)
* MSTest.TestAdapter: [2.2.6](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.6)

# 2.2.5 (June 2021)

- [x] [Fixes #799 by testing logged messages against "null or whitespace" instead of "null or empty"](https://github.com/microsoft/testfx/pull/892)
- [x] [Added missing framework references for WinUI](https://github.com/microsoft/testfx/pull/890)
- [x] [Upgraded winui to 0.8.0](https://github.com/microsoft/testfx/pull/888)
- [x] [Fixed a bug in `ITestDataSource` data deserialization](https://github.com/microsoft/testfx/pull/864)
- [x] [Fixed DataSource deserialization.](https://github.com/microsoft/testfx/pull/859)
- [x] [Fixed a serialization issue with DataRows.](https://github.com/microsoft/testfx/pull/847)
- [x] [Replaced license file with an expression.](https://github.com/microsoft/testfx/pull/846)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.4...v2.2.5)

### Builds
* MSTest.TestFramework: [2.2.5](https://www.nuget.org/packages/MSTest.TestFramework/2.2.5)
* MSTest.TestAdapter: [2.2.5](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.5)

# 2.2.4 (May 2021)
A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/0b95a26282eae17f896d732381e5c77b9a603382...v2.2.4)

### Builds
* MSTest.TestFramework: [2.2.4](https://www.nuget.org/packages/MSTest.TestFramework/2.2.4)
* MSTest.TestAdapter: [2.2.4](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.4)

# 2.2.4-preview-20210331-02 (March 2021)
- [x] [Fix StackOverflowException in StringAssert.DoesNotMatch](https://github.com/microsoft/testfx/pull/806)
- [x] [Added basic WinUI3 support.](https://github.com/microsoft/testfx/pull/782)
- [x] [MSBuild scripts fixed.](https://github.com/microsoft/testfx/pull/801)
- [x] [Some code clean-up and refactoring](https://github.com/microsoft/testfx/pull/800)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.3...v2.2.4-preview-20210331-02)

### Builds
* MSTest.TestFramework: [2.2.4-preview-20210331-02](https://www.nuget.org/packages/MSTest.TestFramework/2.2.4-preview-20210331-02)
* MSTest.TestAdapter: [2.2.4-preview-20210331-02](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.4-preview-20210331-02)

# 2.2.3 (March 2021)
- [x] [Added missing library to the NuGet package.](https://github.com/microsoft/testfx/pull/798)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.2...v2.2.3)

### Builds
* MSTest.TestFramework: [2.2.3](https://www.nuget.org/packages/MSTest.TestFramework/2.2.3)
* MSTest.TestAdapter: [2.2.3](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.3)

# 2.2.2 (March 2021)
- [x] [NuGet package dependencies fixed.](https://github.com/microsoft/testfx/pull/797)
- [x] [Missing assembly added to TestAdapter package](https://github.com/microsoft/testfx/pull/796)
- [x] [Unit test display name issue fixed.](https://github.com/microsoft/testfx/pull/795)
- [x] [Fix infinite iteration in Matches method](https://github.com/microsoft/testfx/pull/792)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.1...v2.2.2)

### Builds
* MSTest.TestFramework: [2.2.2](https://www.nuget.org/packages/MSTest.TestFramework/2.2.2)
* MSTest.TestAdapter: [2.2.2](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.2)

# 2.2.1 (March 2021)
- [x] [Prepend MSTest to log messages, without formatting](https://github.com/microsoft/testfx/pull/785)
- [x] [TestPlatform version updated to v16.9.1](https://github.com/microsoft/testfx/pull/784)
- [x] [WIP: Remove .txt extension from LICENSE file](https://github.com/microsoft/testfx/pull/781)
- [x] [Merge parameters safely](https://github.com/microsoft/testfx/pull/778)
- [x] [Forward logs to EqtTrace on netcore](https://github.com/microsoft/testfx/pull/776)
- [x] [Merge settings safely](https://github.com/microsoft/testfx/pull/771)
- [x] [ManagedNames impl. refactored.](https://github.com/microsoft/testfx/pull/766)
- [x] [Fixed concurrency issues in the TypeCache class.](https://github.com/microsoft/testfx/pull/758)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.0-preview-20210115-03...v2.2.1)

### Builds
* MSTest.TestFramework: [2.2.1](https://www.nuget.org/packages/MSTest.TestFramework/2.2.1)
* MSTest.TestAdapter: [2.2.1](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.1)

# 2.2.0-preview-20210115-03 (January 2021)
- [x] [Pdb2Pbp path fix](https://github.com/microsoft/testfx/pull/761)
- [x] [Fixing pdb2pdb package](https://github.com/microsoft/testfx/pull/760)
- [x] [Fixing nugets](https://github.com/microsoft/testfx/pull/759)
- [x] [Updates](https://github.com/microsoft/testfx/pull/755)
- [x] [Refactored `TypesToLoadAttribute` into `TestExtensionTypesAttribute`](https://github.com/microsoft/testfx/pull/754)
- [x] [Fixed TypesToLoadAttribute compatibility](https://github.com/microsoft/testfx/pull/753)
- [x] [BugFix: WorkItemAttribute not extracted ](https://github.com/microsoft/testfx/pull/749)
- [x] [Removed unnecessary whitespace](https://github.com/microsoft/testfx/pull/752)
- [x] [Removed MyGet references from README.md](https://github.com/microsoft/testfx/pull/751)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.2.0-preview-20201126-03...v2.2.0-preview-20210115-03)

### Builds
* MSTest.TestFramework: [2.2.0-preview-20210115-03](https://www.nuget.org/packages/MSTest.TestFramework/2.2.0-preview-20210115-03)
* MSTest.TestAdapter: [2.2.0-preview-20210115-03](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.0-preview-20210115-03)

# 2.2.0-preview-20201126-03 (November 2020)
- [x] [Load specific types from adapter](https://github.com/microsoft/testfx/pull/746)
- [x] [Added support for ManagedType and ManagedClass](https://github.com/microsoft/testfx/pull/737)
- [x] [Add nullable-annotated Assert.IsNotNull](https://github.com/microsoft/testfx/pull/744)
- [x] [Replaced deprecated certificate](https://github.com/microsoft/testfx/pull/742)
- [x] [Added StringComparison to StringAssert Contains(), EndsWith(), and StartsWith()](https://github.com/microsoft/testfx/pull/691)
- [x] [Assert.IsTrue() & False() to handle nullable bools](https://github.com/microsoft/testfx/pull/690)
- [x] [Add support to treat class/assembly warnings as errors](https://github.com/microsoft/testfx/pull/717)
- [x] [Fix XML doc comments (code -> c)](https://github.com/microsoft/testfx/pull/730)
- [x] [Fix null ref bug when base class cleanup fails when there is no derived class cleanup method](https://github.com/microsoft/testfx/pull/716)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.2...v2.2.0-preview-20201126-03)

### Builds
* MSTest.TestFramework: [2.2.0-preview-20201126-03](https://www.nuget.org/packages/MSTest.TestFramework/2.2.0-preview-20201126-03)
* MSTest.TestAdapter: [2.2.0-preview-20201126-03](https://www.nuget.org/packages/MSTest.TestAdapter/2.2.0-preview-20201126-03)

# 2.1.2 (June 2020)
- [x] [Set IsClassInitializeExecuted=true after base class init to avoid repeated class init calls](https://github.com/microsoft/testfx/pull/705)
- [x] [enhance documentation on when the TestCleanup is executed](https://github.com/microsoft/testfx/pull/709)
- [x] [Improve CollectionAssert.Are*Equal docs (#711)](https://github.com/microsoft/testfx/pull/712)
- [x] [Fixed documentation for the TestMethodAttribute](https://github.com/microsoft/testfx/pull/715)
- [x] [Make AssemblyCleanup/ClassCleanup execute even if Initialize fails.](https://github.com/microsoft/testfx/pull/696)
- [x] [Change NuGet package to use `None` ItemGroup to copy files to output directory](https://github.com/microsoft/testfx/pull/703)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.1...v2.1.2)

### Builds
* MSTest.TestFramework: [2.1.2](https://www.nuget.org/packages/MSTest.TestFramework/2.1.2)
* MSTest.TestAdapter: [2.1.2](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.2)

# 2.1.1 (March 2020)
- [x] [Add FSharp E2E test](https://github.com/microsoft/testfx/pull/683) 
- [x] [remove unused usings](https://github.com/microsoft/testfx/pull/694)
- [x] [fix blog link](https://github.com/microsoft/testfx/pull/677)       
- [x] [Create Write() in TestContext](https://github.com/microsoft/testfx/pull/686)
- [x] [switch arguments for expected and actual in Assert.AreEquals in multiple tests](https://github.com/microsoft/testfx/pull/685)
- [x] [Spelling / conventions and grammar fixes](https://github.com/microsoft/testfx/pull/688)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.0...v2.1.1)

### Builds
* MSTest.TestFramework: [2.1.1](https://www.nuget.org/packages/MSTest.TestFramework/2.1.1)
* MSTest.TestAdapter: [2.1.1](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.1)

# 2.1.0 (January 2020)
- [x] [Fix parameters in tests](https://github.com/microsoft/testfx/pull/680)
- [x] [Fix bugs in parent class init/cleanup logic](https://github.com/microsoft/testfx/pull/660)
- [x] [Record test start/end events for data driven tests](https://github.com/microsoft/testfx/pull/631)

A list of changes since last release are available [here](https://github.com/microsoft/testfx/compare/v2.1.0-beta2...v2.1.0)

### Builds
* MSTest.TestFramework: [2.1.0](https://www.nuget.org/packages/MSTest.TestFramework/2.1.0)
* MSTest.TestAdapter: [2.1.0](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.0)

# 2.1.0-beta2 (December 2019)
 - [x] [Friendly test names](https://github.com/microsoft/testfx/pull/466)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v2.1.0-beta...v2.1.0-beta2)

### Builds
* MSTest.TestFramework: [2.1.0-beta2](https://www.nuget.org/packages/MSTest.TestFramework/2.1.0-beta2)
* MSTest.TestAdapter: [2.1.0-beta2](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.0-beta2)

# 2.1.0-beta (November 2019)
 - [x] [Fix incompatibility between multiple versions of mstest adapter present in a solution](https://github.com/Microsoft/testfx/pull/659)
 - [x] [Build script fix to work with VS2019](https://github.com/Microsoft/testfx/pull/641)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v2.0.0...v2.1.0-beta)

### Builds
* MSTest.TestFramework: [2.1.0-beta](https://www.nuget.org/packages/MSTest.TestFramework/2.1.0-beta)
* MSTest.TestAdapter: [2.1.0-beta](https://www.nuget.org/packages/MSTest.TestAdapter/2.1.0-beta)

# 2.0.0 (September 2019)
 - [x] [Implemented 'AddResultFile' for NetCore TestContext](https://github.com/Microsoft/testfx/pull/609) 
 - [x] [Datarow tests - support methods with optional parameters](https://github.com/Microsoft/testfx/pull/604)
 - [x] [Implemented Initialize Inheritance for ClassInitialize attribute](https://github.com/Microsoft/testfx/issues/577)
 - [x] [Apply TestCategory from derived class on inherited test methods](https://github.com/Microsoft/testfx/issues/513)
 - [x] [Fixed IsNotInstanceOfType failing when objected being asserted on is null](https://github.com/Microsoft/testfx/issues/622)
 - [x] [Setting MapNotRunnableToFailed to true by default](https://github.com/Microsoft/testfx/issues/610)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v2.0.0-beta4...v2.0.0) 

### Builds
* MSTest.TestFramework: [2.0.0](https://www.nuget.org/packages/MSTest.TestFramework/2.0.0)
* MSTest.TestAdapter: [2.0.0](https://www.nuget.org/packages/MSTest.TestAdapter/2.0.0)

# 2.0.0-beta4 (April 2019)
 - [x] [Deployment Item support in .NET Core](https://github.com/Microsoft/testfx/pull/565) 
 - [x] [Support for CancellationTokenSource in TestContext to help in timeout scenario](https://github.com/Microsoft/testfx/pull/585)
 - [x] [Correcting error message when DynamicData doesn't have any data](https://github.com/Microsoft/testfx/issues/443)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/2.0.0-beta2...v2.0.0-beta4) 

### Builds
* MSTest.TestFramework: [2.0.0-beta4](https://www.nuget.org/packages/MSTest.TestFramework/2.0.0-beta4)
* MSTest.TestAdapter: [2.0.0-beta4](https://www.nuget.org/packages/MSTest.TestAdapter/2.0.0-beta4)

 # 2.0.0-beta2 (February 2019)
 - [x] (BREAKING CHANGE) [TestContext Properties type fixed to be IDictionary](https://github.com/Microsoft/testfx/pull/563) 
 - [x] [Base class data rows should not be executed](https://github.com/Microsoft/testfx/pull/546)
 - [x] [Setting option for marking not runnable tests as failed](https://github.com/Microsoft/testfx/pull/524)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/1.4.0...2.0.0-beta2) 

### Builds
* MSTest.TestFramework: [2.0.0-beta2](https://www.nuget.org/packages/MSTest.TestFramework/2.0.0-beta2)
* MSTest.TestAdapter: [2.0.0-beta2](https://www.nuget.org/packages/MSTest.TestAdapter/2.0.0-beta2)

# 1.4.0 (November 2018)
 - [x] (BREAKING CHANGE) [Description, WorkItem, CssIteration, CssProjectStructure Attributes will not be treated as traits](https://github.com/Microsoft/testfx/pull/482) 
 - [x] [Added new runsettings configuration to deploy all files from test source location i.e. DeployTestSourceDependencies](https://github.com/Microsoft/testfx/pull/391) [enhancement]
 - [x] [Removed Test discovery warnings in Test Output pane](https://github.com/Microsoft/testfx/pull/480) [Contributed by [Carlos Parra](https://github.com/parrainc)]
 - [x] [Allow test methods returning Task to run without suppling async keyword](https://github.com/Microsoft/testfx/pull/510) [Contributed by [Paul Spangler](https://github.com/spanglerco)]

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/1.4.0-beta...1.4.0) 

### Builds
* MSTest.TestFramework: [1.4.0](https://www.nuget.org/packages/MSTest.TestFramework/1.4.0)
* MSTest.TestAdapter: [1.4.0](https://www.nuget.org/packages/MSTest.TestAdapter/1.4.0)

# 1.4.0-beta (October 2018)
 - [x] [Enabling Tfs properties in test context object](https://github.com/Microsoft/testfx/pull/472) [enhancement]
 - [x] [Description, WorkItem, CssIteration, CssProjectStructure Attributes should not be treated as traits](https://github.com/Microsoft/testfx/pull/482) 
 - [x] [Adding appropriate error message for TestMethods expecting parameters but parameters not provided](https://github.com/Microsoft/testfx/pull/457)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/1.3.2...1.4.0-beta) 

### Builds
* MSTest.TestFramework: [1.4.0-beta](https://www.nuget.org/packages/MSTest.TestFramework/1.4.0-beta)
* MSTest.TestAdapter: [1.4.0-beta](https://www.nuget.org/packages/MSTest.TestAdapter/1.4.0-beta)
 
# 1.3.2 (June 2018)
 - [x] [Hierarchical view support for data-driven tests ](https://github.com/Microsoft/testfx/pull/417)

A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.3.1...v1.3.2)
 
### Builds
* MSTest.TestFramework: [1.3.2](https://www.nuget.org/packages/MSTest.TestFramework/1.3.2)
* MSTest.TestAdapter: [1.3.2](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.2)
 
# 1.3.1 (May 2018)
 - [x] [AppDomain creation should honor runsettings ](https://github.com/Microsoft/testfx/pull/427)
 - [x] [Don't delete resource folder while clean/rebuild ](https://github.com/Microsoft/testfx/pull/424) 

 A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.3.0...v1.3.1)
 
### Builds
* MSTest.TestFramework: [1.3.1](https://www.nuget.org/packages/MSTest.TestFramework/1.3.1)
* MSTest.TestAdapter: [1.3.1](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.1)

# 1.3.0 (May 2018)
 - [x] [TestTimeout configurable via RunSettings](https://github.com/Microsoft/testfx/pull/403) [enhancement] 
 - [x] [Customize display name for DynamicDataAttribute](https://github.com/Microsoft/testfx/pull/373) [Contributed by [Brad Stoney](https://github.com/bstoney)] [enhancement]
 - [x] [Fix incompatibility between multiple versions of mstest adapter present in a solution](https://github.com/Microsoft/testfx/pull/404)
 - [x] [Fix multiple results not returning for custom TestMethod](https://github.com/Microsoft/testfx/pull/363) [Contributed by [Cédric Bignon](https://github.com/bignoncedric)]
 - [x] [Run Class Cleanup in sync with Class Initialize](https://github.com/Microsoft/testfx/pull/372)
 - [x] [Fix to show right error message on assembly load exception during test run](https://github.com/Microsoft/testfx/issues/395)
 - [x] [Consistent behavior of GenericParameterHelper's while running and debugging](https://github.com/Microsoft/testfx/issues/362) [Contributed by [walterlv](https://github.com/walterlv)]
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.1...v1.3.0)

### Builds
* MSTest.TestFramework: [1.3.0](https://www.nuget.org/packages/MSTest.TestFramework/1.3.0)
* MSTest.TestAdapter: [1.3.0](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.0)

# 1.3.0-beta2 (January 2018)
 - [x] [In-Assembly Parallel Feature](https://github.com/Microsoft/testfx/pull/296)
 - [x] [Add missing Microsoft.Internal.TestPlatform.ObjectModel](https://github.com/Microsoft/testfx/pull/301) [Contributed by [Andrey Kurdyumov](https://github.com/kant2002)]
 - [x] [Adding warning message for vsmdi file](https://github.com/Microsoft/testfx/issues/61)
 - [x] [Fixing Key collision for test run parameters](https://github.com/Microsoft/testfx/issues/298)
 - [x] [Fix for csv x64 scenario](https://github.com/Microsoft/testfx/issues/325)
 - [x] [DataRow DisplayName Fix in .Net framework](https://github.com/Microsoft/testfx/issues/284)
 - [x] [Update File version for adapter and framework dlls](https://github.com/Microsoft/testfx/issues/268)
 - [x] [Add information about which assembly failed to discover test](https://github.com/Microsoft/testfx/pull/299) [Contributed by [Andrey Kurdyumov](https://github.com/kant2002)]
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.0...v1.3.0-beta2)

### Builds
* MSTest.TestFramework: [1.3.0-beta2](https://www.nuget.org/packages/MSTest.TestFramework/1.3.0-beta2)
* MSTest.TestAdapter: [1.3.0-beta2](https://www.nuget.org/packages/MSTest.TestAdapter/1.3.0-beta2)

# 1.2.1 (April 2018)
 - [x] [Fixing Key collision for test run parameters](https://github.com/Microsoft/testfx/pull/328)
 - [x] [Don't call Class Cleanup if Class Init not called](https://github.com/Microsoft/testfx/pull/372)
 - [x] [Fix masking assembly load failure error message](https://github.com/Microsoft/testfx/pull/382)
 - [x] [Fix UWP tests discovery](https://github.com/Microsoft/testfx/pull/332)
 
### Builds
* MSTest.TestFramework: [1.2.1](https://www.nuget.org/packages/MSTest.TestFramework/1.2.1)
* MSTest.TestAdapter: [1.2.1](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.1)

# 1.2.0 (October 2017)
 - [x] [DataSourceAttribute Implementation](https://github.com/Microsoft/testfx/pull/238)
 - [x] [Adding support for DiaNavigation in UWP test adapter](https://github.com/Microsoft/testfx/pull/258)
 - [x] [Arguments order for ArgumentException](https://github.com/Microsoft/testfx/pull/262)
 - [x] [Adding filtering support at discovery](https://github.com/Microsoft/testfx/pull/271)
 - [x] [Improve handling of Assert.Inconclusive](https://github.com/Microsoft/testfx/pull/277)
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.0-beta3...v1.2.0)

### Builds
* MSTest.TestFramework: [1.2.0](https://www.nuget.org/packages/MSTest.TestFramework/1.2.0)
* MSTest.TestAdapter: [1.2.0](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.0)

# 1.2.0-beta3 (August 2017)
 - [x] [Added Mapping for TestOutcome.None to the UnitTestOutcome Enum to achieve NotExecuted behaviour in VSTS](https://github.com/Microsoft/testfx/issues/217) [Contributed By [Irguzhav](https://github.com/irguzhav)] [enhancement]
 - [x] [TestMethod failures masked by TestCleanUp exceptions](https://github.com/Microsoft/testfx/issues/58)
 - [x] [Multiple copies added for same test on running multiple times in IntelliTest](https://github.com/Microsoft/testfx/issues/92)
 - [x] [Adapter is not sending TestCategory traits in Testcase object to Testhost](https://github.com/Microsoft/testfx/issues/189) 
 - [x] [All the Assert constructor's has been made private and the classes sealed](https://github.com/Microsoft/testfx/issues/223)
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.2.0-beta...v1.2.0-beta3)
 
### Builds
* MSTest.TestFramework: [1.2.0-beta3](https://www.nuget.org/packages/MSTest.TestFramework/1.2.0-beta3)
* MSTest.TestAdapter: [1.2.0-beta3](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.0-beta3)

# 1.2.0-beta (June 2017)
 - [x] [Support for Dynamic Data Attribute](https://github.com/Microsoft/testfx/issues/141) [extensibility]
 - [x] [Make discovering test methods from base classes defined in another assembly the default](https://github.com/Microsoft/testfx/issues/164) [enhancement]
 - [x] [CollectSourceInformation awareness to query source information](https://github.com/Microsoft/testfx/issues/119) [enhancement]
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.18...v1.2.0-beta)
 
### Builds
* MSTest.TestFramework: [1.2.0-beta](https://www.nuget.org/packages/MSTest.TestFramework/1.2.0-beta)
* MSTest.TestAdapter: [1.2.0-beta](https://www.nuget.org/packages/MSTest.TestAdapter/1.2.0-beta)

# 1.1.18 (June 2017)
 - [x] [Ability to provide a reason for Ignored tests](https://github.com/Microsoft/testfx/issues/126) [enhancement]
 - [x] [VB unit test project templates that ship in VS 2017 do not reference MSTest V2 nuget packages](https://github.com/Microsoft/testfx/issues/132) [enhancement]
 - [x] [Assert.IsInstanceOf passes on value null](https://github.com/Microsoft/testfx/issues/178) [Contributed By [LarsCelie](https://github.com/larscelie)]
 - [x] [Test methods in a base class defined in a different assembly are not navigable in Test Explorer](https://github.com/Microsoft/testfx/issues/163) [Contributed By [ajryan](https://github.com/ajryan)]
 - [x] [Enable MSTest framework based tests targeting .NET Core to be run and debugged from within VSCode](https://github.com/Microsoft/testfx/issues/182)
 - [x] [Web project templates that ship in VS 2017 do not reference MSTest V2 nuget packages](https://github.com/Microsoft/testfx/issues/167) 
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.17...v1.1.18)
 
### Builds
* MSTest.TestFramework: [1.1.18](https://www.nuget.org/packages/MSTest.TestFramework/1.1.18)
* MSTest.TestAdapter: [1.1.18](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.18)

# 1.1.17 (April 2017)
 - [x] [Console.WriteLine support for .NetCore Projects](https://github.com/Microsoft/testfx/issues/18) [enhancement]
 - [x] [Inheritance support for base classes that resides in different assemblies](https://github.com/Microsoft/testfx/issues/23) [enhancement]
 - [x] [TestContext.Writeline does not output messages](https://github.com/Microsoft/testfx/issues/120)
 - [x] [Logger.LogMessage logs a message mutliple times](https://github.com/Microsoft/testfx/issues/114)
 - [x] [TestContext.CurrentTestOutcome is always InProgress in the TestCleanup method](https://github.com/Microsoft/testfx/issues/89)
 - [x] [An inconclusive in a test initialize fails the test if it has an ExpectedException](https://github.com/Microsoft/testfx/issues/136) 
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.14...v1.1.17)
 
### Builds
* MSTest.TestFramework: [1.1.17](https://www.nuget.org/packages/MSTest.TestFramework/1.1.17)
* MSTest.TestAdapter: [1.1.17](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.17)

# 1.1.14 (March 2017)
 - [x] [Ability to add custom assertions](https://github.com/Microsoft/testfx/issues/116) [enhancement]
 - [x] [Problems with null in DataRow](https://github.com/Microsoft/testfx/issues/70)
 
A list of changes since last release are available [here](https://github.com/Microsoft/testfx/compare/v1.1.13...v1.1.14)
 
### Builds
* MSTest.TestFramework: [1.1.14](https://www.nuget.org/packages/MSTest.TestFramework/1.1.14)
* MSTest.TestAdapter: [1.1.14](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.14)

# 1.1.13 (March 2017)
 - [x] [Tests with Deployment Item do not run](https://github.com/Microsoft/testfx/issues/91)
 - [x] [Run tests fail intermittently with a disconnected from server exception](https://github.com/Microsoft/testfx/issues/28)
 - [x] [Templates and Wizards vsix should be built with RC3 tooling](https://github.com/Microsoft/testfx/issues/77)

This is also the first release from GitHub and with source code building against Dev15 tooling.

### Builds
* MSTest.TestFramework: [1.1.13](https://www.nuget.org/packages/MSTest.TestFramework/1.1.13)
* MSTest.TestAdapter: [1.1.13](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.13)

# 1.1.11 (February 2017)
Initial release.
### Builds
* MSTest.TestFramework: [1.1.11](https://www.nuget.org/packages/MSTest.TestFramework/1.1.11)
* MSTest.TestAdapter: [1.1.11](https://www.nuget.org/packages/MSTest.TestAdapter/1.1.11)
