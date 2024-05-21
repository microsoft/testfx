# Testing framework & extensions execution order

The testing platform consists of a [testing framework](itestframework.md) and any number of extensions that can operate [*in-process*](extensionintro.md) or [*out-of-process*](extensionintro.md). This document outlines the **sequence of calls** to all potential extensibility points to provide clarity on when a feature is anticipated to be invoked.

While a *sequence* could be used to depict this, we opt for a straightforward order of invocation calls, which allows for a more comprehensive commentary on the workflow.

1. [ITestHostEnvironmentVariableProvider.UpdateAsync](itesthostenvironmentvariableprovider.md) : Out-of-process
1. [ITestHostEnvironmentVariableProvider.ValidateTestHostEnvironmentVariablesAsync](itesthostenvironmentvariableprovider.md) : Out-of-process
1. [ITestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync](itesthostprocesslifetimehandler.md) : Out-of-process
1. Test host process start
1. [ITestHostProcessLifetimeHandler.OnTestHostProcessStartedAsync](itesthostprocesslifetimehandler.md) : Out-of-process, this event can intertwine the actions of *in-process* extensions, depending on race conditions.
1. [ITestApplicationLifecycleCallbacks.BeforeRunAsync](itestsessionlifetimehandler.md): In-process
1. [ITestSessionLifetimeHandler.OnTestSessionStartingAsync](itestsessionlifetimehandler.md): In-process
1. [ITestFramework.CreateTestSessionAsync](itestframework.md): In-process
1. [ITestFramework.ExecuteRequestAsync](itestframework.md): In-process, this method can be called one or more times. At this point, the testing framework will transmit information to the [IMessageBus](imessagebus.md) that can be utilized by the [IDataConsumer](idataconsumer.md).
1. [ITestFramework.CloseTestSessionAsync](itestframework.md): In-process
1. [ITestSessionLifetimeHandler.OnTestSessionFinishingAsync](itestsessionlifetimehandler.md): In-process
1. [ITestApplicationLifecycleCallbacks.AfterRunAsync](itestsessionlifetimehandler.md): In-process
1. In-process cleanup, involves calling dispose and [IAsyncCleanableExtension](asyncinitcleanup.md) on all extension points.
1. [ITestHostProcessLifetimeHandler.OnTestHostProcessExitedAsync](itesthostprocesslifetimehandler.md) : Out-of-process
1. Out-of-process cleanup, involves calling dispose and [IAsyncCleanableExtension](asyncinitcleanup.md) on all extension points.
