// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class SessionUidTests
{
    [TestMethod]
    public void ToString_IncludesTheValue()
        => Assert.AreEqual("SessionUid { Value = abc-123 }", new SessionUid("abc-123").ToString());
}
