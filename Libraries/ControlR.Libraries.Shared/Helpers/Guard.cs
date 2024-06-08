// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// https://github.com/CommunityToolkit/dotnet/blob/main/License.md

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Helpers;

public static class Guard
{
    public static void IsNotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        if (value is not null)
        {
            return;
        }

        throw new ArgumentNullException(name);
    }

    public static void IsNotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string name = "")
        where T : struct
    {
        if (value is not null)
        {
            return;
        }

        throw new ArgumentNullException(name);
    }

    public static void IsNotNullOrEmpty([NotNull] string? text, [CallerArgumentExpression(nameof(text))] string name = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            return;
        }

        throw new ArgumentNullException(name);
    }

    public static void IsNotNullOrWhiteSpace([NotNull] string? text, [CallerArgumentExpression(nameof(text))] string name = "")
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        throw new ArgumentNullException(name);
    }
}