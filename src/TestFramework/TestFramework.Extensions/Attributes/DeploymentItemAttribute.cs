// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#if !WINDOWS_UWP && !WIN_UI

/// <summary>
/// Used to specify a deployment item (file or directory) for per-test deployment. The specified files
/// and folders are copied to the <see cref="TestContext"/>.DeploymentDirectory, which is the directory
/// from which the test assembly is executed and where all deployment items are present alongside the
/// test source DLL.
/// Can be specified on a test class or a test method.
/// Can have multiple instances of the attribute to specify more than one item.
/// The item path can be absolute or relative; if relative, it's resolved against the build output
/// directory (the folder that contains the test assembly).
/// </summary>
/// <remarks>
/// If specified on a test class, the class needs to contain at least one test method. This means that the
/// attribute cannot be combined with a test class that would contain only an AssemblyInitialize or
/// ClassInitialize method.
/// </remarks>
/// <example>
/// <code>
/// [DeploymentItem("file1.xml")] // Copy file1.xml from the build output directory to the deployment directory.
/// [DeploymentItem("Resources/file2.xml", "DataFiles")] // Copy file2.xml from the Resources subfolder into a DataFiles subfolder of the deployment directory.
/// [DeploymentItem("TestFiles/")] // Copy all files and subfolders of the TestFiles folder to the deployment directory.
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
// This API should not exist in the netstandard2.0 build, because it's not available in UWP/WinUI.
// This is a binary breaking change, however.
public sealed class DeploymentItemAttribute : Attribute
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
#endif
