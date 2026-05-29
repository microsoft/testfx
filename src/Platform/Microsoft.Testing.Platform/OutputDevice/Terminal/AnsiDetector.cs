// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Portions of the code in this file were ported from the spectre.console by Patrik Svensson, Phil Scott, Nils Andresen
// https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/Backends/Ansi/AnsiDetector.cs
// and from the supports-ansi project by Qingrong Ke
// https://github.com/keqingrong/supports-ansi/blob/master/index.js
namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Works together with the <see cref="NativeMethods"/> to figure out if the current console is capable of using ANSI output codes.
/// </summary>
internal static class AnsiDetector
{
    public static bool IsAnsiSupported(string? termType)
    {
        if (RoslynString.IsNullOrEmpty(termType))
        {
            return false;
        }

        // Each condition mirrors the original regex pattern but uses plain string operations,
        // eliminating the 17 Regex allocations and their compilation cost.
        return termType.StartsWith("xterm", StringComparison.Ordinal) // ^xterm — xterm, PuTTY, Mintty
            || termType.StartsWith("rxvt", StringComparison.Ordinal) // ^rxvt — RXVT
            || (termType.Contains("eterm", StringComparison.Ordinal) // ^(?!eterm-color).*eterm.* — eterm but not eterm-color (#9950)
                && !termType.StartsWith("eterm-color", StringComparison.Ordinal))
            || termType.StartsWith("screen", StringComparison.Ordinal) // ^screen — GNU screen, tmux
            || termType.Contains("tmux", StringComparison.Ordinal) // tmux — tmux
            || termType.StartsWith("vt100", StringComparison.Ordinal) // ^vt100 — DEC VT series
            || termType.StartsWith("vt102", StringComparison.Ordinal) // ^vt102 — DEC VT series
            || termType.StartsWith("vt220", StringComparison.Ordinal) // ^vt220 — DEC VT series
            || termType.StartsWith("vt320", StringComparison.Ordinal) // ^vt320 — DEC VT series
            || termType.Contains("ansi", StringComparison.Ordinal) // ansi — ANSI
            || termType.Contains("scoansi", StringComparison.Ordinal) // scoansi — SCO ANSI
            || termType.Contains("cygwin", StringComparison.Ordinal) // cygwin — Cygwin, MinGW
            || termType.Contains("linux", StringComparison.Ordinal) // linux — Linux console
            || termType.Contains("konsole", StringComparison.Ordinal) // konsole — Konsole
            || termType.Contains("bvterm", StringComparison.Ordinal) // bvterm — Bitvise SSH Client
            || termType.StartsWith("st-256color", StringComparison.Ordinal) // ^st-256color — Suckless Simple Terminal, st
            || termType.Contains("alacritty", StringComparison.Ordinal); // alacritty — Alacritty
    }
}
