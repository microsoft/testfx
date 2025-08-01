# This is required for DataSourceTests
# Otherwise, the tests will fail with:
# The unit test adapter failed to connect to the data source or to read the data. For more information on troubleshooting this error, see "Troubleshooting Data-Driven Unit Tests" (http://go.microsoft.com/fwlink/?LinkId=62412) in the MSDN Library. Error details: The 'Microsoft.Ace.OLEDB.12.0' provider is not registered on the local machine.
#     at MSTest.PlatformServices.TestDataSource.GetData(ITestMethod testMethodInfo, ITestContext testContext) in /_/src/Adapter/MSTest.PlatformServices/Services/TestDataSource.cs:84
# The direct download link originates from https://www.microsoft.com/en-us/download/details.aspx?id=54920&msockid=01fa77be234c617f31936293223560aa
Invoke-RestMethod https://download.microsoft.com/download/3/5/C/35C84C36-661A-44E6-9324-8786B8DBE231/accessdatabaseengine_X64.exe -OutFile ./accessdatabaseengine_X64.exe
Start-Process ./accessdatabaseengine_X64.exe -Wait -ArgumentList "/quiet /passive /norestart"
