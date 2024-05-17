// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILLink.Shared.TrimAnalysis;
using Internal.Text;
using Internal.TypeSystem;
using System;
using System.Diagnostics;

namespace ILCompiler
{
    //
    // The naming format of these names is known to the debugger
    //
    public class WindowsNodeMangler : NodeMangler
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
            sb.Append("Boxed_"u8);
            NameMangler.AppendMangledTypeName(type, ref sb);
        }

        public sealed override void AppendMethodTable(TypeDesc type, ref Utf8StringBuilder sb)
        {
            // "??_7TypeName@@6B@" is the C++ mangling for "const TypeName::`vftable'"
            // This, along with LF_VTSHAPE debug records added by the object writer
            // is the debugger magic that allows debuggers to vcast types to their bases.

            sb.Append("??_7"u8);
            if (type.IsValueType)
                AppendMangledBoxedTypeName(type, ref sb);
            else
                NameMangler.AppendMangledTypeName(type, ref sb);
            sb.Append("@@6B@"u8);
        }

        private void AppendStaticFieldName(TypeDesc type, ReadOnlySpan<byte> fieldName, ref Utf8StringBuilder sb)
        {
            sb.Append('?');
            sb.Append(fieldName);
            sb.Append('@');
            NameMangler.AppendMangledTypeName(type, ref sb);
            sb.Append("@@"u8);
        }

        public sealed override void AppendGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            AppendStaticFieldName(type, "__GCSTATICS"u8, ref sb);
        }

        public sealed override void AppendNonGCStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            AppendStaticFieldName(type, "__NONGCSTATICS"u8, ref sb);
        }

        public sealed override void AppendThreadStatics(TypeDesc type, ref Utf8StringBuilder sb)
        {
            sb.Append('?');
            sb.Append(NameMangler.CompilationUnitPrefix);
            sb.Append(ThreadStaticMemberName);
            sb.Append('@');
            NameMangler.AppendMangledTypeName(type, ref sb);
            sb.Append("@@"u8);
        }

        public sealed override void AppendThreadStaticsIndex(TypeDesc type, ref Utf8StringBuilder sb)
        {
            AppendStaticFieldName(type, "__THREADSTATICINDEX"u8, ref sb);
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
            if (_target.Architecture != TargetArchitecture.X86)
            {
                return unmangledName;
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
                return unmangledName;
            }

            int signatureBytes = 0;
            foreach (var p in method.Signature)
            {
                signatureBytes += AlignmentHelper.AlignUp(p.GetElementSize().AsInt, _target.PointerSize);
            }

            return callConv switch
            {
                UnmanagedCallingConventions.Stdcall => $"_{unmangledName}@{signatureBytes}",
                UnmanagedCallingConventions.Fastcall => $"@{unmangledName}@{signatureBytes}",
                UnmanagedCallingConventions.Cdecl => $"_{unmangledName}",
                _ => throw new System.NotImplementedException()
            };
        }

        public sealed override string ExternVariable(string unmangledName)
        {
            if (_target.Architecture != TargetArchitecture.X86)
            {
                return unmangledName;
            }

            return $"_{unmangledName}";
        }
    }
}
