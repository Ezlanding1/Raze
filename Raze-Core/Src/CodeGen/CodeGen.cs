﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.Value?>
{
    List<Expr> expressions;
    
    internal Assembly assembly = new();

    private protected int conditionalCount;
    private protected string ConditionalLabel
    {
        get { return CreateConditionalLabel(conditionalCount); }
    }
    private protected string CreateConditionalLabel(int i) => "L" + i;

    public int dataCount;
    public string DataLabel 
    {
        get { return CreateDatalLabel(dataCount); } 
    }
    public string CreateDatalLabel(int i) => "LC" + i;

    internal RegisterAlloc alloc = new();

    AssemblyOps assemblyOps;

    public CodeGen(List<Expr> expressions)
    {
        this.expressions = expressions;
        this.assemblyOps = new(this);
    }
    
    public Assembly Generate()
    {
        SymbolTableSingleton.SymbolTable.RunCodeGenOnImports(this);
        foreach (Expr expr in expressions)
        {
            alloc.Free(expr.Accept(this));
        }
        return assembly;
    }

    public virtual AssemblyExpr.Value? VisitBinaryExpr(Expr.Binary expr)
    {
        return HandleICall(expr);
    }

    public virtual AssemblyExpr.Value? VisitCallExpr(Expr.Call expr)
    {
        return HandleICall(expr);
    }

    private AssemblyExpr.Value HandleICall(Expr.ICall iCall) 
    {
        bool instance = !iCall.InternalFunction.modifiers["static"];

        var localParams = new AssemblyExpr.Register?[Math.Min(iCall.Arguments.Count + Convert.ToInt16(instance), 6)];

        if (instance)
        {
            var call = (Expr.Call)iCall;
            if (!call.constructor)
            {
                if (call.callee != null)
                {
                    var callee = call.callee.Accept(this);
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.AllocParam(0, InstructionUtils.ToRegisterSize(call.callee.GetLastSize()), localParams, this), callee));
                    alloc.Free(callee);
                }
                else
                {
                    var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(call.internalFunction);
                    var size = enclosing.allocSize;
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.AllocParam(0, InstructionUtils.ToRegisterSize(size), localParams, this), new AssemblyExpr.Pointer(8, size)));
                }
            }
            else
            {
                var rbx = alloc.GetRegister(alloc.NameToIdx(AssemblyExpr.Register.RegisterName.RBX), InstructionUtils.SYS_SIZE);
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(InstructionUtils.paramRegister[0], InstructionUtils.SYS_SIZE), rbx));
            }
        }

        for (int i = 0; i < iCall.Arguments.Count; i++)
        {
            AssemblyExpr.Value arg = iCall.Arguments[i].Accept(this)
                .IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)iCall.InternalFunction.parameters[i].stack.size);

            if (i + Convert.ToUInt16(instance) < InstructionUtils.paramRegister.Length)
            {
                if (iCall.InternalFunction.parameters[i].modifiers["ref"])
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.LEA, alloc.AllocParam(Convert.ToInt16(instance) + i, InstructionUtils.SYS_SIZE, localParams, this), arg));
                }
                else
                {
                    var paramReg = alloc.AllocParam(Convert.ToInt16(instance) + i, InstructionUtils.ToRegisterSize(iCall.InternalFunction.parameters[i].stack.size), localParams, this);

                    if (!(arg.IsRegister() && HandleSeteOptimization((AssemblyExpr.Register)arg, paramReg)))
                    {
                        if (arg.IsRegister() && !alloc.IsNeeded(alloc.NameToIdx(((AssemblyExpr.Register)arg).Name)))
                        {
                            alloc.FreeRegister((AssemblyExpr.Register)arg);
                            ((AssemblyExpr.Register)arg).nameBox.Value = paramReg.Name;
                        }
                        else
                        {
                            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, paramReg, arg));
                        }
                    }
                }
            }
            else
            {
                if (iCall.InternalFunction.parameters[i].modifiers["ref"])
                {
                    AssemblyExpr.Register refRegister;
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.LEA, (refRegister = alloc.NextRegister(InstructionUtils.SYS_SIZE)), arg));
                    Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, refRegister));
                    alloc.FreeRegister(refRegister);
                }
                else
                {
                    Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.PUSH, arg));
                }
            }

            alloc.Free(arg);
        }

        alloc.SavePreservedRegistersBeforeCall(this);

        EmitCall(new AssemblyExpr.Unary(AssemblyExpr.Instruction.CALL, new AssemblyExpr.ProcedureRef(ToMangledName(iCall.InternalFunction))));

        if (instance)
        {
            alloc.FreeParameter(0, localParams[0], this);
        }
        for (int i = Convert.ToInt16(instance); i < localParams.Length; i++)
        {
            alloc.FreeParameter(i, localParams[i], this);
        }


        if (iCall.Arguments.Count > InstructionUtils.paramRegister.Length && alloc.fncPushPreserved.leaf)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.ADD, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RSP, InstructionUtils.SYS_SIZE), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes((iCall.Arguments.Count - InstructionUtils.paramRegister.Length) * 8))));
        }

        return alloc.NeededAlloc(InstructionUtils.ToRegisterSize(iCall.InternalFunction._returnType.type.allocSize), this);
    }

    public AssemblyExpr.Value? VisitClassExpr(Expr.Class expr)
    {
        alloc.current = expr;

        foreach (var blockExpr in expr.definitions)
        {
            blockExpr.Accept(this);
        }

        alloc.UpContext();
        return null;
    }

    public AssemblyExpr.Value? VisitDeclareExpr(Expr.Declare expr)
    {
        AssemblyExpr.Value operand = expr.value.Accept(this);
        alloc.AllocateVariable(expr.stack);

        var _ref = expr.stack._ref;

        if (operand == null) return null;

        if (operand.IsPointer())
        {
            AssemblyExpr.Register reg = ((AssemblyExpr.Pointer)operand).AsRegister(this);
            reg.size = _ref ? InstructionUtils.SYS_SIZE : ((AssemblyExpr.Pointer)operand).Size;

            Emit(new AssemblyExpr.Binary(_ref? AssemblyExpr.Instruction.LEA : AssemblyExpr.Instruction.MOV, reg, operand));
            _ref = false;
            operand = reg;
        }
        else if (operand.IsRegister())
        {
            if (SafeGetCmpTypeRaw((AssemblyExpr.Register)operand, out var instruction))
            {
                alloc.Free(instruction.operand);

                if (expr.classScoped)
                {
                    var stackPtr = (AssemblyExpr.Pointer)expr.stack.value;
                    Emit(new AssemblyExpr.Binary(_ref ? AssemblyExpr.Instruction.LEA : AssemblyExpr.Instruction.MOV, alloc.CurrentRegister(InstructionUtils.SYS_SIZE), new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE)));
                    instruction.operand = new AssemblyExpr.Pointer(alloc.CurrentRegister(InstructionUtils.SYS_SIZE), -stackPtr.offset, expr.stack._ref ? InstructionUtils.SYS_SIZE : InstructionUtils.ToRegisterSize(expr.stack.size));
                }
                else
                {
                    instruction.operand = new AssemblyExpr.Pointer(-expr.stack.ValueAsPointer.offset, expr.stack._ref ? InstructionUtils.SYS_SIZE : InstructionUtils.ToRegisterSize(expr.stack.size));
                }
                return null;
            }
        }
        else
        {
            operand = ((AssemblyExpr.ILiteralBase)operand).CreateLiteral((AssemblyExpr.Register.RegisterSize)expr.stack.size);
            AssemblyExpr.Literal literal = (AssemblyExpr.Literal)operand;

            var chunks = ChunkString(literal);
            if (chunks.Item1 != -1)
            {
                if (expr.classScoped)
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.CurrentRegister(InstructionUtils.SYS_SIZE), new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE)));
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(alloc.CurrentRegister(InstructionUtils.SYS_SIZE), -expr.stack.ValueAsPointer.offset, AssemblyExpr.Register.RegisterSize._32Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes(chunks.Item1))));
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(alloc.CurrentRegister(InstructionUtils.SYS_SIZE), -expr.stack.ValueAsPointer.offset, InstructionUtils.ToRegisterSize(expr.stack.size - 4)), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes(chunks.Item2))));
                }
                else
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(-expr.stack.ValueAsPointer.offset -4, AssemblyExpr.Register.RegisterSize._32Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes(chunks.Item1))));
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(-expr.stack.ValueAsPointer.offset, InstructionUtils.ToRegisterSize(expr.stack.size-4)), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes(chunks.Item2))));
                }
                goto dealloc;
            }
        }

        if (expr.classScoped)
        {
            Emit(new AssemblyExpr.Binary(_ref ? AssemblyExpr.Instruction.LEA : AssemblyExpr.Instruction.MOV, alloc.CurrentRegister(InstructionUtils.SYS_SIZE), new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE)));
            Emit(new AssemblyExpr.Binary(_ref ? AssemblyExpr.Instruction.LEA : AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(alloc.CurrentRegister(InstructionUtils.SYS_SIZE), -expr.stack.ValueAsPointer.offset, expr.stack._ref ? InstructionUtils.SYS_SIZE : InstructionUtils.ToRegisterSize(expr.stack.size)), operand));
        }
        else
        {
            Emit(new AssemblyExpr.Binary(_ref ? AssemblyExpr.Instruction.LEA : AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(-expr.stack.ValueAsPointer.offset, expr.stack._ref ? InstructionUtils.SYS_SIZE : InstructionUtils.ToRegisterSize(expr.stack.size)), operand));
        }

        dealloc:
        alloc.Free(operand);
        return null;
    }

    public AssemblyExpr.Value? VisitFunctionExpr(Expr.Function expr)
    {
        if (expr.modifiers["inline"] || expr.dead)
        {
            return null;
        }

        Emit(new AssemblyExpr.Procedure(ToMangledName(expr)));

        alloc.InitializeFunction(expr, this);

        alloc.AllocateParameters(expr, this);

        if (expr.constructor && expr.enclosing?.definitionType == Expr.Definition.DefinitionType.Class)
        {
            alloc.current = (Expr.Class)expr.enclosing;
            alloc.current.size = 0;
            alloc.ListAccept(((Expr.Class)expr.enclosing).declarations, this);
            alloc.current = expr;
        }

        expr.block.Accept(this);

        if (Analyzer.Primitives.IsVoidType(expr._returnType.type) || expr.modifiers["unsafe"])
        {
            DoFooter();
        }

        alloc.RemoveBlock();

        alloc.fncPushPreserved.GenerateHeader(assembly.text);

        alloc.UpContext();
        return null;
    }

    public AssemblyExpr.Value? VisitTypeReferenceExpr(Expr.TypeReference expr)
    {
        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Type accepted in assembler"));
    }

    public AssemblyExpr.Value? VisitAmbiguousGetReferenceExpr(Expr.AmbiguousGetReference expr)
    {
        if (expr.instanceCall)
        {
            AssemblyExpr.Register register = null;

            if (expr.classScoped)
            {
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, (register = alloc.NextRegister(InstructionUtils.SYS_SIZE)), new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE)));
            }

            var stack = expr.datas[0];

            if (stack.inlinedData)
            {
                if (expr.datas.Length == 1)
                {
                    return stack.value.IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)stack.size);
                }
                register = stack.value.NonPointerNonLiteral(this);
            }
            else if (stack._ref)
            {
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.CurrentRegister(InstructionUtils.SYS_SIZE), new AssemblyExpr.Pointer(new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RBP, InstructionUtils.SYS_SIZE), -stack.ValueAsPointer.offset, InstructionUtils.SYS_SIZE)));
                alloc.NullReg();
                register = alloc.NextRegister(InstructionUtils.SYS_SIZE);

                if (expr.datas.Length == 1)
                {
                    return new AssemblyExpr.Pointer(register, 0, stack.size);
                }
            }
            else if (register == null)
            {
                if (expr.datas.Length == 1)
                {
                    return stack.value;
                }
                register = alloc.NextRegister(InstructionUtils.SYS_SIZE);
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, stack.value));
            }
            else
            {
                if (expr.datas.Length == 1)
                {
                    return new AssemblyExpr.Pointer(register, -stack.ValueAsPointer.offset, stack.size);
                }
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, new AssemblyExpr.Pointer(register, -stack.ValueAsPointer.offset, stack.size)));
            }

            for (int i = 1; i < expr.datas.Length; i++)
            {
                if (i == expr.datas.Length - 1)
                {
                    register.size = InstructionUtils.ToRegisterSize(expr.datas[i].size);
                    return new AssemblyExpr.Pointer(register, -expr.datas[i].ValueAsPointer.offset, expr.datas[i].size);
                }
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, new AssemblyExpr.Pointer(register, -expr.datas[i].ValueAsPointer.offset, expr.datas[i].size)));

            }
            return register;
        }
        return null;
    }

    public AssemblyExpr.Value? VisitInstanceGetReferenceExpr(Expr.InstanceGetReference expr)
    {
        AssemblyExpr.Register register;

        var firstGet = expr.getters[0].Accept(this);
        
        if (expr.getters.Count == 1)
        {
            return firstGet;
        }
        if (firstGet.IsPointer())
        {
            register = ((AssemblyExpr.Pointer)firstGet).register;
        }
        else
        {
            register = (AssemblyExpr.Register)firstGet;
        }

        for (int i = 1; i < expr.getters.Count; i++)
        {
            if (i == expr.getters.Count-1)
            {
                register.size = InstructionUtils.ToRegisterSize(((Expr.Get)expr.getters[i]).data.size);
                return new AssemblyExpr.Pointer(register, -((Expr.Get)expr.getters[i]).data.ValueAsPointer.offset, ((Expr.Get)expr.getters[i]).data.size);
            }
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, new AssemblyExpr.Pointer(register, -((Expr.Get)expr.getters[i]).data.ValueAsPointer.offset, ((Expr.Get)expr.getters[i]).data.size)));
        }
        return register;
    }

    public AssemblyExpr.Value? VisitGetExpr(Expr.Get expr)
    {
        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Get accepted in assembler"));
    }

    public AssemblyExpr.Value? VisitLogicalExpr(Expr.Logical expr)
    {
        var operand1 = expr.left.Accept(this);

        if (operand1.IsLiteral())
        {
            operand1 = ((AssemblyExpr.ILiteralBase)operand1).CreateLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
            if ((expr.op.type == Token.TokenType.AND) ^ (IsTrueBooleanLiteral((AssemblyExpr.Literal)operand1)))
            {
                return operand1;
            }
            else
            {
                return expr.right.Accept(this);
            }
        }
        else if (operand1.IsPointer())
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, operand1, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 })));

            Emit(new AssemblyExpr.Unary((expr.op.type == Token.TokenType.AND) ? AssemblyExpr.Instruction.JE : AssemblyExpr.Instruction.JNE, new AssemblyExpr.LocalProcedureRef(ConditionalLabel)));
        }
        else
        {
            if (SafeGetCmpTypeRaw((AssemblyExpr.Register)operand1, out AssemblyExpr.Unary instruction))
            {
                alloc.Free(instruction.operand);
                assembly.text.RemoveAt(assembly.text.Count - 1);

                Emit(new AssemblyExpr.Unary(
                    (expr.op.type == Token.TokenType.AND) ? 
                        InstructionUtils.ConditionalJumpReversed(instruction.instruction) : 
                        InstructionUtils.ConditionalJump(instruction.instruction), 
                    new AssemblyExpr.LocalProcedureRef(ConditionalLabel)));
            }
            else
            {
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.TEST, operand1, operand1));

                Emit(new AssemblyExpr.Unary((expr.op.type == Token.TokenType.AND) ? AssemblyExpr.Instruction.JE : AssemblyExpr.Instruction.JNE, new AssemblyExpr.LocalProcedureRef(ConditionalLabel)));
            }
        }

        alloc.Free(operand1);

        var operand2 = expr.right.Accept(this);

        if (operand2.IsLiteral())
        {
            operand2 = ((AssemblyExpr.ILiteralBase)operand2).CreateLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
            assembly.text.RemoveRange(assembly.text.Count-2, 2);

            if ((expr.op.type == Token.TokenType.AND) ^ (IsTrueBooleanLiteral((AssemblyExpr.Literal)operand2)))
            {
                return operand2;
            }
            else
            {
                return operand1;
            }
        }
        else
        {
            string cLabel = CreateConditionalLabel((expr.op.type == Token.TokenType.AND) ? conditionalCount : conditionalCount + 1);


            if (operand1.IsPointer())
            {
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, operand1, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 })));

                Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JE, new AssemblyExpr.LocalProcedureRef(cLabel)));
            }
            else if (SafeGetCmpTypeRaw((AssemblyExpr.Register)operand2, out AssemblyExpr.Unary instruction))
            {
                alloc.Free((AssemblyExpr.Value)instruction.operand);
                assembly.text.RemoveAt(assembly.text.Count - 1);

                Emit(new AssemblyExpr.Unary(InstructionUtils.ConditionalJumpReversed(instruction.instruction), new AssemblyExpr.LocalProcedureRef(cLabel)));
            }
            else
            {
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.TEST, operand2, operand2));

                Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JE, new AssemblyExpr.LocalProcedureRef(cLabel)));
            }
        }

        alloc.Free(operand2);

        if (expr.op.type == Token.TokenType.OR)
        {
            Emit(new AssemblyExpr.LocalProcedure(ConditionalLabel));

            conditionalCount++;
        }

        conditionalCount++;

        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.CurrentRegister(AssemblyExpr.Register.RegisterSize._8Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 1 })));
       
        Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(ConditionalLabel)));

        Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(conditionalCount-1)));

        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.CurrentRegister(AssemblyExpr.Register.RegisterSize._8Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 })));

        Emit(new AssemblyExpr.LocalProcedure(ConditionalLabel));
        conditionalCount++;
        

        return alloc.NextRegister(AssemblyExpr.Register.RegisterSize._8Bits);
    }

    public AssemblyExpr.Value? VisitGroupingExpr(Expr.Grouping expr)
    {
        return expr.expression.Accept(this);
    }

    public AssemblyExpr.Value? VisitLiteralExpr(Expr.Literal expr)
    {
        switch (expr.literal.type)
        {
            case Parser.LiteralTokenType.RefString:
                string name = DataLabel;
                EmitData(
                    new AssemblyExpr.Data(
                        name,
                        new(AssemblyExpr.Literal.LiteralType.String, AssemblyExpr.ImmediateGenerator.Generate(
                            AssemblyExpr.Literal.LiteralType.String, expr.literal.lexeme, (AssemblyExpr.Register.RegisterSize)(expr.literal.lexeme.Length+1)
                        ))
                    )
                );
                dataCount++;
                return new AssemblyExpr.UnresolvedLiteral(AssemblyExpr.Literal.LiteralType.RefData, name);
            case Parser.LiteralTokenType.String:
            case Parser.LiteralTokenType.Integer:
            case Parser.LiteralTokenType.UnsignedInteger:
            case Parser.LiteralTokenType.Floating:
            case Parser.LiteralTokenType.Binary:
            case Parser.LiteralTokenType.Hex:
            case Parser.LiteralTokenType.Boolean:
                return new AssemblyExpr.UnresolvedLiteral((AssemblyExpr.Literal.LiteralType)expr.literal.type, expr.literal.lexeme);
            default:
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Literal Type ({expr.literal.type})"));
        }
        
    }

    public virtual AssemblyExpr.Value? VisitUnaryExpr(Expr.Unary expr)
    {
        return HandleICall(expr);
    }

    public AssemblyExpr.Value? VisitIfExpr(Expr.If expr)
    {
        bool multiBranch = expr._else != null || expr.conditionals.Count > 1;

        AssemblyExpr.Unary endJump = new AssemblyExpr.Unary(
            AssemblyExpr.Instruction.JMP,
            new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(conditionalCount++))
        );

        for (int i = 0; i < expr.conditionals.Count; i++)
        {
            var condition = expr.conditionals[i].condition.Accept(this);

            AssemblyExpr.Instruction cmpType = HandleConditionalCmpType(condition);

            if (condition.IsLiteral())
            {
                condition = ((AssemblyExpr.ILiteralBase)condition).CreateLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
                if (IsTrueBooleanLiteral((AssemblyExpr.Literal)condition))
                {
                    expr.conditionals[i].block.Accept(this);

                    Emit(new AssemblyExpr.LocalProcedure(ConditionalLabel));
                    endJump.operand = new AssemblyExpr.LocalProcedureRef(ConditionalLabel);

                    conditionalCount++;
                    return null;
                }
                else
                {
                    continue;
                }
            }
            int localConditionalCount = conditionalCount;
            Emit(new AssemblyExpr.Unary(
                InstructionUtils.ConditionalJumpReversed(cmpType),
                new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(conditionalCount++))
            ));

            alloc.Free(condition);

            expr.conditionals[i].block.Accept(this);

            if (multiBranch)
            {
                Emit(endJump);
            }

            Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(localConditionalCount)));
        }

        if (expr._else != null)
        {
            foreach (Expr blockExpr in expr._else.block)
            {
                blockExpr.Accept(this);
            }
        }

        if (multiBranch)
        {
            Emit(new AssemblyExpr.LocalProcedure(((AssemblyExpr.LocalProcedureRef)endJump.operand).Name));
        }

        return null;
    }

    AssemblyExpr.Value? Expr.IVisitor<AssemblyExpr.Value?>.VisitWhileExpr(Expr.While expr)
    {
        int localConditionalCount = conditionalCount;
        conditionalCount+=2;

        Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(localConditionalCount + 1))));

        // Block Label
        Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(localConditionalCount)));

        expr.conditional.block.Accept(this);

        // Condition Label
        Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(localConditionalCount + 1)));

        Emit(new AssemblyExpr.Unary(InstructionUtils.ConditionalJump(HandleConditionalCmpType(expr.conditional.condition.Accept(this))),
            new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(localConditionalCount))));

        return null;
    }

    public AssemblyExpr.Value? VisitForExpr(Expr.For expr)
    {
        int localConditionalCount = conditionalCount;
        conditionalCount += 2;

        expr.initExpr.Accept(this);

        Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JMP, new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(localConditionalCount + 1))));

        // Block Label
        Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(localConditionalCount)));

        expr.conditional.block.Accept(this);
        expr.updateExpr.Accept(this);

        // Condition Label
        Emit(new AssemblyExpr.LocalProcedure(CreateConditionalLabel(localConditionalCount + 1)));

        Emit(new AssemblyExpr.Unary(InstructionUtils.ConditionalJump(HandleConditionalCmpType(expr.conditional.condition.Accept(this))),
            new AssemblyExpr.LocalProcedureRef(CreateConditionalLabel(localConditionalCount))));

        return null;
    }

    public AssemblyExpr.Value? VisitBlockExpr(Expr.Block expr)
    {
        alloc.CreateBlock();
        foreach (Expr blockExpr in expr.block)
        {
            alloc.Free(blockExpr.Accept(this));
        }
        alloc.RemoveBlock();
        return null;
    }

    public virtual AssemblyExpr.Value? VisitReturnExpr(Expr.Return expr)
    {
        if (!expr.IsVoid(alloc.current))
        {
            AssemblyExpr.Value operand = expr.value.Accept(this)
                .IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)Math.Max((int)AssemblyExpr.Register.RegisterSize._32Bits, ((Expr.Function)alloc.current)._returnType.type.allocSize));

            var rax = new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, operand.Size);

            if ((operand.IsRegister() && (((AssemblyExpr.Register)operand).Name != AssemblyExpr.Register.RegisterName.RAX))
                || operand.IsPointer())
            {
                if (operand.Size < AssemblyExpr.Register.RegisterSize._32Bits)
                {
                    Emit(PartialRegisterOptimize(((Expr.Function)alloc.current)._returnType.type, rax, operand));
                }
                else
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, operand));
                }
            }
            else if (operand.IsLiteral())
            {
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, operand));
            }

            alloc.Free(operand);
        }
        else
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, InstructionUtils.SYS_SIZE), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 })));
        }

        DoFooter();

        return null;
    }

    public virtual AssemblyExpr.Value? VisitAssignExpr(Expr.Assign expr)
    {
        AssemblyExpr.Value operand2 = expr.value.Accept(this);
        AssemblyExpr.Value operand1 = expr.member.Accept(this).NonLiteral(this);

        if (operand2 == null) return null;

        if (operand2.IsPointer())
        {
            var reg = alloc.NextRegister(((AssemblyExpr.Pointer)operand2).Size);
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, operand2));
            operand2 = reg;
        }
        else if (operand2.IsRegister())
        {
            if (HandleSeteOptimization((AssemblyExpr.Register)operand2, operand1))
            {
                return null;
            }
        }
        else
        {
            operand2 = ((AssemblyExpr.ILiteralBase)operand2).CreateLiteral((AssemblyExpr.Register.RegisterSize)expr.member.GetLastSize());
            AssemblyExpr.Literal literal = (AssemblyExpr.Literal)operand2;

            if (operand1.IsPointer())
            {
                var chunks = ChunkString(literal);
                if (chunks.Item1 != -1)
                {
                    var ptr = (AssemblyExpr.Pointer)operand1;
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(ptr.register, ptr.offset - 4, AssemblyExpr.Register.RegisterSize._32Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, Encoding.ASCII.GetBytes(chunks.Item1.ToString()))));
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(ptr.register, ptr.offset, InstructionUtils.ToRegisterSize((int)ptr.Size-4)), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, Encoding.ASCII.GetBytes(chunks.Item2.ToString()))));

                    goto dealloc;
                }
            }
        }

        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, operand1, operand2));

        dealloc:
        alloc.Free(operand1);
        alloc.Free(operand2);
        return null;
    }

    public AssemblyExpr.Value? VisitPrimitiveExpr(Expr.Primitive expr)
    {
        alloc.current = expr;
        alloc.ListAccept(expr.definitions, this);
        alloc.UpContext();
        return null;
    }

    public AssemblyExpr.Value? VisitKeywordExpr(Expr.Keyword expr)
    {
        switch (expr.keyword)
        {
            case "null": return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 });
            case "true": return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 1 });
            case "false": return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 });
            default: 
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"'{expr.keyword}' is not a keyword"));
        }
    }
    public AssemblyExpr.Value? VisitAssemblyExpr(Expr.Assembly expr)
    {
        assemblyOps.count = 0;
        assemblyOps.vars = expr.variables;

        foreach (var instruction in expr.block)
        {
            instruction.Assign(assemblyOps);
        }
        return null;
    }

    public AssemblyExpr.Value? VisitNewExpr(Expr.New expr)
    {
        // either dealloc on exit (handled by OS), require manual delete, or implement GC
        alloc.ReserveRegister(this, 0);
        var rax = alloc.GetRegister(alloc.NameToIdx(AssemblyExpr.Register.RegisterName.RAX), AssemblyExpr.Register.RegisterSize._64Bits);
        alloc.ReserveRegister(this, 9);
        var rbx = alloc.GetRegister(alloc.NameToIdx(AssemblyExpr.Register.RegisterName.RBX), AssemblyExpr.Register.RegisterSize._64Bits);
        alloc.fncPushPreserved.IncludeRegister(alloc.NameToIdx(AssemblyExpr.Register.RegisterName.RBX));

        // Move the following into a runtime procedure, and pass in the expr.internalClass.size as a parameter
        // {
        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 12 })));
        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 0 })));
        Emit(new AssemblyExpr.Zero(AssemblyExpr.Instruction.SYSCALL));

        var ptr = new AssemblyExpr.Pointer(rax, -expr.internalClass.CalculateSize(), 8);
        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.LEA, rbx, ptr));

        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.LEA, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits), ptr));
        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, new byte[] { 12 })));
        Emit(new AssemblyExpr.Zero(AssemblyExpr.Instruction.SYSCALL));

        //Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, rbx));
        // }

        alloc.FreeRegister(rax);
        alloc.Free(expr.call.Accept(this));
        return rbx;
    }

    public AssemblyExpr.Value? VisitIsExpr(Expr.Is expr)
    {
        return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Boolean, expr.value == "true"? new byte[] { 1 } : new byte[] { 0 });
    }

    public AssemblyExpr.Value? VisitImportExpr(Expr.Import expr)
    {
        return null;
    }

    public AssemblyExpr.Value? VisitNoOpExpr(Expr.NoOp expr)
    {
        return new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.TMP, AssemblyExpr.Register.RegisterSize._64Bits);
    }

    private void DoFooter()
    {
        alloc.fncPushPreserved.RegisterFooter(assembly.text);
    }

    internal void EmitCall(AssemblyExpr.TextExpr instruction)
    {
        alloc.fncPushPreserved.leaf = false;
        Emit(instruction);
    }

    internal void Emit(AssemblyExpr.TextExpr instruction)
    {
        assembly.text.Add(instruction);
    }

    internal void EmitData(AssemblyExpr.DataExpr instruction)
    {
        assembly.data.Add(instruction);
    }

    internal AssemblyExpr.Instruction HandleConditionalCmpType(AssemblyExpr.Value conditional)
    {
        if (conditional.IsRegister() && SafeGetCmpTypeRaw((AssemblyExpr.Register)conditional, out var res))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);
            return res.instruction;
        }
        else if (!conditional.IsLiteral())
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, conditional, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Boolean, new byte[]{ 1 })));
        }

        return AssemblyExpr.Instruction.SETE;
    }

    internal bool HandleSeteOptimization(AssemblyExpr.Register register, AssemblyExpr.Value newValue)
    {
        if (SafeGetCmpTypeRaw(register, out var instruction))
        {
            alloc.Free((AssemblyExpr.Value)instruction.operand);

            instruction.operand = newValue;

            return true;
        }
        return false;
    }

    internal bool SafeGetCmpTypeRaw(AssemblyExpr.Register register, out AssemblyExpr.Unary? res)
    {
        if (assembly.text[^1] is AssemblyExpr.Unary instruction)
        {
            if (instruction.instruction.ToString()[..3] == "SET" && instruction.operand == register)
            {
                res = instruction;
                return true;
            }
        }
        res = null;
        return false;
    }

    public static string ToMangledName(Expr.Function function)
    {
        return (function.enclosing != null ?
                    function.enclosing.ToString() + "." :
                    "")
                    + function.name.lexeme + getParameters();

        string getParameters()
        {
            string res = "";
            if (function.parameters.Count != 0 && function.parameters[0].typeName.Count == 0)
            {
                foreach (var type in function.parameters)
                {
                    res += (type.stack.type.GetHashCode());
                }
            }
            return res;
        }
    }

    internal static AssemblyExpr.Binary PartialRegisterOptimize(Expr.Type operand1Type, AssemblyExpr.RegisterPointer operand1, AssemblyExpr.Value operand2)
    {
        operand1.size = AssemblyExpr.Register.RegisterSize._32Bits;

        bool signed = Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer].Matches(operand1Type);

        return new AssemblyExpr.Binary(signed ? AssemblyExpr.Instruction.MOVSX : AssemblyExpr.Instruction.MOVZX, operand1, operand2);
    }

    internal static AssemblyExpr.Register.RegisterSize GetIntegralSizeSigned(long value)
    {
        if (value <= sbyte.MaxValue && value >= sbyte.MinValue)
        {
            return AssemblyExpr.Register.RegisterSize._8Bits;
        }
        else if (value <= short.MaxValue && value >= short.MinValue)
        {
            return AssemblyExpr.Register.RegisterSize._16Bits;
        }
        else if (value <= int.MaxValue && value >= int.MinValue)
        {
            return AssemblyExpr.Register.RegisterSize._32Bits;
        }
        return AssemblyExpr.Register.RegisterSize._64Bits;
    }
    internal static AssemblyExpr.Register.RegisterSize GetIntegralSizeUnsigned(ulong value)
    {
        if (value <= byte.MaxValue)
        {
            return AssemblyExpr.Register.RegisterSize._8Bits;
        }
        else if (value <= ushort.MaxValue)
        {
            return AssemblyExpr.Register.RegisterSize._16Bits;
        }
        else if (value <= uint.MaxValue)
        {
            return AssemblyExpr.Register.RegisterSize._32Bits;
        }
        return AssemblyExpr.Register.RegisterSize._64Bits;
    }

    private bool IsTrueBooleanLiteral(AssemblyExpr.Literal literal) => literal.value.Length == 1 && literal.value[0] == 1;

    // MOV M64, IMM64 is not encodable, so the 64-bit value must be chunked into two IMM32s
    private (int, int) ChunkString(AssemblyExpr.Literal literal)
    {
        int size = literal.value.Length;
        if (literal.type != AssemblyExpr.Literal.LiteralType.String || size <= 32)
        {
            return (-1, -1);
        }

        int h1 = literal.value[3];
        for (int i = 2; i >= 0; i--)
        {
            h1 <<= 8;
            h1 += literal.value[i];
        }

        int h2 = literal.value[^1];
        for (int i = size-1; i >= 4; i--)
        {
            h2 <<= 8;
            h2 += literal.value[i];
        }

        return (h2, h1);
    }
}
