# Pillars

The new testing platform is a result of the Microsoft testing team's experience. It aims to address the challenges encountered since the release of .NET Core in 2016. While there is a high level of compatibility between the .NET Framework and the new .NET, certain key features like the plugin system and the new possible form factors of .NET compilations have made it complex to evolve or fully support the new runtime feature with the current [VSTest](https://github.com/microsoft/vstest) testing platform architecture.

The main driving factors for the evolution of the new testing platform are:

* **Determinism**: Ensuring that running the same tests in different contexts (local, CI) will produce the same result. The new runtime does not rely on reflection or any other dynamic .NET runtime feature to coordinate a test run.
* **Runtime transparency**: The test runtime does not interfere with the test framework code, it does not create isolated contexts like `AppDomain` or `AssemblyLoadContext`, and it does not use reflection or custom assembly resolvers.
* **Compile-time registration of extensions**: Extensions, such as test frameworks and in/out-of-process extensions, are registered during compile-time to ensure determinism.
* **0 dependencies**: The core of the platform is a single .NET assembly, `Microsoft.Testing.Platform.dll`, which has no dependencies other than the supported runtimes.
* **Hostable**: The test runtime can be hosted in any .NET application. While a console application is commonly used to run tests, you can create a test application in any type of .NET application. This allows you to run tests within special contexts, such as devices or browsers, where there may be limitations.
* **Support all .NET form factors**: Support current and future .NET form factors, including Native AOT.
* **Performant**: Finding the right balance between features and extension points to avoid bloating the runtime with non-fundamental code. The new test platform is designed to "orchestrate" a test run, rather than providing implementation details on how to do it.
* **Extensible enough**: The new platform is built on extensibility points to allow for maximum customization of runtime execution. It allows you to configure the test process host, observe the test process, and consume information from the test framework within the test host process.
* **Single module deploy**: The hostability feature enables a single module deploy model, where a single compilation result can be used to support all extensibility points, both out-of-process and in-process, without the need to ship different executable modules.

The following factors should enhance the overall quality of the testing platform:

* Shifting all to compile time aids in identifying issues prior to runtime.
* Determinism is beneficial in all aspects, as it reduces bugs and, in the event of a bug, provides a clear, straightforward stack trace that directly indicates the problem.
* Determinism facilitates straightforward reproduction of issues, independent of the execution context (such as machine setup, local environment, CI, etc.). If the issue is related to the execution context, determinism makes it evident.
* Dynamic code, often associated with "indirect execution", can result in performance degradation. By avoiding it, performance can be enhanced.
* Eliminating dynamic code simplifies the logic behind feature development. The code you see is exactly what will be executed at runtime.
* Eliminating dependencies and dynamic code guarantees compatibility with upcoming runtime features.
* Eliminating dependencies means we can run everywhere.
* Operating a self-contained testing platform without dependencies ensures that there's always a mechanism to execute user tests. This is because `Microsoft.Testing.Platform.dll` is viewed as a fundamental part of the runtime itself, ideally `System.Testing.Platform`.
