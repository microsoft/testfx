// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Text;
using System.Text.Json;
#else
using Jsonite;
#endif

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Telemetry;

internal static class ExtensionInformationCollector
{
    public static async Task<string> CollectAndSerializeToJsonAsync(ServiceProvider serviceProvider)
    {
        HashSet<ExtensionInformation> extensionsInformation = [];

        foreach (object service in serviceProvider.Services)
        {
            if (service is IExtension extension)
            {
                extensionsInformation.Add(new ExtensionInformation(Sha256Hasher.HashWithNormalizedCasing(extension.Uid), extension.Version, await extension.IsEnabledAsync()));
            }

            if (service is MessageBusProxy messageBus)
            {
                foreach (IDataConsumer dataConsumer in messageBus.DataConsumerServices)
                {
                    if (dataConsumer is IExtension extension1)
                    {
                        extensionsInformation.Add(new ExtensionInformation(Sha256Hasher.HashWithNormalizedCasing(extension1.Uid), extension1.Version, await extension1.IsEnabledAsync()));
                    }
                }
            }
        }

#if NETCOREAPP
        using MemoryStream stream = new();
        Utf8JsonWriter writer = new(stream);
        writer.WriteStartArray();
        foreach (ExtensionInformation extension in extensionsInformation)
        {
            writer.WriteStartObject();
            writer.WriteString("Uid", extension.Id);
            writer.WriteString("Version", extension.Version);
            writer.WriteBoolean("Enabled", extension.Enabled);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
#else
        JsonArray jsonArray = [];
        foreach (ExtensionInformation extension in extensionsInformation)
        {
            JsonObject jsonObject = new()
            {
                { "Uid", extension.Id },
                { "Version", extension.Version },
                { "Enabled", extension.Enabled },
            };
            jsonArray.Add(jsonObject);
        }

        return jsonArray.ToString();
#endif
    }
}
