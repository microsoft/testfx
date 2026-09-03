// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;

using Microsoft.Testing.Extensions.AzureDevOpsReport;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Covers the platform guard around <see cref="HttpClientHandler.AutomaticDecompression"/>. The
/// browser and wasi handlers throw <see cref="PlatformNotSupportedException"/> from that setter
/// because <c>fetch</c> / <c>wasi:http</c> decode responses themselves, which used to make the
/// Azure DevOps report extension unusable there (see
/// <see href="https://github.com/microsoft/testfx/issues/10313"/>).
/// </summary>
[TestClass]
public sealed class AzureDevOpsTestResultsClientHandlerTests
{
    [TestMethod]
    public void CreateHttpClientHandler_WhenPlatformSupportsDecompression_DisablesRedirectsAndOptsIntoGZipAndDeflate()
    {
        Assert.IsFalse(OperatingSystem.IsBrowser(), "The unit test host is expected to run outside the browser sandbox.");

        using HttpClientHandler handler = AzureDevOpsTestResultsClient.CreateHttpClientHandler();

        Assert.IsFalse(handler.AllowAutoRedirect);
        Assert.IsTrue(handler.SupportsAutomaticDecompression, "This platform is expected to support automatic decompression.");
        Assert.AreEqual(DecompressionMethods.Deflate | DecompressionMethods.GZip, handler.AutomaticDecompression);
    }

    [TestMethod]
    public void ShouldOptInToAutomaticDecompression_WhenHandlerReportsNoSupport_ReturnsFalse()
    {
        using UnsupportedDecompressionHandler handler = new();

        Assert.IsFalse(AzureDevOpsTestResultsClient.ShouldOptInToAutomaticDecompression(handler));
    }

    [TestMethod]
    public void ShouldOptInToAutomaticDecompression_WhenHandlerReportsSupportOutsideBrowser_ReturnsTrue()
    {
        Assert.IsFalse(OperatingSystem.IsBrowser(), "The unit test host is expected to run outside the browser sandbox.");

        using HttpClientHandler handler = new();

        Assert.IsTrue(handler.SupportsAutomaticDecompression, "This platform is expected to support automatic decompression.");
        Assert.IsTrue(AzureDevOpsTestResultsClient.ShouldOptInToAutomaticDecompression(handler));
    }

    /// <summary>
    /// Stands in for the browser/wasi handlers: they report no support for automatic decompression
    /// and throw from the setter, so the guard must never reach it.
    /// </summary>
    private sealed class UnsupportedDecompressionHandler : HttpClientHandler
    {
        public override bool SupportsAutomaticDecompression => false;
    }
}
