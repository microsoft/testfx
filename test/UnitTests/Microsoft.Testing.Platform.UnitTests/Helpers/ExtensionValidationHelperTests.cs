// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    // Generic overload: ValidateUniqueExtension<T>(IEnumerable<T>, IExtension, Func<T, IExtension>)
    [TestMethod]
    public void ValidateUniqueExtension_WhenExistingExtensionsIsNull_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = null!;
        IExtension newExtension = CreateExtension("uid1");

        Assert.ThrowsExactly<ArgumentNullException>(
            () => existingExtensions.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenNewExtensionIsNull_ThrowsArgumentNullException()
    {
        IExtension[] existingExtensions = [];
        IExtension newExtension = null!;

        Assert.ThrowsExactly<ArgumentNullException>(
            () => existingExtensions.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenExtensionSelectorIsNull_ThrowsArgumentNullException()
    {
        IExtension[] existingExtensions = [];
        IExtension newExtension = CreateExtension("uid1");

        Assert.ThrowsExactly<ArgumentNullException>(
            () => existingExtensions.ValidateUniqueExtension<IExtension>(newExtension, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenCollectionIsEmpty_DoesNotThrow()
    {
        IExtension[] existingExtensions = [];
        IExtension newExtension = CreateExtension("uid1");

        existingExtensions.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenNoDuplicateUid_DoesNotThrow()
    {
        IExtension[] existingExtensions = [CreateExtension("uid-A"), CreateExtension("uid-B")];
        IExtension newExtension = CreateExtension("uid-C");

        existingExtensions.ValidateUniqueExtension(newExtension, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenDuplicateUidExists_ThrowsInvalidOperationException()
    {
        IExtension[] existingExtensions = [CreateExtension("uid-dup")];
        IExtension newExtension = CreateExtension("uid-dup");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => existingExtensions.ValidateUniqueExtension(newExtension, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenDuplicateUidExists_ErrorMessageContainsUid()
    {
        const string duplicateUid = "my-duplicate-uid";
        IExtension[] existingExtensions = [CreateExtension(duplicateUid)];
        IExtension newExtension = CreateExtension(duplicateUid);

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => existingExtensions.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains(duplicateUid, ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenDuplicateUidExists_ErrorMessageContainsTypeNames()
    {
        const string duplicateUid = "my-duplicate-uid";
        IExtension existing = CreateExtension(duplicateUid);
        IExtension newExtension = CreateExtension(duplicateUid);

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => new[] { existing }.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains(existing.GetType().ToString(), ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenMultipleDuplicatesExist_ErrorMessageContainsAllTypes()
    {
        const string duplicateUid = "shared-uid";
        FakeExtensionA existing1 = new(duplicateUid);
        FakeExtensionB existing2 = new(duplicateUid);
        FakeExtensionC newExtension = new(duplicateUid);

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => new IExtension[] { existing1, existing2 }.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains(typeof(FakeExtensionA).ToString(), ex.Message);
        Assert.Contains(typeof(FakeExtensionB).ToString(), ex.Message);
        Assert.Contains(typeof(FakeExtensionC).ToString(), ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithWrapperType_SelectsExtensionViaSelector()
    {
        const string duplicateUid = "wrapper-uid";
        ExtensionWrapper existing = new(CreateExtension(duplicateUid));
        IExtension newExtension = CreateExtension(duplicateUid);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new[] { existing }.ValidateUniqueExtension(newExtension, w => w.Extension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithWrapperType_WhenNoDuplicate_DoesNotThrow()
    {
        ExtensionWrapper existing = new(CreateExtension("uid-X"));
        IExtension newExtension = CreateExtension("uid-Y");

        new[] { existing }.ValidateUniqueExtension(newExtension, w => w.Extension);
    }

    // Simple overload: ValidateUniqueExtension(IEnumerable<IExtension>, IExtension)
    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_WhenExistingExtensionsIsNull_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = null!;
        IExtension newExtension = CreateExtension("uid1");

        Assert.ThrowsExactly<ArgumentNullException>(
            () => existingExtensions.ValidateUniqueExtension(newExtension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_WhenNewExtensionIsNull_ThrowsArgumentNullException()
    {
        IExtension[] existingExtensions = [];

        Assert.ThrowsExactly<ArgumentNullException>(
            () => existingExtensions.ValidateUniqueExtension(null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_WhenNoDuplicate_DoesNotThrow()
    {
        IExtension[] existingExtensions = [CreateExtension("uid-A"), CreateExtension("uid-B")];
        IExtension newExtension = CreateExtension("uid-C");

        existingExtensions.ValidateUniqueExtension(newExtension);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_WhenDuplicateUidExists_ThrowsInvalidOperationException()
    {
        const string duplicateUid = "duplicated";
        IExtension[] existingExtensions = [CreateExtension(duplicateUid)];
        IExtension newExtension = CreateExtension(duplicateUid);

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => existingExtensions.ValidateUniqueExtension(newExtension));

        Assert.Contains(duplicateUid, ex.Message);
    }

    private static IExtension CreateExtension(string uid) => new FakeExtension(uid);

    private sealed class FakeExtension(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class FakeExtensionA(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class FakeExtensionB(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class FakeExtensionC(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed record ExtensionWrapper(IExtension Extension);
}
