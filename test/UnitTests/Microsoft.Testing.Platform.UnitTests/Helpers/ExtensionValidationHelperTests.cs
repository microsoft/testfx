// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    // ---- ValidateUniqueExtension<T>(IEnumerable<T>, IExtension, Func<T, IExtension>) ----
    [TestMethod]
    public void ValidateUniqueExtension_NullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> nullCollection = null!;
        IExtension newExtension = new TestExtension();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            nullCollection.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_NullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];
        IExtension nullExtension = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(nullExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_NullSelector_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];
        IExtension newExtension = new TestExtension();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(newExtension, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_EmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        IExtension newExtension = new TestExtension("MyExt");

        existing.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_NoDuplicateUid_DoesNotThrow()
    {
        List<IExtension> existing = [new TestExtension("ExtA"), new TestExtension("ExtB")];
        IExtension newExtension = new TestExtension("ExtC");

        existing.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_DuplicateUid_ThrowsInvalidOperationException()
    {
        List<IExtension> existing = [new TestExtension("DupExt")];
        IExtension newExtension = new TestExtension("DupExt");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_DuplicateUid_ExceptionMessageContainsUid()
    {
        const string uid = "SharedUid";
        List<IExtension> existing = [new TestExtension(uid)];
        IExtension newExtension = new TestExtension(uid);

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains(uid, ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithWrapper_ExtractsSelectorCorrectly()
    {
        var existing = new List<(string Name, IExtension Ext)>
        {
            ("first", new TestExtension("UniqueA")),
        };
        IExtension newExtension = new TestExtension("UniqueB");

        existing.ValidateUniqueExtension(newExtension, item => item.Ext);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithWrapper_DuplicateSelectorUid_Throws()
    {
        const string uid = "ConflictUid";
        var existing = new List<(string Name, IExtension Ext)>
        {
            ("first", new TestExtension(uid)),
        };
        IExtension newExtension = new TestExtension(uid);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, item => item.Ext));
    }

    [TestMethod]
    public void ValidateUniqueExtension_MultipleDuplicates_ExceptionMessageContainsAllTypes()
    {
        const string uid = "SharedUid";
        List<IExtension> existing =
        [
            new TestExtension(uid),
            new TestExtension(uid),
        ];
        IExtension newExtension = new TestExtension(uid);

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains(uid, ex.Message);
        Assert.Contains(nameof(TestExtension), ex.Message);
    }

    // ---- ValidateUniqueExtension(IEnumerable<IExtension>, IExtension) simple overload ----
    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_EmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        IExtension newExtension = new TestExtension("ExtZ");

        existing.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NoDuplicate_DoesNotThrow()
    {
        List<IExtension> existing = [new TestExtension("ExtX")];
        IExtension newExtension = new TestExtension("ExtY");

        existing.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_DuplicateUid_ThrowsInvalidOperationException()
    {
        const string uid = "DupSimple";
        List<IExtension> existing = [new TestExtension(uid)];
        IExtension newExtension = new TestExtension(uid);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> nullCollection = null!;
        IExtension newExtension = new TestExtension();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            nullCollection.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];
        IExtension nullExtension = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(nullExtension));
    }
}
