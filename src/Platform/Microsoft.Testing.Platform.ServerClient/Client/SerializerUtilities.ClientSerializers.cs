// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

/// <summary>
/// Client-side additions to the server's <see cref="SerializerUtilities"/>.
/// </summary>
/// <remarks>
/// The server (platform) registers serializers for the messages it <i>sends</i> and deserializers for the
/// messages it <i>receives</i>. A client is the mirror image: it must serialize the request arguments the
/// server only ever deserializes, and deserialize the response arguments the server only ever serializes.
/// This partial adds exactly those missing directions and overrides the generic <see cref="RpcMessage"/>
/// decoder with a client-oriented one (the server's drops notification params).
/// <para>
/// It is compiled ONLY into the client package, never linked into the platform, so the server is untouched.
/// Because it is a partial of <see cref="SerializerUtilities"/> it can reach the private
/// <c>Serializers</c>/<c>Deserializers</c> dictionaries and private helper methods.
/// </para>
/// </remarks>
internal static partial class SerializerUtilities
{
    private static bool s_clientSerializersRegistered;

    /// <summary>
    /// Registers the client-only serializers/deserializers. Idempotent. MUST be called before
    /// <see cref="FormatterUtilities.CreateFormatter"/> so the System.Text.Json formatter (on .NET) snapshots
    /// the request-argument serializer types when it is constructed.
    /// </summary>
    public static void RegisterClientSerializers()
    {
        if (s_clientSerializersRegistered)
        {
            return;
        }

        s_clientSerializersRegistered = true;

        // --- Request-argument serializers (the server only deserializes these; the client sends them). ---
        Serializers[typeof(ClientInfo)] = new ObjectSerializer<ClientInfo>(info => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Name] = info.Name,
            [JsonRpcStrings.Version] = info.Version,
        });

        Serializers[typeof(ClientCapabilities)] = new ObjectSerializer<ClientCapabilities>(capabilities => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Testing] = new Dictionary<string, object?>
            {
                [JsonRpcStrings.DebuggerProvider] = capabilities.DebuggerProvider,
                [JsonRpcStrings.IsStateful] = capabilities.IsStateful,
            },
        });

        Serializers[typeof(InitializeRequestArgs)] = new ObjectSerializer<InitializeRequestArgs>(args => new Dictionary<string, object?>
        {
            [JsonRpcStrings.ProcessId] = args.ProcessId,
            [JsonRpcStrings.ClientInfo] = Serialize(args.ClientInfo),
            [JsonRpcStrings.Capabilities] = Serialize(args.Capabilities),
        });

        Serializers[typeof(DiscoverRequestArgs)] = new ObjectSerializer<DiscoverRequestArgs>(SerializeRequestArgs);
        Serializers[typeof(RunRequestArgs)] = new ObjectSerializer<RunRequestArgs>(SerializeRequestArgs);

        // Exit is a parameterless notification; register an (empty) serializer so a caller may pass an
        // ExitRequestArgs instance as the notification params without tripping the missing-serializer path.
        Serializers[typeof(ExitRequestArgs)] = new ObjectSerializer<ExitRequestArgs>(_ => new Dictionary<string, object?>());

        // --- Response deserializers (the server only serializes these; the client decodes them). ---
        Deserializers[typeof(Artifact)] = new ObjectDeserializer<Artifact>(properties => new Artifact(
            Uri: GetOptionalPropertyFromJson(properties, JsonRpcStrings.Uri) as string ?? string.Empty,
            Producer: GetOptionalPropertyFromJson(properties, JsonRpcStrings.Producer) as string ?? string.Empty,
            Type: GetOptionalPropertyFromJson(properties, JsonRpcStrings.Type) as string ?? string.Empty,
            DisplayName: GetOptionalPropertyFromJson(properties, JsonRpcStrings.DisplayName) as string ?? string.Empty,
            Description: GetOptionalPropertyFromJson(properties, JsonRpcStrings.Description) as string));

        Deserializers[typeof(DiscoverResponseArgs)] = new ObjectDeserializer<DiscoverResponseArgs>(_ => new DiscoverResponseArgs());

        Deserializers[typeof(RunResponseArgs)] = new ObjectDeserializer<RunResponseArgs>(properties =>
        {
            var attachments = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Attachments) as ICollection<object>;
            Artifact[] artifacts = attachments?
                .OfType<IDictionary<string, object?>>()
                .Select(attachment => Deserialize<Artifact>(attachment))
                .ToArray() ?? [];

            return new RunResponseArgs(artifacts);
        });

        // --- Override the generic RpcMessage decoder with a client-oriented one. ---
        // The server's decoder (SerializerUtilities.Deserializers.cs) is written from the server's point of
        // view: its method switch only knows the requests a *server* receives and returns null (dropping the
        // params) for every other method. A client instead receives notifications
        // (testing/testUpdates/tests, client/log, telemetry/update, testing/testUpdates/attachments) and
        // server-initiated requests (client/attachDebugger, client/launchDebugger) whose params it must keep.
        // We therefore preserve the raw params dictionary and let the client typed-deserialize per method.
        Deserializers[typeof(RpcMessage)] = new ObjectDeserializer<RpcMessage>(properties =>
        {
            ValidateJsonRpcHeader(properties);

            if (properties.TryGetValue(JsonRpcStrings.Method, out object? methodObj) && methodObj is not null)
            {
                string method = (string)methodObj;
                object? idObj = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Id);

                // Keep the params as the raw dictionary; the client decodes it based on the method name.
                object? @params = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Params);

                int? id = idObj is null
                    ? null
                    : GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

                return id.HasValue
                    ? new RequestMessage(id.Value, method, @params)
                    : new NotificationMessage(method, @params);
            }
            else if (properties.TryGetValue(JsonRpcStrings.Error, out _))
            {
                return Deserialize<ErrorMessage>(properties);
            }
            else if (properties.TryGetValue(JsonRpcStrings.Result, out object? resultObj))
            {
                object? idObj = GetRequiredPropertyFromJson<object?>(properties, JsonRpcStrings.Id);
                var result = resultObj as IDictionary<string, object?>;
                int id = GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

                return new ResponseMessage(id, result);
            }

            throw new MessageFormatException();
        });
    }

    private static IDictionary<string, object?> SerializeRequestArgs(RequestArgsBase args)
    {
        Dictionary<string, object?> properties = new()
        {
            [JsonRpcStrings.RunId] = args.RunId.ToString(),
        };

        if (args.TestNodes is not null)
        {
            properties[JsonRpcStrings.Tests] = args.TestNodes.Select(node => Serialize(node)).ToList<object>();
        }

        if (args.GraphFilter is not null)
        {
            properties[JsonRpcStrings.Filter] = args.GraphFilter;
        }

        return properties;
    }
}
