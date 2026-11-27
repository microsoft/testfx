// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
    InvocationSetupCompleted (16):
    |---FieldCount---| 2 bytes
    ExecutionId (1, string), InstanceId (2, string), Succeeded (3, bool, always), ErrorMessage (4, string),
    Outputs (5, list, omitted when empty). Each output: |---FieldCount---| Name (1, string), HasValue (2, bool),
    Value (3, string, omitted when HasValue is false).
*/
internal sealed class InvocationSetupCompletedSerializer : NamedPipeSerializer<InvocationSetupCompleted>, INamedPipeSerializer
{
    public override int Id => InvocationSetupCompletedFieldsId.MessagesSerializerId;

    protected override InvocationSetupCompleted DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        bool succeeded = false;
        string? errorMessage = null;
        InvocationLifetimeOutput[] outputs = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            switch (fieldId)
            {
                case InvocationSetupCompletedFieldsId.Succeeded:
                    succeeded = ReadFieldPayload(stream, fieldSize, ReadBool);
                    return true;

                case InvocationSetupCompletedFieldsId.ErrorMessage:
                    errorMessage = ReadStringValue(stream, fieldSize);
                    return true;

                case InvocationSetupCompletedFieldsId.Outputs:
                    outputs = ReadFieldPayload(stream, fieldSize, ReadOutputs);
                    return true;

                default:
                    return false;
            }
        });

        return new(executionId, instanceId, succeeded, errorMessage, outputs);
    }

    protected override void SerializeCore(InvocationSetupCompleted objectToSerialize, Stream stream)
    {
        WriteExecutionScopedHeader(stream, objectToSerialize.ExecutionId, objectToSerialize.InstanceId, (ushort)(1
            + (objectToSerialize.ErrorMessage is null ? 0 : 1)
            + (IsNullOrEmpty(objectToSerialize.Outputs) ? 0 : 1)));
        WriteField(stream, InvocationSetupCompletedFieldsId.Succeeded, objectToSerialize.Succeeded);
        WriteField(stream, InvocationSetupCompletedFieldsId.ErrorMessage, objectToSerialize.ErrorMessage);
        WriteListPayload(stream, InvocationSetupCompletedFieldsId.Outputs, objectToSerialize.Outputs, static (s, output) =>
        {
            bool writeValue = output.HasValue && output.Value is not null;
            WriteUShort(s, (ushort)(2 + (writeValue ? 1 : 0)));
            WriteField(s, InvocationLifetimeOutputFieldsId.Name, output.Name);
            WriteField(s, InvocationLifetimeOutputFieldsId.HasValue, output.HasValue);
            if (writeValue)
            {
                WriteField(s, InvocationLifetimeOutputFieldsId.Value, output.Value);
            }
        });
    }

    private static InvocationLifetimeOutput[] ReadOutputs(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var outputs = new InvocationLifetimeOutput[length];
        for (int i = 0; i < length; i++)
        {
            string? name = null;
            bool hasValue = false;
            string? value = null;
            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case InvocationLifetimeOutputFieldsId.Name:
                        name = ReadStringValue(stream, fieldSize);
                        return true;

                    case InvocationLifetimeOutputFieldsId.HasValue:
                        hasValue = ReadFieldPayload(stream, fieldSize, ReadBool);
                        return true;

                    case InvocationLifetimeOutputFieldsId.Value:
                        value = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            outputs[i] = name is null
                ? throw new InvalidDataException("Invocation lifetime output is missing its name.")
                : new InvocationLifetimeOutput(name, hasValue, hasValue ? value ?? string.Empty : null);
        }

        return outputs;
    }
}

/*
    ModuleTestsCompleted (17):
    |---FieldCount---| 2 bytes
    ExecutionId (1, string), InstanceId (2, string), ExitCode (3, int, always).
*/
internal sealed class ModuleTestsCompletedSerializer : NamedPipeSerializer<ModuleTestsCompleted>, INamedPipeSerializer
{
    public override int Id => ModuleTestsCompletedFieldsId.MessagesSerializerId;

    protected override ModuleTestsCompleted DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        int exitCode = 0;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == ModuleTestsCompletedFieldsId.ExitCode)
            {
                exitCode = ReadFieldPayload(stream, fieldSize, ReadInt);
                return true;
            }

            return false;
        });

        return new(executionId, instanceId, exitCode);
    }

    protected override void SerializeCore(ModuleTestsCompleted objectToSerialize, Stream stream)
    {
        WriteExecutionScopedHeader(stream, objectToSerialize.ExecutionId, objectToSerialize.InstanceId, 1);
        WriteField(stream, ModuleTestsCompletedFieldsId.ExitCode, objectToSerialize.ExitCode);
    }
}

/*
    InvocationCleanupCompleted (18):
    |---FieldCount---| 2 bytes
    ExecutionId (1, string), InstanceId (2, string), Succeeded (3, bool, always), ErrorMessage (4, string).
*/
internal sealed class InvocationCleanupCompletedSerializer : NamedPipeSerializer<InvocationCleanupCompleted>, INamedPipeSerializer
{
    public override int Id => InvocationCleanupCompletedFieldsId.MessagesSerializerId;

    protected override InvocationCleanupCompleted DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        bool succeeded = false;
        string? errorMessage = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            switch (fieldId)
            {
                case InvocationCleanupCompletedFieldsId.Succeeded:
                    succeeded = ReadFieldPayload(stream, fieldSize, ReadBool);
                    return true;

                case InvocationCleanupCompletedFieldsId.ErrorMessage:
                    errorMessage = ReadStringValue(stream, fieldSize);
                    return true;

                default:
                    return false;
            }
        });

        return new(executionId, instanceId, succeeded, errorMessage);
    }

    protected override void SerializeCore(InvocationCleanupCompleted objectToSerialize, Stream stream)
    {
        WriteExecutionScopedHeader(stream, objectToSerialize.ExecutionId, objectToSerialize.InstanceId, (ushort)(1 + (objectToSerialize.ErrorMessage is null ? 0 : 1)));
        WriteField(stream, InvocationCleanupCompletedFieldsId.Succeeded, objectToSerialize.Succeeded);
        WriteField(stream, InvocationCleanupCompletedFieldsId.ErrorMessage, objectToSerialize.ErrorMessage);
    }
}
