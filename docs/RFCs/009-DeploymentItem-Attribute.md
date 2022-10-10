# RFC 009- Deployment Item Attribute for Net Core

## Summary
This details the MSTest V2 framework attribute `DeploymentItem` for copying files or folders specified as deployment items to the deployment directory. Deployment directory is where all the deployment items are present along with TestSource dll.

## Motivation
Many a times, a test author takes dependency on certain files(like .dll, .xml, .txt etc.) and requires those files to be present in test run location at the time of test execution. Instead of manually copying the files, he/she can leverage the 'DeploymentItem' attribute provided by MSTest adapter to deploy those files to the test run location/deployment directory.

## Constructors
### public DeploymentItemAttribute (string path)
#### Parameters
&nbsp;&nbsp;&nbsp;&nbsp; `path` <br/>
&nbsp;&nbsp;&nbsp;&nbsp; The file or directory to deploy. The path is either absolute or relative to build output directory.

### public DeploymentItemAttribute (string path, string outputDirectory)
#### Parameters
&nbsp;&nbsp;&nbsp;&nbsp; `path` <br/>
&nbsp;&nbsp;&nbsp;&nbsp; The file or directory to deploy. The path is relative to the deployment directory. <br/>
&nbsp;&nbsp;&nbsp;&nbsp; `outputDirectory` <br/>
&nbsp;&nbsp;&nbsp;&nbsp; The path of the directory inside the deployment directory to which the items are to be copied. All files and directories identified by `path` will be copied to this directory.

## Features
1. It can be used either on TestClass or on TestMethod.
2. Users can have multiple instances of the attribute to specify more than one item.

## Example
```csharp
    [TestClass]
    [DeploymentItem(@"C:\classLevelDepItem.xml")]   //absolute path
    public class UnitTest1
    {
        [TestMethod]
        [DeploymentItem(@"..\..\methodLevelDepItem1.xml")]   //relative path
        [DeploymentItem(@"C:\DataFiles\methodLevelDepItem2.xml", "DataFiles")]   //custom output path
        public void TestMethod1()
        {
            String textFromFile = File.ReadAllText("classLevelDepItem.xml");
        }
    }
```

## Behavior Changes wrt FullFramework
1. In FullFramework, tests run from a newly created deployment directory ProjectRoot\TestResults\Deploy_*username* *timestamp*\Out. In NetCore, tests run from build output directory.      
2. Dependencies of DeploymentItem are by default deployed in FullFramework. Users can override this by specifying `DeployTestSourceDependencies` as false in RunSettings. Since dependencies of deployment items are not deployed in NetCore, `DeployTestSourceDependencies` setting will not be honored.

## Limitations
1. Error messages are supported only in English language yet. It'll be fixed as part of https://github.com/Microsoft/testfx/issues/591.
