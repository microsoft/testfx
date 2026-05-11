// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    [TestMethod]
    public void ValidateUniqueExtension_NullExistingExtensions_ThrowsArgumentNullException()
    {
        Mock<IExtension> newExtension = CreateExtension("uid1");
        IEnumerable<IExtension> nullExtensions = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            nullExtensions.ValidateUniqueExtension(newExtension.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_NullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(null!, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_NullExtensionSelector_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];
        Mock<IExtension> newExtension = CreateExtension("uid1");

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension<IExtension>(newExtension.Object, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_EmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        Mock<IExtension> newExtension = CreateExtension("uid1");

        existing.ValidateUniqueExtension(newExtension.Object, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_NoDuplicate_DoesNotThrow()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        Mock<IExtension> newExtension = CreateExtension("uid2");

        List<IExtension> extensions = [existing.Object];

        extensions.ValidateUniqueExtension(newExtension.Object, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_OneDuplicate_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        Mock<IExtension> newExtension = CreateExtension("uid1");

        List<IExtension> extensions = [existing.Object];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            extensions.ValidateUniqueExtension(newExtension.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_OneDuplicate_ErrorMessageContainsUid()
    {
        Mock<IExtension> existing = CreateExtension("my-uid");
        Mock<IExtension> newExtension = CreateExtension("my-uid");

        List<IExtension> extensions = [existing.Object];

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            extensions.ValidateUniqueExtension(newExtension.Object, x => x));

        Assert.Contains("my-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_OneDuplicate_ErrorMessageContainsBothTypeNames()
    {
        IExtension existing = new ExtensionStubA("uid1");
        IExtension newExtension = new ExtensionStubB("uid1");
        string existingTypeName = existing.GetType().ToString();
        string newTypeName = newExtension.GetType().ToString();

        List<IExtension> extensions = [existing];

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            extensions.ValidateUniqueExtension(newExtension, x => x));

        Assert.Contains(existingTypeName, ex.Message);
        Assert.Contains(newTypeName, ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_MultipleDuplicates_ThrowsInvalidOperationException()
    {
        Mock<IExtension> ext1 = CreateExtension("uid1");
        Mock<IExtension> ext2 = CreateExtension("uid1");
        Mock<IExtension> newExtension = CreateExtension("uid1");

        List<IExtension> extensions = [ext1.Object, ext2.Object];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            extensions.ValidateUniqueExtension(newExtension.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithCustomSelector_DetectsDuplicateViaSelector()
    {
        Mock<IExtension> innerExt1 = CreateExtension("uid1");
        Mock<IExtension> innerNewExt = CreateExtension("uid1");

        List<(string Label, IExtension Ext)> wrappers = [("first", innerExt1.Object)];
        IExtension wrapperNew = innerNewExt.Object;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            wrappers.ValidateUniqueExtension(wrapperNew, w => w.Ext));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullExistingExtensions_ThrowsArgumentNullException()
    {
        Mock<IExtension> newExtension = CreateExtension("uid1");
        IEnumerable<IExtension> nullExtensions = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            nullExtensions.ValidateUniqueExtension(newExtension.Object));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullNewExtension_ThrowsArgumentNullException()
    {
        List<IExtension> existing = [];

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existing.ValidateUniqueExtension(null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_EmptyCollection_DoesNotThrow()
    {
        List<IExtension> existing = [];
        Mock<IExtension> newExtension = CreateExtension("uid1");

        existing.ValidateUniqueExtension(newExtension.Object);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NoDuplicate_DoesNotThrow()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        Mock<IExtension> newExtension = CreateExtension("uid2");

        List<IExtension> extensions = [existing.Object];

        extensions.ValidateUniqueExtension(newExtension.Object);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_OneDuplicate_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        Mock<IExtension> newExtension = CreateExtension("uid1");

        List<IExtension> extensions = [existing.Object];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            extensions.ValidateUniqueExtension(newExtension.Object));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_OneDuplicate_ErrorMessageContainsUid()
    {
        Mock<IExtension> existing = CreateExtension("duplicate-uid");
        Mock<IExtension> newExtension = CreateExtension("duplicate-uid");

        List<IExtension> extensions = [existing.Object];

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            extensions.ValidateUniqueExtension(newExtension.Object));

        Assert.Contains("duplicate-uid", ex.Message);
    }

    private static Mock<IExtension> CreateExtension(string uid)
    {
        Mock<IExtension> mock = new();
        mock.SetupGet(e => e.Uid).Returns(uid);
        mock.SetupGet(e => e.Version).Returns("1.0.0");
        mock.SetupGet(e => e.DisplayName).Returns("Test Extension");
        mock.SetupGet(e => e.Description).Returns("Test Extension Description");
        return mock;
    }

    private sealed class ExtensionStubA(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0.0";

        public string DisplayName => "Stub A";

        public string Description => "Stub A Description";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class ExtensionStubB(string uid) : IExtension
    {
        public string Uid => uid;

        public string Version => "1.0.0";

        public string DisplayName => "Stub B";

        public string Description => "Stub B Description";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
