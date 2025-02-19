// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

[Obsolete]
public sealed class BinaryFormatterExceptionSerializationTests : TestContainer
{
    public void AssertFailedExceptionCanBeSerializedAndDeserialized()
        => VerifySerialization(Assert.Fail);

    public void AssertInconclusiveExceptionCanBeSerializedAndDeserialized()
        => VerifySerialization(Assert.Inconclusive);

    public void InternalTestFailureExceptionCanBeSerializedAndDeserialized()
        => VerifySerialization(() => throw new InternalTestFailureException("Some internal error."));

    private void VerifySerialization(Action actionThatThrows)
    {
        try
        {
            actionThatThrows();
        }
        catch (Exception ex)
        {
            // Ensure the thrown exception can be serialized and deserialized by binary formatter to keep compatibility with it,
            // even though it is obsoleted and removed in .NET.
            var mem = new MemoryStream();
            var formatter = new BinaryFormatter
            {
                Binder = new FormatterBinder(),
            };
            formatter.Serialize(mem, ex);
            mem.Position = 0;
            string str = Encoding.UTF8.GetString(mem.GetBuffer(), 0, (int)mem.Length);
            Assert.IsNotNull(str);
            var deserializedException = (Exception)formatter.Deserialize(mem);

            Assert.AreEqual(ex.Message, deserializedException.Message);

            return;
        }

        throw new InvalidOperationException($"The provided '{nameof(actionThatThrows)}' did not throw any exception.");
    }

    /// <summary>
    /// This is for compliance, usage of BinaryFormatter without binder is not allowed.
    /// </summary>
    private class FormatterBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
            => null!;
    }
}
