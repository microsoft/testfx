# Pillars

The new testing platform is a result of the Microsoft testing team's experience. It aims to address the challenges encountered since the release of .NET Core in 2016. While there is a high level of compatibility between the .NET Framework and the new .NET, certain key features like the plugin system and the new possible form factors of .NET compilations have made it complex to evolve or fully support the new runtime feature with the current [VSTest](https://github.com/microsoft/vstest) testing platform architecture.

The main driving factors for the evolution of the new testing platform are:

* **Determinism**: Ensuring that running the same tests in different contexts (local, CI) will produce the same result. The new runtime does not rely on reflection or any other dynamic .NET runtime feature to coordinate a test run.
* **0 dependencies**: The core of the platform is a single .NET assembly, `Microsoft.Testing.Platform.dll`, which has no dependencies other than the supported runtimes.
* **Hostable**: The test runtime can be hosted in any .NET application. While a console application is commonly used to run tests, you can create a test application in any type of .NET application. This allows you to run tests within special contexts, such as devices or browsers, where there may be limitations.
* **Support all .NET form factors**: Support current and future .NET form factors, including Native AOT.
* **Performant**: Finding the right balance between features and extension points to avoid bloating the runtime with non-fundamental code. The new test platform is designed to "orchestrate" a test run, rather than providing implementation details on how to do it.
* **Enough extensible**: The new platform provides essential extensibility points to allow for maximum customization of runtime execution. It allows you to configure the test process host, observe the test process, and consume information from the test framework within the test host process.
