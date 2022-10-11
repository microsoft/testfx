# RFC 008 - Test case timeout via runsettings

## Motivation
User should be able to configure global test case timeout for all the test cases part of the run. 

### Proposed solution
Make test case timeout configurable via TestTimeout tag which is part of the adapter node in the runsettings.

Here is a sample runsettings: 
```xml
<Runsettings> 
  <MSTestV2> 
    <TestTimeout>5000</TestTimeout>   
  </MSTestV2> 
</Runsettings> 
```

### Honoring the settings 
- If no settings are provided in runsettings, default timeout is set to 0. 
- Timeout specified via Timeout attribute on TestMethod takes precedence over the global timeout specified via runsettings. 
- For all the test methods that do not have Timeout attribute, timeout will be based on the timeout specified via runsettings.