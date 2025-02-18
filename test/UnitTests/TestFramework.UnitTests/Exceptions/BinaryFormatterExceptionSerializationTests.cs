// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class BinaryFormatterExceptionSerializationTests : TestContainer
{
    public void AssertFailedExceptionCanBeSerializedAndDeserialized()
        => VerifySerialization(() => Assert.AreEqual(0, 1));

    private void VerifySerialization(Action actionThatThrows)
    {
        try
        {
            actionThatThrows();
        }
        catch (Exception ex)
        {
            var mem = new MemoryStream();
            new BinaryFormatter().Serialize(mem, ex);
            mem.Position = 0;
            new BinaryFormatter().Deserialize(mem);
        }

        throw new InvalidOperationException($"The provided '{nameof(actionThatThrows)}' did not throw any exception.");
    }
}
#endif
