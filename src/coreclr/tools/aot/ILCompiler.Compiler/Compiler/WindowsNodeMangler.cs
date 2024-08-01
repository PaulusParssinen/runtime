// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILLink.Shared.TrimAnalysis;
using Internal.Text;
using Internal.TypeSystem;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILCompiler
{
    //
    // The naming format of these names is known to the debugger
    //
    public sealed class WindowsNodeMangler : NodeMangler
    {
        private TargetDetails _target;

        public const string NonGCStaticMemberName = "__NONGCSTATICS";
        public const string GCStaticMemberName = "__GCSTATICS";
        public const string ThreadStaticMemberName = "__THREADSTATICS";
        public const string ThreadStaticIndexName = "__THREADSTATICINDEX";

        public WindowsNodeMangler(TargetDetails target)
        {
            _target = target;
        }

        // Mangled name of boxed version of a type
        public sealed override void AppendMangledBoxedTypeName(TypeDesc type, ref Utf8StringBuilder sb)
        {
            Debug.Assert(type.IsValueType);
            sb.AppendLiteral("Boxed_");
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendMethodTable(TypeDesc type, ref Utf8StringBuilder sb)
        {
            // "??_7TypeName@@6B@" is the C++ mangling for "const TypeName::`vftable'"
            // This, along with LF_VTSHAPE debug records added by the object writer
            // is the debugger magic that allows debuggers to vcast types to their bases.

            sb.AppendLiteral("??_7");
            if (type.IsValueType)
                AppendMangledBoxedTypeName(type, ref sb);
            else
                NameMangler.AppendMangledTypeName(type, ref sb);
            sb.AppendLiteral("@@6B@");
        }

        private void AppendStaticFieldName(TypeDesc type, [ConstantExpected] string fieldName, ref Utf8StringBuilder sb)
        {
            sb.AppendInterpolated($"?{fieldName}@");
            NameMangler.AppendMangledTypeName(type, ref sb);
            sb.AppendLiteral("@@");
        }

        public sealed override void AppendGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            AppendStaticFieldName(type, GCStaticMemberName, ref sb);
        }

        public sealed override void AppendNonGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            AppendStaticFieldName(type, NonGCStaticMemberName, ref sb);
        }

        public sealed override void AppendThreadStatics(TypeDesc type, scoped ref Utf8StringBuilder sb)
        {
            sb.AppendInterpolated($"?{NameMangler.CompilationUnitPrefix}{ThreadStaticMemberName}@");
            NameMangler.AppendMangledTypeName(type, ref sb);
            sb.AppendLiteral("@@");
        }

        public sealed override void AppendThreadStaticsIndex(TypeDesc type, ref Utf8StringBuilder sb)
        {
            AppendStaticFieldName(type, ThreadStaticIndexName, ref sb);
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
            if (_target.Architecture != TargetArchitecture.X86)
            {
                return new Utf8String(unmangledName);
            }

            UnmanagedCallingConventions callConv;
            if (method.IsPInvoke)
            {
                callConv = method.GetPInvokeMethodCallingConventions() & UnmanagedCallingConventions.CallingConventionMask;
            }
            else if (method.IsUnmanagedCallersOnly)
            {
                if (method is not Internal.TypeSystem.Ecma.EcmaMethod)
                    callConv = method.Signature.GetStandaloneMethodSignatureCallingConventions();
                else
                    callConv = method.GetUnmanagedCallersOnlyMethodCallingConventions() & UnmanagedCallingConventions.CallingConventionMask;
            }
            else
            {
                Debug.Assert(method is Internal.TypeSystem.Ecma.EcmaMethod ecmaMethod && (ecmaMethod.GetRuntimeImportName() != null || ecmaMethod.GetRuntimeExportName() != null));
                return new Utf8String(unmangledName);
            }

            int signatureBytes = 0;
            foreach (var p in method.Signature)
            {
                signatureBytes += AlignmentHelper.AlignUp(p.GetElementSize().AsInt, _target.PointerSize);
            }

            Span<byte> scratchBuffer = stackalloc byte[256];
            return callConv switch
            {
                UnmanagedCallingConventions.Stdcall => Utf8String.Create(scratchBuffer, $"_{unmangledName}@{signatureBytes}"),
                UnmanagedCallingConventions.Fastcall => Utf8String.Create(scratchBuffer, $"@{unmangledName}@{signatureBytes}"),
                UnmanagedCallingConventions.Cdecl => Utf8String.Create(scratchBuffer, $"_{unmangledName}"),
                _ => throw new System.NotImplementedException()
            };
        }

        public sealed override Utf8String ExternVariable(Utf8String unmangledName)
        {
            if (_target.Architecture != TargetArchitecture.X86)
            {
                return unmangledName;
            }

            return Utf8String.Create(stackalloc byte[128], $"_{unmangledName}");
        }
    }
}
