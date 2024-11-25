﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    void GenerateProgramDriver()
    {
        alloc.InitializeFunction(new(new(), false, null, null, null, null, null), this);

        var mainCall = Analyzer.SpecialObjects.GenerateRuntimeCall([], SymbolTableSingleton.SymbolTable.main);
        alloc.Free(mainCall.Accept(this));

        Expr exitParameter =
            Analyzer.Primitives.IsVoidType(SymbolTableSingleton.SymbolTable.main._returnType.type) ?
                new Expr.Literal(new(Parser.LiteralTokenType.Integer, "0", Location.NoLocation)) :
                new Expr.Keyword("EAX");

        alloc.NeededAlloc(AssemblyExpr.Register.RegisterSize._64Bits, this, 0);
        alloc.NullReg(0);
        alloc.Free(
            Analyzer.SpecialObjects.GenerateRuntimeCall(
                [exitParameter],
                Analyzer.TypeCheckUtils.exitFunction.Value
            ).Accept(this)
        );

        alloc.RemoveBlock();
        alloc.fncPushPreserved.GenerateHeader(assembly.text, true);
        alloc.UpContext();
    }
}
