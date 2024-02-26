// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

namespace Contoso.EnsureTestFramework;

internal sealed class EnsureTestFramework : ITestFramework, IDataProducer
{
    private readonly IExtension _extension;

    public EnsureTestFramework(IExtension extension, ITestFrameworkCapabilities _, IServiceProvider __)
    {
        _extension = extension;
    }

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public Type[] DataTypesProduced { get; } =
        new Type[1] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        // Cleanup, e.g. close all child processes if you started any.
        CloseTestSessionResult result = new()
        {
            IsSuccess = true,
            // Here you can report any errors that happened but don't belong to a test, e.g. you failed to clean up.
            ErrorMessage = null,
        };

        return Task.FromResult(result);
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        CreateTestSessionResult result = new()
        {
            IsSuccess = true,
            // Here you can report any errors that happened but don't belong to a test, e.g. you failed start up.
            ErrorMessage = null,
        };

        return Task.FromResult(result);
    }

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        IRequest request = context.Request;
        try
        {
            switch (request)
            {
                case DiscoverTestExecutionRequest:
                    {
                        var testNode = new TestNode
                        {
                            DisplayName = "My test method",
                            Uid = new TestNodeUid("A stable, unique id that identifies my test method, such as assemblyname.classname.methodname.parameters."),
                            Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
                        };

                        // Report back the update, including optional parent of this test node.
                        var discovered = new TestNodeUpdateMessage(context.Request.Session.SessionUid, testNode, parentTestNodeUid: null);

                        // By implementing this interface, we tell the message bus what data we will produce.
                        IDataProducer testUpdateReporter = this;
                        await context.MessageBus.PublishAsync(testUpdateReporter, discovered);
                        break;
                    }
                case RunTestExecutionRequest:
                    {
                        var failedProperty = new FailedTestNodeStateProperty(new InvalidOperationException("Assertion failed."));
                        var testNode = new TestNode
                        {
                            DisplayName = "My test method",
                            Uid = new TestNodeUid("A stable, unique id that identifies my test method, such as assemblyname.classname.methodname.parameters."),
                            Properties = new PropertyBag(
                                failedProperty,
                                new TimingProperty(new TimingInfo(DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(100)), DateTime.UtcNow, TimeSpan.FromMilliseconds(100)))
                            ),
                        };
                        var executed = new TestNodeUpdateMessage(context.Request.Session.SessionUid, testNode, parentTestNodeUid: null);

                        // By implementing this interface, we tell the message bus what data we will produce.
                        IDataProducer testUpdateReporter = this;
                        await context.MessageBus.PublishAsync(testUpdateReporter, executed);
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }
        }
        finally
        {
            context.Complete();
        }

        return;
    }

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }
}
