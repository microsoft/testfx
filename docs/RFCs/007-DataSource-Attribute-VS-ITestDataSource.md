# RFC 007- DataSource Attribute Vs ITestDataSource

## Summary
This details the MSTest V2 framework attribute "DataSource" for data driven tests where test data can be present in an excel file, xml file, sql database or OleDb. You can refer documentation [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.datasourceattribute) for more details.

## Motivation
At present, there are two codeflows for data-driven tests, one for DataSource Attribute and another for DataRow & DynamicData Attributes. This aims to have one common codeflow for handling data-driven tests.

Also, currently DataSource Attribute does not follow Test Framework's custom data source extensibility (i.e. `ITestDataSource`) and we want to modify DataSource Attribute implementation so that it follows framework's data source extensibility model. Presently, DataSource Attribute consumes test data via TestContext object whereas `ITestDataSource` consumes test data via Testmethod parameters. We will not be changing how data is consumed in DataSource Attribute, purely because of back compatibility reasons. So, DataSource Attribute will not exactly be extended from Test Framework's `ITestDataSource` but this is an attempt to bring the DataSource Attribute implementation closer to how it would have looked if it would have extended from Test Framework's data source extensibility.

## Detailed Design

### Requirements
1. DataSource Attribute and ITestDataSource should have a common code flow.
2. DataSource Attribute should provide the data for that invocation in the TestContext object.
3. Design should be extensible to support in-assembly parallelization on a data source.

### Proposed solution
The test adapter should define an interface class `ITestDataSource` (on similar lines of framework's ITestDataSource interface)which will be extended to get data from data source.
```csharp
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
	/// <summary>
	/// Interface that provides values from data source when data driven tests are run.
	/// </summary>
	public interface ITestDataSource
	{
		/// <summary>
		/// Gets the test data from custom test data source and sets dbconnection in testContext object.
		/// </summary>
		/// <param name="testMethodInfo">
		/// The info of test method.
		/// </param>
		/// <param name="testContext">
		/// Test Context object
		/// </param>
		/// <returns>
		/// Test data for calling test method.
		/// </returns>
		IEnumerable<object> GetData(UTF.ITestMethod testMethodInfo, ITestContext testContext);
	}
}
``` 
There is no change in how DataSource Attribute will be consumed. Test methods can be decorated as they were decorated earlier like this:
```csharp
[TestMethod]
[DataSource("Microsoft.VisualStudio.TestTools.DataSource.XML", "MyFile.xml", "MyTable", DataAccessMethod.Sequential)]
public void MyTestMethod()  
{
	var v = testContext.DataRow[0];
	Assert.AreEqual(v, "3");
}
```

The display name of tests in the above example would appear like they used to be as :
```
MyTestMethod (Data Row 0)
MyTestMethod (Data Row 1)
```

### Behaviour Changes in DataSource Attributes
Presently, TestFrameworks's `Execute()` is called once for a data-driven TestMethod, which in-turn takes care of running test for all DataRows. This will be changed to calling TestFramework's `Execute()` for each DataRow. i.e. the logic of executing data-driven tests will be moved out from framework to adapter.

### Differences between DataSource Attribute and ITestDataSource
| DataSource                                        | ITestDataSource                                        |
|---------------------------------------------------|--------------------------------------------------------|
| Test authors consume data via TestContext         | Test authors consume data via Testmethod parameters    |
| TestMethod does not require to have parameters    | TestMethod is required to have parameters              |

Note :
Test authors should not expect data to be set in TestContext for attributes inheriting from `ITestDataSource`. Going forward, data should only be consumed from Testmethod parameters for data-driven tests. 

### Support Scenarios
Following scenarios will not supported in case of DataSource Attributes :

* Multiple DataSource Attributes on a TestMethod will not be supported.
* DataSource Attribute and DataRow Attribute should not be given together for a TestMethod. If in case both are given, DataSource Attribute will take precedence and will be used as a DataSource for that test, provided that TestMethod doesn't take any parameters.
* DataSource Attribute will not be open for extensibility.

## Open Questions
None.  
