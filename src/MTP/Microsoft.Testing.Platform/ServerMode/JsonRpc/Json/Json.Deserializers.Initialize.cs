// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static void RegisterInitializeDeserializers(Dictionary<Type, JsonDeserializer> deserializers)
    {
        deserializers[typeof(InitializeRequestArgs)] = new JsonElementDeserializer<InitializeRequestArgs>((json, jsonElement) =>
        {
            json.TryArrayBind(jsonElement, out string[]? protocolVersions, JsonRpcStrings.ProtocolVersions);
            if (protocolVersions is not null)
            {
                for (int i = 0; i < protocolVersions.Length; i++)
                {
                    protocolVersions[i] = protocolVersions[i]
                        ?? throw new MessageFormatException($"'{JsonRpcStrings.ProtocolVersions}' entries must be strings");
                }
            }

            return new InitializeRequestArgs(
                ProcessId: json.Bind<int>(jsonElement, JsonRpcStrings.ProcessId),
                ClientInfo: json.Bind<ClientInfo>(jsonElement, JsonRpcStrings.ClientInfo),
                Capabilities: json.Bind<ClientCapabilities>(jsonElement, JsonRpcStrings.Capabilities))
            {
                ProtocolVersions = protocolVersions,
            };
        });

        deserializers[typeof(ClientInfo)] = new JsonElementDeserializer<ClientInfo>((json, jsonElement) => new ClientInfo(
                Name: json.Bind<string>(jsonElement, JsonRpcStrings.Name),
                Version: json.Bind<string>(jsonElement, JsonRpcStrings.Version)));

        deserializers[typeof(ClientCapabilities)] = new JsonElementDeserializer<ClientCapabilities>((json, jsonElement) =>
        {
            jsonElement.TryGetProperty(JsonRpcStrings.Testing, out JsonElement testing);

            bool? isStateful = testing.ValueKind != JsonValueKind.Object
                || !testing.TryGetProperty(JsonRpcStrings.IsStateful, out JsonElement statefulElement)
                ? null
                : statefulElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new MessageFormatException($"'{JsonRpcStrings.IsStateful}' field has wrong type (expected {nameof(Boolean)})"),
                };

            return new ClientCapabilities(
                    DebuggerProvider: json.Bind<bool>(testing, JsonRpcStrings.DebuggerProvider),
                    IsStateful: isStateful)
            {
                ShowMessage = GetOptionalBoolean(testing, JsonRpcStrings.ShowMessage),
            };
        });

        deserializers[typeof(InitializeResponseArgs)] = new JsonElementDeserializer<InitializeResponseArgs>(
          (json, jsonElement) =>
          {
              string? protocolVersion = null;
              if (jsonElement.TryGetProperty(JsonRpcStrings.ProtocolVersion, out JsonElement protocolVersionElement)
                  && protocolVersionElement.ValueKind != JsonValueKind.Null)
              {
                  protocolVersion = protocolVersionElement.ValueKind == JsonValueKind.String
                      ? protocolVersionElement.GetString()
                      : throw new MessageFormatException(
                          $"'{JsonRpcStrings.ProtocolVersion}' field has wrong type (expected String)");
              }

              return new InitializeResponseArgs(
                  ProcessId: json.Bind<int>(jsonElement, JsonRpcStrings.ProcessId),
                  ServerInfo: json.Bind<ServerInfo>(jsonElement, JsonRpcStrings.ServerInfo),
                  Capabilities: json.Bind<ServerCapabilities>(jsonElement, JsonRpcStrings.Capabilities))
              {
                  ProtocolVersion = protocolVersion,
              };
          });

        deserializers[typeof(ServerInfo)] = new JsonElementDeserializer<ServerInfo>(
          (json, jsonElement) => new ServerInfo(
                  Name: json.Bind<string>(jsonElement, JsonRpcStrings.Name),
                  Version: json.Bind<string>(jsonElement, JsonRpcStrings.Version)));

        deserializers[typeof(ServerCapabilities)] = new JsonElementDeserializer<ServerCapabilities>(
          (json, jsonElement) => new ServerCapabilities(
                  TestingCapabilities: json.Bind<ServerTestingCapabilities>(jsonElement, JsonRpcStrings.Testing)));

        deserializers[typeof(ServerTestingCapabilities)] = new JsonElementDeserializer<ServerTestingCapabilities>(
          (json, jsonElement) => new ServerTestingCapabilities(
              SupportsDiscovery: json.Bind<bool>(jsonElement, JsonRpcStrings.SupportsDiscovery),
              MultiRequestSupport: json.Bind<bool>(jsonElement, JsonRpcStrings.MultiRequestSupport),
              VSTestProviderSupport: json.Bind<bool>(jsonElement, JsonRpcStrings.VSTestProviderSupport),
              SupportsAttachments: json.Bind<bool>(jsonElement, JsonRpcStrings.AttachmentsSupport),
              MultiConnectionProvider: json.Bind<bool>(jsonElement, JsonRpcStrings.MultiConnectionProvider))
          {
              ShowMessage = GetOptionalBoolean(jsonElement, JsonRpcStrings.ShowMessage),
          });
    }
}
