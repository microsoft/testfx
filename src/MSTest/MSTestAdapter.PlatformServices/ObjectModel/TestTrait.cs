// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// A platform-agnostic name/value trait associated with a test (for example the values produced by
/// <c>[TestProperty]</c> attributes). This mirrors the shape historically carried by the test platform's
/// trait type but does not depend on any specific test platform's object model, so the platform services
/// layer can describe test traits without referencing that object model.
/// </summary>
#if NETFRAMEWORK
// A <see cref="UnitTestElement"/> (which carries these) is serialized across app domains on .NET Framework.
[Serializable]
#endif
internal readonly struct TestTrait
{
    public TestTrait(string name, string value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the trait name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the trait value.
    /// </summary>
    public string Value { get; }
}
