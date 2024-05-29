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
            sb.AppendLiteral("Boxed_");
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendMethodTable(TypeDesc type, ref Utf8StringBuilder sb)
        {
            // Use temporary buffer to get the length prefix
            var nameBuffer = new Utf8StringBuilder(stackalloc byte[256]);
            if (type.IsValueType)
                AppendMangledBoxedTypeName(type, ref sb);
            else
                NameMangler.AppendMangledTypeName(type, ref sb);

            sb.AppendLiteral("_ZTV");
            sb.AppendInvariant(nameBuffer.Length);
            sb.Append(nameBuffer.AsSpan());

            nameBuffer.Dispose();
        }

        public sealed override void AppendGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.AppendLiteral("__GCSTATICS");
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendNonGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.AppendLiteral("__NONGCSTATICS");
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendThreadStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append(NameMangler.CompilationUnitPrefix);
            sb.AppendLiteral("__THREADSTATICS");
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendThreadStaticsIndex(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.AppendLiteral("__TypeThreadStaticIndex");
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendTypeGenericDictionary(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.AppendLiteral(GenericDictionaryNamePrefix);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendMethodGenericDictionary(MethodDesc method, ref Utf8StringBuilder sb)
        {
            sb.AppendLiteral(GenericDictionaryNamePrefix);
            NameMangler.AppendMangledMethodName(method, ref sb);
        }

        public sealed override Utf8String ExternMethod(string unmangledName, MethodDesc method)
        {
            return new Utf8String(unmangledName);
        }

        public sealed override Utf8String ExternVariable(Utf8String unmangledName)
        {
            return unmangledName;
        }
    }
}
