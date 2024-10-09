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

        public SaveInlineStateNoInline(InlinedCodeGen codeGen)
        {
            this.codeGen = codeGen;
            lastInlineState = codeGen.inlineState;
            codeGen.inlineState = null;
        }

        public void Dispose() => codeGen.inlineState = lastInlineState;
    }

    internal readonly struct SaveInlineStateInline : IDisposable
    {
        readonly InlinedCodeGen codeGen;
        readonly InlineState? lastInlineState;

        public SaveInlineStateInline(InlinedCodeGen codeGen, Expr.Function currentInlined)
        {
            this.codeGen = codeGen;
            lastInlineState = codeGen.inlineState;
            codeGen.inlineState = new(currentInlined);
        }

        public void Dispose() => codeGen.inlineState = lastInlineState;
    }
}
