// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// The file operations.
/// </summary>
internal sealed class FileOperations : IFileOperations
{
    private readonly ConcurrentDictionary<string, Assembly> _assemblyCache = new();

#if WIN_UI
    private readonly bool _isPackaged = AppModel.IsPackagedProcess();
#endif

    /// <summary>
    /// Loads an assembly.
    /// </summary>
    /// <param name="assemblyName"> The assembly name. </param>
    /// <param name="isReflectionOnly">Indicates whether this should be a reflection only load.</param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
    public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly)
    {
#if NETSTANDARD || NETCOREAPP || WINDOWS_UWP
#if WIN_UI
        if (!_isPackaged && Path.IsPathRooted(assemblyName))
        {
            return Assembly.LoadFrom(assemblyName);
        }
#endif
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
        // Do NOT use `new AssemblyName(fileNameWithoutExtension)` here. The provided string is a FullName of assembly and needs to be properly escaped (but there is no utility for it).
        // To correctly construct AssemblyName from file name, we need to just set the Name, and it will be escaped correct when constructing FullName.
        // https://github.com/dotnet/runtime/blob/da322a2260bcb07347df3082fca211987ec8f2fc/src/libraries/Common/src/System/Reflection/AssemblyNameFormatter.cs#L120
        // When we did it wrong the exception thrown is "The given assembly name was invalid." for files that have `=` or any other special characters in their names (see the AssemblyNameFormatter.cs code).
        Assembly assembly = _assemblyCache.GetOrAdd(fileNameWithoutExtension, fileNameWithoutExtension => Assembly.Load(new AssemblyName { Name = fileNameWithoutExtension }));

        return assembly;
#elif NETFRAMEWORK
        if (isReflectionOnly)
        {
            return Assembly.ReflectionOnlyLoadFrom(assemblyName);
        }
        else
        {
            Assembly assembly = _assemblyCache.GetOrAdd(assemblyName, Assembly.LoadFrom);
            return assembly;
        }
#endif
    }

    /// <summary>
    /// Verifies if file exists in context.
    /// </summary>
    /// <param name="assemblyFileName"> The assembly file name. </param>
    /// <returns> true if file exists. </returns>
    /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
    public bool DoesFileExist(string assemblyFileName)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI && !WINDOWS_UWP)
        // For projectK these assemblies can be created on the fly which means the file might not exist on disk.
        // Depend on Assembly Load failures instead of this validation.
        return true;
#elif NETFRAMEWORK
        return (SafeInvoke(() => File.Exists(assemblyFileName)) as bool?) ?? false;
#elif WINDOWS_UWP
        bool fileExists = false;

        try
        {
            string fileNameWithoutPath = Path.GetFileName(assemblyFileName);
            Task<Windows.Storage.StorageFile> searchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileNameWithoutPath).AsTask();
            searchTask.Wait();
            fileExists = searchTask.Result != null;
        }
        catch (Exception)
        {
            // ignore
        }

        return fileExists;
#elif WIN_UI
        string path = GetFullFilePath(assemblyFileName);
        return File.Exists(path);
#endif
    }

    /// <summary>
    /// Gets the full file path of an assembly file.
    /// </summary>
    /// <param name="assemblyFileName"> The file name. </param>
    /// <returns> The full file path. </returns>
    public string GetFullFilePath(string assemblyFileName)
    {
#if NETSTANDARD || (NETCOREAPP && !WIN_UI) || WINDOWS_UWP
        return assemblyFileName;
#elif WIN_UI
        string? packagePath = AppModel.GetCurrentPackagePath();
        return packagePath == null ? assemblyFileName : Path.Combine(packagePath, assemblyFileName);
#elif NETFRAMEWORK

        return (SafeInvoke(() => Path.GetFullPath(assemblyFileName)) as string) ?? assemblyFileName;
#endif
    }

#if NETFRAMEWORK
    private static object? SafeInvoke<T>(Func<T> action, string? messageFormatOnException = null)
    {
        try
        {
            return action.Invoke();
        }
        catch (Exception exception)
        {
            if (StringEx.IsNullOrEmpty(messageFormatOnException))
            {
                messageFormatOnException = "{0}";
            }

            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, messageFormatOnException, exception.Message);
        }

        return null;
    }
#endif

}
