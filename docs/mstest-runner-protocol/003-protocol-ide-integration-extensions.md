# 003 - Test Platform V3 integration with the editor

## Summary

An IDE's might want to include commands that allows the user to trigger test runs from various contexts,
such as running test clicking on a line of code, on a file, on a project.

In order to support this the adapter needs to be able to find tests based on the scope that's been given.
In addition the adapter needs to provide properties back to the IDE, such that it can map the tests to source
code.

This document describes these formats.

> Note: Various formats presented here are language specific and as such the document describes the high level
> design, but then follows that up with the specific per-language implementations of the high level specification.

## IDE language integration

Some of the requests/responses contain language specific metadata.
In those cases the server's capabilities it announces at [initialization](./001-protocol-intro.md#determine-capabilities-during-test-runner-initialization),
should specify what metadata it embeds.

For example the .NET languages provide an FQN, whereas others do not. To support this concept the test runner
as part of its capabilities, should list FQN support.

Similarly the structure of the tests is language/framework dependant and should also be specified in the capabilities.

> Note: This document describes well known properties for such metadata.
> For instance, .NET languages include `project`, `namespace` and `class` as well known concepts.
> For some features to light up, the client' .NET extension should make use of these properties and the
> .NET test frameworks should also make use of these properties.

In particular the test node tree can represent an arbitrary graph.
However, if it's split into Namespace/Class/Theory/Test nodes, the test window can better use that information.

## Capabilities overview

```typescript
interface InitializeResponseWithEditorIntegration {
    capabilities: {
        testing: {
            // If `true` the server provides properties/features to interop with the old
            // legacy TPv2 properties on test nodes.
            // The server should also add the properties as specified by the `VsTestTestNode`
            // interface.
            vstestProvider?: boolean;

            // Note: The adapter can send location-namespace/location-class/location-testGroup
            // properties and these are optionally supported by IDE.
        }
    }
}
```

## Declaring syntax reference

The IDE has several features that require the knowledge of the location of tests:

* Provides navigation from test window to source
* Provides ability to show code lens or any other inline adornments
* Provides ability to select tests in test explorer from source code

### Location properties

If `capabilities.testing.vstestProvider` is false, then the server should include the following properties in the TestNode.

```typescript
interface ManagedTypeLocationTestNode : TestNode {
    // Managed type of the test corresponds to the class in C# that the test belongs to.
    'location.type': string;

    // Managed method of the test corresponds to the method (including the method parameters) in C#
    // that the test belongs to.
    'location.method': string;
}

// Note: location.file/location.line properties are by default specified on TestNodes.
```

> [!NOTE]
> `location.type` and `location.method` refer to the managed test properties as defined in [vstest documentation](https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md).

## Vstest provider properties

If `capabilities.testing.vstestProvider` is true, then the server should include the following properties in the TestNode.

```typescript
interface VsTestTestNode : TestNode {
    // vstest.TestCase.Id
    'vstest.TestCase.Id': string;

    // vstest.TestCase.FullyQualifliedName
    'vstest.TestCase.FullyQualifiedName': string;

    // Type location property of the test case
    'vstest.TestCase.ManagedType': string;

    // Method location property of the test case
    'vstest.TestCase.ManagedMethod': string;

    // How a test should be displayed as in the UI
    'vstest.TestCase.DisplayName': string;

    // The source where the test is coming from (for example a .dll/.js/.py file)
    'vstest.TestCase.Source': string;

    // The file where the test definition is located
    'vstest.TestCase.CodeFilePath': string;

    // The line number where the test definition is located
    'vstest.TestCase.LineNumber': string;

    // Note: If the test is returned from Microsoft Testing Platform, that itself was discovered
    // using another adapter, the executor uri can be specified by this property.
    // vstest.original-executor-uri
    'vstest.original-executor-uri'?: string;

    // Note: Different overrides to the test's hierarchy.
    // These affect how a test is displayed in the UI, and specifies the display
    // of the project/namespace/class/theory properties in the UI.
    'vstest.TestCase.Hierarchy': string[];
}
```

> [!NOTE]
> `vstest.TestCase.ManagedType` and `vstest.TestCase.ManagedMethod` refer to the managed test properties as defined in [vstest documentation](https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md).
> `vstest.TestCase.Hierarchy` refers to the hierarchy properties as defined in [vstest documentation](https://github.com/microsoft/vstest/blob/main/docs/RFCs/0033-Hierarchy-TestCase-Property.md).
