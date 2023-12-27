// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.FileSystemGlobbing;

namespace MSTest.Performance.Runner.Steps;

internal class MoveFiles : IStep<Files, Files>
{
    private readonly DirectoryInfo _finalFolder;
    private readonly Matcher _matcher = new();

    public string Description => "Move files";

    public MoveFiles(string filter, string finalFolder)
    {
        _matcher.AddInclude(filter);
        _finalFolder = Directory.CreateDirectory(finalFolder);
    }

    public Task<Files> ExecuteAsync(Files payload, IContext context)
    {
        foreach (string file in payload.FilesCollection)
        {
            Console.WriteLine($"File produced: '{file}'");
            if (_matcher.Match(Path.GetFileName(file)).HasMatches)
            {
                Console.WriteLine($"Moving file '{file}' to '{_finalFolder.FullName}'");
                File.Move(file, Path.Combine(_finalFolder.FullName, Path.GetFileName(file)), overwrite: true);
            }
        }

        return Task.FromResult(payload);
    }
}
