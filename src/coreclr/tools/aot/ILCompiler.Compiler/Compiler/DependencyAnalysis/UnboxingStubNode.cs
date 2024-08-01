// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an unboxing stub that supports calling instance methods on boxed valuetypes.
    /// </summary>
    public partial class UnboxingStubNode : AssemblyStubNode, IMethodNode, ISymbolDefinitionNode
    {
        public MethodDesc Method { get; }

        public override ObjectNodeSection GetSection(NodeFactory factory)
        {
            return factory.Target.IsWindows ?
                ObjectNodeSection.UnboxingStubWindowsContentSection :
                ObjectNodeSection.UnboxingStubUnixContentSection;
        }
        public override bool IsShareable => true;

        public UnboxingStubNode(MethodDesc target)
        {
            Debug.Assert(target.GetCanonMethodTarget(CanonicalFormKind.Specific) == target);
            Debug.Assert(target.OwningType.IsValueType);
            Method = target;
        }

        private ISymbolNode GetUnderlyingMethodEntrypoint(NodeFactory factory)
        {
            ISymbolNode node = factory.MethodEntrypoint(Method);
            return node;
        }

        public override void AppendMangledName(NameMangler nameMangler, ref Utf8StringBuilder sb)
        {
            sb.AppendLiteral("unbox_");
            nameMangler.AppendMangledMethodName(Method, ref sb);
        }

        public static Utf8String GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            // TODO: Choose deduping pattern when there's static calls to get mangled name without materializing a tmp node.
            var sb = new Utf8StringBuilder(stackalloc byte[256]);
            sb.AppendLiteral("unbox_");
            nameMangler.AppendMangledMethodName(method, ref sb);
            return sb.ToUtf8StringAndDispose();
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override int ClassCode => -1846923013;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(Method, ((UnboxingStubNode)other).Method);
        }
    }
}
