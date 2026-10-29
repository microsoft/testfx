"""Compile the existing source-only server client validation assembly."""

load(":defs.bzl", "testfx_library")

_PLATFORM = "src/Platform/Microsoft.Testing.Platform"

def server_client_library():
    testfx_library(
        name = "server_client",
        project = "src/Platform/Microsoft.Testing.Platform.ServerMode.Client.Sources",
        target_frameworks = ["net8.0"],
        polyfills = False,
        defines = ["IS_CORE_MTP", "IS_MTP_SERVER_MODE_CLIENT", "IS_MTP_CLIENT_REPO"],
        usings = ["Polyfills"],
        linked_srcs = [
            "src/Polyfills/Ensure.cs",
            "src/Polyfills/EmbeddedAttribute.cs",
            _PLATFORM + "/OutputDevice/Terminal/ExceptionFlattener.cs",
            _PLATFORM + "/TestHost/SessionUid.cs",
        ] + [
            _PLATFORM + "/ServerMode/JsonRpc/" + name + ".cs"
            for name in [
                "RpcMessages",
                "RpcIdParser",
                "JsonRpcMethods",
                "ErrorCodes",
                "SerializerUtilities",
                "SerializerUtilities.RpcMessageSerializers",
                "SerializerUtilities.TestNodeSerializers",
                "SerializerUtilities.Deserializers",
                "FormatterUtilities",
                "IMessageFormatter",
                "IObjectSerializer",
                "IObjectDeserializer",
                "MessageFormatException",
                "IMessageHandler",
                "TcpMessageHandler",
            ]
        ] + [
            _PLATFORM + "/Logging/" + name + ".cs"
            for name in ["ServerLogMessage", "LogLevel", "ILogger", "NopLogger", "LoggingExtensions"]
        ] + [
            _PLATFORM + "/Helpers/" + name + ".cs"
            for name in ["ObjectPool", "ApplicationStateGuard", "RoslynString", "RoslynHashCode", "RoslynDebug", "TaskExtensions"]
        ] + native.glob([_PLATFORM + "/ServerMode/JsonRpc/Json/**/*.cs"]) + native.glob(
            [_PLATFORM + "/Messages/*.cs"],
            exclude = [
                _PLATFORM + "/Messages/" + name + ".cs"
                for name in [
                    "AsyncConsumerDataProcessor.net",
                    "AsyncConsumerDataProcessor.netstandard",
                    "AsyncConsumerDataProcessorMessage",
                    "AsynchronousMessageBus",
                    "BaseMessageBus",
                    "BlockingConsumerDataProcessor",
                    "IAsyncConsumerDataProcessor",
                    "IAsyncConsumerDataProcessorWithActivityContext",
                    "IDataProducer",
                    "IMessageBus",
                    "MessageBusProxy",
                    "ShutdownTimeouts",
                    "SingleConsumerUnboundedChannel",
                ]
            ],
        ),
        internals_visible_to = [
            "Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests",
            "MSTest.Acceptance.IntegrationTests",
        ],
    )
