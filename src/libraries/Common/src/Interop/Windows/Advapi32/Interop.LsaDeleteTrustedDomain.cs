// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static partial class Interop
{
    internal static partial class Advapi32
    {
        [LibraryImport(Libraries.Advapi32)]
        internal static partial uint LsaDeleteTrustedDomain(SafeLsaPolicyHandle handle, IntPtr pSid);
    }
}
