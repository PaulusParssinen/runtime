// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILLink.Shared.TrimAnalysis;
using Internal.Text;
using Internal.TypeSystem;
using System.Diagnostics;
using System.Globalization;

namespace ILCompiler
{
    public sealed class UnixNodeMangler : NodeMangler
    {
        // Mangled name of boxed version of a type
        public sealed override void AppendMangledBoxedTypeName(TypeDesc type, ref Utf8StringBuilder sb)
        {
            Debug.Assert(type.IsValueType);
            sb.Append("Boxed_"u8);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendMethodTable(TypeDesc type, ref Utf8StringBuilder sb)
        {
            // Use temporary buffer to get the length prefix
            var nameBuffer = new Utf8StringBuilder(stackalloc byte[128]);
            if (type.IsValueType)
                AppendMangledBoxedTypeName(type, ref sb);
            else
                NameMangler.AppendMangledTypeName(type, ref sb);

            sb.Append("_ZTV"u8);
            sb.AppendInvariant(nameBuffer.Length);
            sb.Append(nameBuffer.AsSpan());

            nameBuffer.Dispose();
        }

        public sealed override void AppendGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append("__GCSTATICS"u8);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendNonGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append("__NONGCSTATICS"u8);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendThreadStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append(NameMangler.CompilationUnitPrefix);
            sb.Append("__THREADSTATICS"u8);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendThreadStaticsIndex(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append("__TypeThreadStaticIndex"u8);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendTypeGenericDictionary(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append(GenericDictionaryNamePrefix);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendMethodGenericDictionary(MethodDesc method, ref Utf8StringBuilder sb)
        {
            sb.Append(GenericDictionaryNamePrefix);
            NameMangler.AppendMangledMethodName(method, ref sb);
        }

        public sealed override string ExternMethod(string unmangledName, MethodDesc method)
        {
            return unmangledName;
        }

        public sealed override string ExternVariable(string unmangledName)
        {
            return unmangledName;
        }
    }
}
