﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice.Console;

/// <summary>
/// Console writer that is used when writing ANSI is allowed. It is capable of batching as many updates as possible and writing them at the end,
/// because the terminal is responsible for rendering the colors and control codes.
/// </summary>
internal class AnsiTerminal : ITerminal
{
    private static readonly string[] KnownFileExtensions = new string[]
    {
        ".cs",
        ".vb",
        ".fs",
        ".trx",
        ".coverage",
        ".log",
        ".txt",
    };

    private readonly IConsole _console;
    private readonly string? _baseDirectory;
    private readonly bool _useBusyIndicator;
    private readonly StringBuilder _stringBuilder = new();
    private bool _isBatching;
    private TestProgressFrame _currentFrame = new(Array.Empty<TestProgressState>(), 0, 0);

    public AnsiTerminal(IConsole console, string? baseDirectory)
    {
        _console = console;
        _baseDirectory = baseDirectory ?? Directory.GetCurrentDirectory();

        // Output ansi code to get spinner on top of a terminal, to indicate in-progress task.
        // https://github.com/dotnet/msbuild/issues/8958: iTerm2 treats ;9 code to post a notification instead, so disable progress reporting on Mac.
        _useBusyIndicator = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    public int Width
        => _console.IsOutputRedirected ? int.MaxValue : _console.BufferWidth;

    public int Height
        => _console.IsOutputRedirected ? int.MaxValue : _console.BufferHeight;

    public void Append(char value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void Append(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void AppendLine()
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine();
        }
        else
        {
            _console.WriteLine();
        }
    }

    public void AppendLine(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine(value);
        }
        else
        {
            _console.WriteLine(value);
        }
    }

    public void SetColor(TerminalColor color)
    {
        string setColor = $"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}";
        if (_isBatching)
        {
            _stringBuilder.Append(setColor);
        }
        else
        {
            _console.Write(setColor);
        }
    }

    public void ResetColor()
    {
        string resetColor = AnsiCodes.SetDefaultColor;
        if (_isBatching)
        {
            _stringBuilder.Append(resetColor);
        }
        else
        {
            _console.Write(resetColor);
        }
    }

    public void ShowCursor()
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.ShowCursor);
        }
        else
        {
            _console.Write(AnsiCodes.ShowCursor);
        }
    }

    public void HideCursor()
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.HideCursor);
        }
        else
        {
            _console.Write(AnsiCodes.HideCursor);
        }
    }

    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException(PlatformResources.ConsoleIsAlreadyInBatchingMode);
        }

        _stringBuilder.Clear();
        _isBatching = true;
    }

    public void StopUpdate()
    {
        _console.Write(_stringBuilder.ToString());
        _isBatching = false;
    }

    public void AppendLink(string? path, int? lineNumber)
    {
        if (RoslynString.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // For non code files, point to the directory, so we don't end up running the
        // exe by clicking at the link.
        string? extension = Path.GetExtension(path);
        bool linkToFile = !RoslynString.IsNullOrWhiteSpace(extension) && KnownFileExtensions.Contains(extension);

        bool knownNonExistingFile = path.StartsWith("/_/", ignoreCase: false, CultureInfo.CurrentCulture);

        string linkPath = path;
        if (!linkToFile)
        {
            try
            {
                linkPath = Path.GetDirectoryName(linkPath) ?? linkPath;
            }
            catch
            {
                // Ignore all GetDirectoryName errors.
            }
        }

        // If the output path is under the initial working directory, make the console output relative to that to save space.
        if (_baseDirectory != null && path.StartsWith(_baseDirectory, FileUtilities.PathComparison))
        {
            if (path.Length > _baseDirectory.Length
                && (path[_baseDirectory.Length] == Path.DirectorySeparatorChar
                    || path[_baseDirectory.Length] == Path.AltDirectorySeparatorChar))
            {
                path = path[(_baseDirectory.Length + 1)..];
            }
        }

        if (lineNumber != null)
        {
            path += $":{lineNumber}";
        }

        if (knownNonExistingFile)
        {
            Append(path);
            return;
        }

        // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
        string urlString = linkPath.ToString();
        if (Uri.TryCreate(urlString, UriKind.Absolute, out Uri? uri))
        {
            // url.ToString() un-escapes the URL which is needed for our case file://
            urlString = uri.ToString();
        }

        SetColor(TerminalColor.Gray);
        Append(AnsiCodes.LinkPrefix);
        Append(urlString);
        Append(AnsiCodes.LinkInfix);
        Append(path);
        Append(AnsiCodes.LinkSuffix);
        ResetColor();
    }

    public void MoveCursorUp(int lineCount)
    {
        string moveCursor = $"{AnsiCodes.CSI}{lineCount}{AnsiCodes.MoveUpToLineStart}";
        if (_isBatching)
        {
            _stringBuilder.AppendLine(moveCursor);
        }
        else
        {
            _console.WriteLine(moveCursor);
        }
    }

    public void SetCursorHorizontal(int position)
    {
        string setCursor = AnsiCodes.SetCursorHorizontal(position);
        if (_isBatching)
        {
            _stringBuilder.Append(setCursor);
        }
        else
        {
            _console.Write(setCursor);
        }
    }

    /// <summary>
    /// Erases the previously printed live node output.
    /// </summary>
    public void EraseProgress()
    {
        if (_currentFrame.ProgressCount == 0)
        {
            return;
        }

        AppendLine($"{AnsiCodes.CSI}{_currentFrame.ProgressCount + 1}{AnsiCodes.MoveUpToLineStart}");
        Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        _currentFrame.Clear();
    }

    public void RenderProgress(TestProgressState?[] progress)
    {
        TestProgressFrame newFrame = new(progress, Width, Height);

        // Do not render delta but clear everything if Terminal width or height have changed.
        if (newFrame.Width != _currentFrame.Width || newFrame.Height != _currentFrame.Height)
        {
            EraseProgress();
        }

        StartUpdate();
        try
        {
            newFrame.Render(_currentFrame, this);
        }
        finally
        {
            StopUpdate();
        }

        _currentFrame = newFrame;
    }

    public void StartBusyIndicator()
    {
        if (_useBusyIndicator)
        {
            Append(AnsiCodes.SetBusySpinner);
        }

        HideCursor();
    }

    public void StopBusyIndicator()
    {
        if (_useBusyIndicator)
        {
            Append(AnsiCodes.RemoveBusySpinner);
        }

        ShowCursor();
    }
}
