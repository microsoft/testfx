# The `Microsoft.Testing.Platform` a.k.a. `TestAnywhere`

## Table of content

1. [Pillars](pillars.md)
1. [High level architecture](architecture.md)
1. How to write and register a testing framework
    1. [Register the testing framework](registertestframework.md)
    1. [Implement the ITestFramework](itestframework.md)
    1. [Available requests](irequest.md)
        1. [Well known TestNodeUpdateMessage.TestNode properties](testnodeupdatemessage.md)
1. [Capabilities](capabilities.md)
1. Extensions
    1. [Introduction](extensionintro.md)
    1. In-process & out-of-process
        1. [ICommandLineOptionsProvider](icommandlineoptionsprovider.md)
    1. In-process
        1. [ITestSessionLifetimeHandler](itestsessionlifetimehandler.md)
        1. [ITestApplicationLifecycleCallbacks](itestapplicationlifecyclecallbacks.md)
        1. [IDataConsumer](idataconsumer.md)
    1. Out-of-process
        1. [ITestHostEnvironmentVariableProvider](itesthostenvironmentvariableprovider.md)
        1. [ITestHostProcessLifetimeHandler](itesthostprocesslifetimehandler.md)
1. Extensions miscellaneous
    1. [IAsyncInitializableExtension & IAsyncCleanableExtension](asyncinitcleanup.md)
    1. [CompositeExtensionFactory\<T\>](compositeextensionfactory.md)
1. [Testing framework & extensions execution order](executionorder.md)
1. Services
    1. [IServiceProvider](iserviceprovider.md)
    1. [IConfiguration](configuration.md)
    1. [ILoggerFactory](iloggerfactory.md)
    1. [IMessageBus](imessagebus.md)
    1. [ICommandLineOptions](icommandlineoptions.md)
    1. [IOutputDevice](ioutputdevice.md)
1. [Code sample](codesample.md)
