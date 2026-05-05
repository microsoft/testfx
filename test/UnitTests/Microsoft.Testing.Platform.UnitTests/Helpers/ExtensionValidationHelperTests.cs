// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionValidationHelperTests
{
    // Generic overload: ValidateUniqueExtension(IEnumerable<T>, IExtension, Func<T, IExtension>)
    [TestMethod]
    public void ValidateUniqueExtension_WhenExistingExtensionsIsNull_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = null!;
        Mock<IExtension> newExtension = CreateExtension("uid1");
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenNewExtensionIsNull_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = [];
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existingExtensions.ValidateUniqueExtension(null!, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenExtensionSelectorIsNull_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = [];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existingExtensions.ValidateUniqueExtension<IExtension>(newExtension.Object, null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenCollectionIsEmpty_DoesNotThrow()
    {
        IEnumerable<IExtension> existingExtensions = [];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenNoDuplicateUid_DoesNotThrow()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        IEnumerable<IExtension> existingExtensions = [existing.Object];
        Mock<IExtension> newExtension = CreateExtension("uid2");
        existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenDuplicateUidExists_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        IEnumerable<IExtension> existingExtensions = [existing.Object];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x));
    }

    [TestMethod]
    public void ValidateUniqueExtension_ErrorMessageContainsUid()
    {
        Mock<IExtension> existing = CreateExtension("my-special-uid");
        IEnumerable<IExtension> existingExtensions = [existing.Object];
        Mock<IExtension> newExtension = CreateExtension("my-special-uid");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x));
        Assert.Contains("my-special-uid", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_ErrorMessageContainsTypeNames()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        IEnumerable<IExtension> existingExtensions = [existing.Object];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x));
        Assert.IsGreaterThan(0, ex.Message.Length);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WhenMultipleDuplicates_AllTypesInMessage()
    {
        Mock<IExtension> ext1 = CreateExtension("uid1");
        Mock<IExtension> ext2 = CreateExtension("uid1");
        IEnumerable<IExtension> existingExtensions = [ext1.Object, ext2.Object];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object, x => x));
        Assert.Contains("uid1", ex.Message);
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithWrapperType_SelectsViaFunc()
    {
        Mock<IExtension> innerExt = CreateExtension("uid1");
        WrapperExtension wrapper = new(innerExt.Object);
        IEnumerable<WrapperExtension> existingExtensions = [wrapper];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object, w => w.Extension));
    }

    [TestMethod]
    public void ValidateUniqueExtension_WithWrapperType_NoDuplicate_DoesNotThrow()
    {
        Mock<IExtension> innerExt = CreateExtension("uid1");
        WrapperExtension wrapper = new(innerExt.Object);
        IEnumerable<WrapperExtension> existingExtensions = [wrapper];
        Mock<IExtension> newExtension = CreateExtension("uid2");
        existingExtensions.ValidateUniqueExtension(newExtension.Object, w => w.Extension);
    }

    // Simple overload: ValidateUniqueExtension(IEnumerable<IExtension>, IExtension)
    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullExistingExtensions_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = null!;
        Mock<IExtension> newExtension = CreateExtension("uid1");
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NullNewExtension_ThrowsArgumentNullException()
    {
        IEnumerable<IExtension> existingExtensions = [];
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            existingExtensions.ValidateUniqueExtension(null!));
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_NoDuplicate_DoesNotThrow()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        IEnumerable<IExtension> existingExtensions = [existing.Object];
        Mock<IExtension> newExtension = CreateExtension("uid2");
        existingExtensions.ValidateUniqueExtension(newExtension.Object);
    }

    [TestMethod]
    public void ValidateUniqueExtension_SimpleOverload_DuplicateUid_ThrowsInvalidOperationException()
    {
        Mock<IExtension> existing = CreateExtension("uid1");
        IEnumerable<IExtension> existingExtensions = [existing.Object];
        Mock<IExtension> newExtension = CreateExtension("uid1");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            existingExtensions.ValidateUniqueExtension(newExtension.Object));
    }

    private static Mock<IExtension> CreateExtension(string uid)
    {
        Mock<IExtension> mock = new();
        mock.Setup(e => e.Uid).Returns(uid);
        mock.Setup(e => e.DisplayName).Returns(uid);
        mock.Setup(e => e.Description).Returns(uid);
        mock.Setup(e => e.Version).Returns("1.0");
        return mock;
    }

    private sealed class WrapperExtension(IExtension extension)
    {
        public IExtension Extension { get; } = extension;
    }
}
