// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    // ValidateUniqueExtension<T> overload (with extensionSelector)
    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_NullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> nullExtensions = null!;
        TestExtension newExtension = new();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            nullExtensions.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_NullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(null!, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_NullExtensionSelector_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];
        TestExtension newExtension = new();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension<IExtension>(newExtension, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_EmptyExistingCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        TestExtension newExtension = new();

        existing.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_NoDuplicateUid_DoesNotThrow()
    {
        TestExtension existing1 = new() { UidOverride = "ext-1" };
        TestExtension existing2 = new() { UidOverride = "ext-2" };
        TestExtension newExtension = new() { UidOverride = "ext-3" };

        List<IExtension> existing = [existing1, existing2];

        existing.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_DuplicateUid_ThrowsInvalidOperationException()
    {
        TestExtension existingExtension = new() { UidOverride = "same-uid" };
        TestExtension newExtension = new() { UidOverride = "same-uid" };
        List<IExtension> existing = [existingExtension];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_DuplicateUid_ErrorMessageContainsUid()
    {
        TestExtension existingExtension = new() { UidOverride = "my-unique-uid" };
        TestExtension newExtension = new() { UidOverride = "my-unique-uid" };
        List<IExtension> existing = [existingExtension];

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains("my-unique-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_MultipleDuplicates_ThrowsInvalidOperationException()
    {
        TestExtension existing1 = new() { UidOverride = "same-uid" };
        TestExtension existing2 = new() { UidOverride = "same-uid" };
        TestExtension newExtension = new() { UidOverride = "same-uid" };
        List<IExtension> existing = [existing1, existing2];

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains("same-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_UidIsCaseSensitive_DifferentCaseDoesNotThrow()
    {
        TestExtension existingExtension = new() { UidOverride = "uid-lowercase" };
        TestExtension newExtension = new() { UidOverride = "UID-LOWERCASE" };
        List<IExtension> existing = [existingExtension];

        existing.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_SelectorUsedToExtractExtension()
    {
        Wrapper existing1 = new(new TestExtension { UidOverride = "ext-1" });
        TestExtension newExtension = new() { UidOverride = "ext-1" };
        List<Wrapper> wrappers = [existing1];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            wrappers.ValidateUniqueExtension(newExtension, w => w.Extension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithSelector_SelectorWithNonMatchingUid_DoesNotThrow()
    {
        Wrapper existing1 = new(new TestExtension { UidOverride = "ext-1" });
        TestExtension newExtension = new() { UidOverride = "ext-2" };
        List<Wrapper> wrappers = [existing1];

        wrappers.ValidateUniqueExtension(newExtension, w => w.Extension);
    }

    // ValidateUniqueExtension simple overload (IEnumerable<IExtension>)
    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> nullExtensions = null!;
        TestExtension newExtension = new();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            nullExtensions.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_EmptyExistingCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        TestExtension newExtension = new();

        existing.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NoDuplicateUid_DoesNotThrow()
    {
        TestExtension existingExtension = new() { UidOverride = "ext-1" };
        TestExtension newExtension = new() { UidOverride = "ext-2" };
        List<IExtension> existing = [existingExtension];

        existing.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_DuplicateUid_ThrowsInvalidOperationException()
    {
        TestExtension existingExtension = new() { UidOverride = "same-uid" };
        TestExtension newExtension = new() { UidOverride = "same-uid" };
        List<IExtension> existing = [existingExtension];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_DuplicateUid_ErrorMessageContainsUid()
    {
        TestExtension existingExtension = new() { UidOverride = "duplicate-uid" };
        TestExtension newExtension = new() { UidOverride = "duplicate-uid" };
        List<IExtension> existing = [existingExtension];

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existing.ValidateUniqueExtension(newExtension));

        Assert.Contains("duplicate-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_UidIsCaseSensitive_DifferentCaseDoesNotThrow()
    {
        TestExtension existingExtension = new() { UidOverride = "uid-lowercase" };
        TestExtension newExtension = new() { UidOverride = "UID-LOWERCASE" };
        List<IExtension> existing = [existingExtension];

        existing.ValidateUniqueExtension(newExtension);
    }

    private sealed class Wrapper(TestExtension extension)
    {
        public TestExtension Extension { get; } = extension;
    }
}
