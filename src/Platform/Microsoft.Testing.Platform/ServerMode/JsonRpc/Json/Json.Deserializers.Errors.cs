// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static void RegisterErrorDeserializer(Dictionary<Type, JsonDeserializer> deserializers)
        => deserializers[typeof(ErrorMessage)] = new JsonElementDeserializer<ErrorMessage>(
          (json, jsonElement) =>
          {
              ValidateJsonRpcHeader(json, jsonElement);

              if (!jsonElement.TryGetProperty(JsonRpcStrings.Id, out JsonElement idElement))
              {
                  throw new MessageFormatException($"'{JsonRpcStrings.Id}' field is missing");
              }

              string? stringId = null;
              int? id = idElement.ValueKind == JsonValueKind.Null ? null : BindRpcId(jsonElement, out stringId);
              JsonElement error = jsonElement.GetProperty(JsonRpcStrings.Error);

              if (id is null)
              {
                  if (jsonElement.TryGetProperty(JsonRpcStrings.Method, out _) || jsonElement.TryGetProperty(JsonRpcStrings.Result, out _))
                  {
                      throw new MessageFormatException("An uncorrelated error response cannot contain 'method' or 'result'");
                  }

                  if (error.ValueKind != JsonValueKind.Object)
                  {
                      throw new MessageFormatException("'error' field has wrong type (expected Object)");
                  }

                  if (!error.TryGetProperty(JsonRpcStrings.Code, out JsonElement codeElement)
                      || codeElement.ValueKind != JsonValueKind.Number
                      || !codeElement.TryGetInt32(out _))
                  {
                      throw new MessageFormatException("error.code field must be an integer");
                  }

                  if (!error.TryGetProperty(JsonRpcStrings.Message, out JsonElement messageElement)
                      || messageElement.ValueKind != JsonValueKind.String)
                  {
                      throw new MessageFormatException("error.message field must be a string");
                  }
              }

              int code = json.Bind<int>(error, JsonRpcStrings.Code);
              string message = json.Bind<string>(error, JsonRpcStrings.Message);

              object? data = error.TryGetProperty(JsonRpcStrings.Data, out JsonElement dataElement)
                  ? ReadUntypedValue(json, dataElement)
                  : null;
              if (data is IDictionary<string, object?> { Count: 0 })
              {
                  data = null;
              }

              return new ErrorMessage(
                  Id: id,
                  ErrorCode: code,
                  Message: message ?? string.Empty,
                  Data: data)
              {
                  StringId = stringId,
              };
          });

    private static bool TryGetRpcId(JsonElement jsonElement, out int id, out string? stringId)
    {
        if (!jsonElement.TryGetProperty(JsonRpcStrings.Id, out JsonElement idElement))
        {
            id = default;
            stringId = null;
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Null)
        {
            throw new MessageFormatException($"'{JsonRpcStrings.Id}' field cannot be null");
        }

        stringId = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
        id = ReadRpcId(idElement);
        return true;
    }

    private static int BindRpcId(JsonElement jsonElement, out string? stringId)
        => jsonElement.TryGetProperty(JsonRpcStrings.Id, out JsonElement idElement)
            ? ReadRpcIdAndCaptureString(idElement, out stringId)
            : throw new MessageFormatException($"'{JsonRpcStrings.Id}' field is missing");

    private static int ReadRpcIdAndCaptureString(JsonElement idElement, out string? stringId)
    {
        stringId = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
        return ReadRpcId(idElement);
    }

    private static int ReadRpcId(JsonElement idElement)
        => idElement.ValueKind switch
        {
            JsonValueKind.Number when RpcIdParser.TryParseNumericId(idElement.GetRawText(), out int numericId) => numericId,
            JsonValueKind.String when int.TryParse(
                idElement.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int stringId)
                && idElement.GetString() == stringId.ToString(CultureInfo.InvariantCulture) => stringId,
            _ => throw new MessageFormatException($"'{JsonRpcStrings.Id}' field should be an int or a numeric string"),
        };
}
