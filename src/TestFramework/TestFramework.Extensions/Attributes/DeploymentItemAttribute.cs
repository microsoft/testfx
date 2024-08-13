// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Used to specify deployment item (file or directory) per-test deployment for copying files or folders specified as deployment items to the <see cref="TestContext"/>.DeploymentDirectory.
/// Deployment directory is where all the deployment items are present along with TestSource dll.
/// Can be specified on test class or test method.
/// Can have multiple instances of the attribute to specify more than one item.
/// The item path can be absolute or relative, if relative, it is relative to RunConfig.RelativePathRoot.
/// </summary>
/// <remarks>
/// If specified on a test class, the class needs to contain at least one test method. This means that the
/// attribute cannot be combined with a test class that would contain only a AssemblyInitialize or ClassInitialize
/// method.
/// </remarks>
/// <example>
/// [DeploymentItem("file1.xml")]
/// [DeploymentItem("file2.xml", "DataFiles")]
/// [DeploymentItem("bin\Debug")].
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
#if WINDOWS_UWP || WIN_UI
// Putting this in here so that UWP discovery works. We still do not want users to be using DeploymentItem in the UWP world - Hence making it internal.
// We should separate out DeploymentItem logic in the adapter via a Framework extensibility point.
// Filed https://github.com/Microsoft/testfx/issues/100 to track this.
internal sealed class DeploymentItemAttribute : Attribute
#else
public sealed class DeploymentItemAttribute : Attribute
#endif
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentItemAttribute"/> class.
    /// </summary>
    /// <param name="path">The file or directory to deploy. The path is relative to the build output directory. The item will be copied to the same directory as the deployed test assemblies.</param>
    public DeploymentItemAttribute(string? path)
    {
        Path = path;
        OutputDirectory = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentItemAttribute"/> class.
    /// </summary>
    /// <param name="path">The relative or absolute path to the file or directory to deploy. The path is relative to the build output directory. The item will be copied to the same directory as the deployed test assemblies.</param>
    /// <param name="outputDirectory">The path of the directory to which the items are to be copied. It can be either absolute or relative to the deployment directory. All files and directories identified by <paramref name="path"/> will be copied to this directory.</param>
    public DeploymentItemAttribute(string? path, string? outputDirectory)
    {
        Path = path;
        OutputDirectory = outputDirectory;
    }

    /// <summary>
    /// Gets the path of the source file or folder to be copied.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets the path of the directory to which the item is copied.
    /// </summary>
    public string? OutputDirectory { get; }
}
