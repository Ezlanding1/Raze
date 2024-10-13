using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class InlinedCodeGen
{
    internal readonly struct SaveInlineStateNoInline : IDisposable
    {
        readonly InlinedCodeGen codeGen;
        readonly InlineState? lastInlineState;
        readonly Expr.Function? lastCurrentInlined;

        public SaveInlineStateNoInline(InlinedCodeGen codeGen)
        {
            this.codeGen = codeGen;
            lastInlineState = codeGen.inlineState;
            lastCurrentInlined = codeGen.alloc.currentInlined;

            codeGen.alloc.currentInlined = null;
            codeGen.inlineState = null;
        }

        public void Dispose() => (codeGen.inlineState, codeGen.alloc.currentInlined) = (lastInlineState, lastCurrentInlined);
    }

    internal readonly struct SaveInlineStateInline : IDisposable
    {
        readonly InlinedCodeGen codeGen;
        readonly InlineState? lastInlineState;
        readonly Expr.Function? lastCurrentInlined;

        public SaveInlineStateInline(InlinedCodeGen codeGen, Expr.Function currentInlined)
        {
            this.codeGen = codeGen;
            lastInlineState = codeGen.inlineState;
            codeGen.inlineState = new();
            lastCurrentInlined = codeGen.alloc.currentInlined;

            codeGen.alloc.currentInlined = currentInlined;
        }

        public void Dispose() => (codeGen.inlineState, codeGen.alloc.currentInlined) = (lastInlineState, lastCurrentInlined);
    }
}
