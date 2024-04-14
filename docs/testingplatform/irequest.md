# Available requests

The subsequent section provides a detailed description of the various requests that a test framework may receive and process.

Before proceeding to the next section, it's crucial to thoroughly comprehend the concept of the [IMessageBus](imessagebus.md), which is the essential service for conveying test execution information to the testing platform.

## The `TestNodeUpdateMessage` data payload

As mentioned in the [IMessageBus](imessagebus.md) section, before utilizing the message bus, you must specify the type of data you intend to supply. The testing platform has defined a well-known type, `TestNodeUpdateMessage`, to represent the concept of a *test update information*.
This part of the document will explain how to utilize this payload data. Let's examine the surface:

```cs
public sealed class TestNodeUpdateMessage(SessionUid sessionUid, TestNode testNode, TestNodeUid? parentTestNodeUid = null)
{
    public TestNode TestNode { get; }
    public TestNodeUid? ParentTestNodeUid { get; }
}

public class TestNode
{
    public required TestNodeUid Uid { get; init; }
    public required string DisplayName { get; init; }
    public PropertyBag Properties { get; init; } = new();
}

public sealed class TestNodeUid(string value) 

public sealed partial class PropertyBag
{
    public PropertyBag();
    public PropertyBag(params IProperty[] properties);
    public PropertyBag(IEnumerable<IProperty> properties);
    public int Count { get; }
    public void Add(IProperty property);
    public bool Any<TProperty>();
    public TProperty? SingleOrDefault<TProperty>();
    public TProperty Single<TProperty>();
    public TProperty[] OfType<TProperty>();        
    public IEnumerable<IProperty> AsEnumerable();
    public IEnumerator<IProperty> GetEnumerator();
    ...
}

public interface IProperty
{
}
```

* `TestNodeUpdateMessage`: The `TestNodeUpdateMessage` consists of two properties: a `TestNode`, which we will discuss in this section, and a `ParentTestNodeUid`. The `ParentTestNodeUid` indicates that a test may have a parent test, introducing the concept of a **test tree** where `TestNode`s can be arranged in relation to each other. This structure allows for future enhancements and features based on the *tree* relationship between the nodes. If your test framework doesn't require a test tree structure, you can opt not to use it and simply set it to null, resulting in a straightforward flat list of `TestNode`s.

* `TestNode`: The `TestNode` is composed of three properties, one of which is the `Uid` of type `TestNodeUid`. This `Uid` serves as the **UNIQUE STABLE ID** for the node. The term **UNIQUE STABLE ID** implies that the same `TestNode` should maintain an **IDENTICAL** `Uid` across different runs and operating systems. The `TestNodeUid` is an **arbitrary opaque string** that the testing platform accepts as is.

>> [!NOTE]
>> The stability and uniqueness of the ID are crucial in the testing domain. They enable the precise targeting of a single test for execution and allow the ID to serve as a persistent identifier for a test, facilitating powerful extensions and features.

The second property is `DisplayName`, which is the human-friendly name for the test. For example, this name is displayed when you execute the `--list-tests` command line.

The third attribute is `Properties`, which is a `PropertyBag` type. As demonstrated in the code, this is a specialized property bag that holds generic properties about the `TestNode`. This implies that you can append any property to the node that implements the placeholder interface `IProperty`.

***The testing platform identifies specific properties added to a `TestNode` to determine whether a test has passed, failed, or been skipped.***

You can find the current list of available properties with the relative description in the section [TestNodeUpdateMessage.TestNode](testnodeupdatemessage.md)

Finally this section makes clear that you test framework implementaion needs to implement the `IDataProducer` that produces `TestNodeUpdateMessage`s like in the sample below:

```cs
internal sealed class TestingFramework : ITestFramework, IDataProducer
{
   ...
   public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };
   ...
}
```

If your test adapter requires the publication of *files* during execution, you can find the recognized properties in this source file: <https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Messages/FileArtifacts.cs>. As you can see, you can provide file assets in a general manner or associate them with a specific `TestNode`. Remember, if you intend to push a `SessionFileArtifact`, you must declare it to the platform in advance, as shown below:

```cs
internal sealed class TestingFramework : ITestFramework, IDataProducer
{
   ...
   public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact) };
   ...
}
```

## DiscoverTestExecutionRequest

```cs
public class DiscoverTestExecutionRequest
{
    // Detailed in the custom section below
    public TestSessionContext Session { get; }

    // This is experimental and intended for future use, please disregard for now.
    public ITestExecutionFilter Filter { get; }
}
```

The `DiscoverTestExecutionRequest` instructs the test framework **to discover** the tests and communicate this information thought to the [IMessageBus](imessagebus.md).

As outlined in the previous section, the property for a discovered test is `DiscoveredTestNodeStateProperty`. Here is a generic code snippet for reference:

```cs
...
var testNode = new TestNode()
{
    Uid = GenerateUniqueStableId(),
    DisplayName = GetDisplayName(),
    Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
};

await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(discoverTestExecutionRequest.Session.SessionUid, testNode));
...
```

You can visit the [code sample](codesample.md) for a working discovery sample.

## RunTestExecutionRequest

```cs
public class RunTestExecutionRequest
{
    // Detailed in the custom section below
    public TestSessionContext Session { get; }

    // This is experimental and intended for future use, please disregard for now.
    public ITestExecutionFilter Filter { get; }
}
```

The `RunTestExecutionRequest` instructs the test framework **to execute** the tests and communicate this information thought to the [IMessageBus](imessagebus.md).

Here is a generic code snippet for reference:

```cs
...
var skippedTestNode = new TestNode()
{
    Uid = GenerateUniqueStableId(),
    DisplayName = GetDisplayName(),
    Properties = new PropertyBag(SkippedTestNodeStateProperty.CachedInstance),
};

await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, skippedTestNode));
...
var successfulTestNode = new TestNode()
{
    Uid = GenerateUniqueStableId(),
    DisplayName = GetDisplayName(),
    Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
};

await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, successfulTestNode));
...
var assertionFailedTestNode = new TestNode()
{
    Uid = GenerateUniqueStableId(),
    DisplayName = GetDisplayName(),
    Properties = new PropertyBag(new FailedTestNodeStateProperty(assertionException)),
};

await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, assertionFailedTestNode));
...
var failedTestNode = new TestNode()
{
    Uid = GenerateUniqueStableId(),
    DisplayName = GetDisplayName(),
    Properties = new PropertyBag(new ErrorTestNodeStateProperty(ex.InnerException!)),
};

await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, failedTestNode));          
```

You can visit the [code sample](codesample.md) for a working execution sample.

### TestSessionContext

The `TestSessionContext` is a shared property across all requests, providing information about the ongoing test session:

```cs
public class TestSessionContext
{
    public SessionUid SessionUid { get; }
    public ClientInfo Client { get; }
}

public readonly struct SessionUid(string value)
{
    public string Value { get; }
}

public sealed class ClientInfo
{
    public string Id { get; }
    public string Version { get; }
}
```

The `TestSessionContext` consists of the `SessionUid`, a unique identifier for the ongoing test session that aids in logging and correlating test session data. It also includes the `ClientInfo` type, which provides details about the *initiator* of the test session. The test framework may choose different routes or publish varying information based on the identity of the test session's *initiator*.
