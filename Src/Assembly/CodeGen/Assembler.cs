using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class Assembler : Expr.IVisitor<Instruction.Value?>
{
    List<Expr> expressions;
    public List<Instruction> data;
    List<Instruction> instructions;

    bool leaf = true;

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

    private protected Token.TokenType lastJump;

    public RegisterAlloc alloc;

    AssemblyOps assemblyOps = null;

    public Assembler(List<Expr> expressions)
    {
        this.expressions = expressions;
        this.data = new();
        data.Add(new Instruction.Section("data"));
        this.instructions = new();
        this.conditionalCount = 0;
        this.alloc = new();
        this.dataCount = 0;
    }
    
    internal (List<Instruction>, List<Instruction>) Assemble()
    {
        foreach (Expr expr in expressions)
        {
            expr.Accept(this);
            alloc.FreeAll();
        }
        return (instructions, data);
    }

    public virtual Instruction.Value? visitBinaryExpr(Expr.Binary expr)
    {
        var localParams = new Instruction.Register[2];

        Instruction.Value operand1 = expr.left.Accept(this);
        emit(new Instruction.Binary("MOV", (localParams[0] = alloc.AllocParam(0, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[0].stack.size))), operand1));

        alloc.Free(operand1);

        Instruction.Value operand2 = expr.right.Accept(this);
        emit(new Instruction.Binary("MOV", (localParams[1] = alloc.AllocParam(1, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[1].stack.size))), operand2));

        alloc.Free(operand2);

        for (int i = 0; i < 2; i++)
        {
            if (localParams[i] != null)
            {
                alloc.FreeParameter(i, localParams[i], this);
            }
        }

        alloc.ReserveRegister(this);

        if (InstructionUtils.ConditionalJump.ContainsKey(expr.op.type)) 
        {
            lastJump = expr.op.type;
        }

        emitCall(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangedName(expr.internalFunction))));

        return alloc.CallAlloc(InstructionUtils.ToRegisterSize(expr.internalFunction._returnSize));
    }

    public virtual Instruction.Value? visitCallExpr(Expr.Call expr)
    {
        bool instance = !expr.internalFunction.modifiers["static"];

        var localParams = new Instruction.Register?[Math.Min(expr.arguments.Count + Convert.ToInt16(instance), 6)];

        for (int i = 0; i < expr.arguments.Count; i++)
        {
            Instruction.Value arg = expr.arguments[i].Accept(this);

            if (i + Convert.ToUInt16(instance) < InstructionUtils.paramRegister.Length)
            {
                if (expr.internalFunction.parameters[i].modifiers["ref"])
                {
                    emit(new Instruction.Binary("LEA", alloc.AllocParam(Convert.ToInt16(instance) + i, Instruction.Register.RegisterSize._64Bits), arg));
                }
                else
                {
                    emit(new Instruction.Binary("MOV", alloc.AllocParam(Convert.ToInt16(instance) + i, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[i].stack.size)), arg));
                }
                localParams[Convert.ToInt16(instance) + i] = alloc.paramRegisters[Convert.ToInt16(instance) + i];
            }
            else
            {
                if (expr.internalFunction.parameters[i].modifiers["ref"])
                {
                    Instruction.Register refRegister;
                    emit(new Instruction.Binary("LEA", (refRegister = alloc.NextRegister(Instruction.Register.RegisterSize._64Bits)), arg));
                    emit(new Instruction.Unary("PUSH", refRegister));
                    alloc.FreeRegister(refRegister);
                }
                else
                {
                    emit(new Instruction.Unary("PUSH", arg));
                }
            }

            alloc.Free(arg);
        }

        if (instance)
        {
            if (!expr.constructor)
            {
                if (expr.callee != null)
                {
                    for (int i = expr.offsets.Length - 1; i >= 1; i--)
                    {
                        emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(((i == expr.offsets.Length - 1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits)), expr.offsets[i].stackOffset, 8)));
                    }
                    emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[0], Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer((0 == expr.offsets.Length - 1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.offsets[0].stackOffset, 8)));
                }
                else
                {
                    emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[0], Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(8,8)));
                }
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[0], Instruction.Register.RegisterSize._64Bits), new Instruction.Register(Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits)));
            }
        }

        for (int i = Convert.ToInt16(instance); i < localParams.Length; i++)
        {
            if (localParams[i] != null)
            {
                alloc.FreeParameter(i, localParams[i], this);
            }
        }

        alloc.ReserveRegister(this);

        emitCall(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangedName(expr.internalFunction))));

        if (expr.arguments.Count > InstructionUtils.paramRegister.Length && leaf)
        {
            emit(new Instruction.Binary("ADD", new Instruction.Register(Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], ((expr.arguments.Count - InstructionUtils.paramRegister.Length) * 8).ToString())));
        }
        
        //if (expr.get != null)
        return alloc.CallAlloc(InstructionUtils.ToRegisterSize(expr.internalFunction._returnSize));

        //return this.visitGetReferenceExpr(expr.get);
    }

    public Instruction.Value? visitClassExpr(Expr.Class expr)
    {
        foreach (var blockExpr in expr.definitions)
        {
            blockExpr.Accept(this);
        }
        
        return null;
    }

    public Instruction.Value? visitDeclareExpr(Expr.Declare expr)
    {
        Instruction.Value operand = expr.value.Accept(this);

        var _ref = expr.stack._ref;

        if (operand.IsPointer())
        {
            var reg = alloc.CurrentRegister(_ref? Instruction.Register.RegisterSize._64Bits : ((Instruction.Pointer)operand).size);
            emit(new Instruction.Binary(_ref? "LEA" : "MOV", reg, operand));
            _ref = false;
            operand = reg;
        }

        if (expr.classScoped)
        {
            emit(new Instruction.Binary(_ref ? "LEA" : "MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
            emit(new Instruction.Binary(_ref ? "LEA" : "MOV", new Instruction.Pointer(alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.stack.stackOffset, expr.stack._ref ? 8 : expr.stack.size), operand));
        }
        else
        {
            emit(new Instruction.Binary(_ref ? "LEA" : "MOV", new Instruction.Pointer(expr.stack.stackOffset, expr.stack._ref ? 8 : expr.stack.size), operand));
        }

        alloc.Free(operand);

        return null;
    }

    public Instruction.Value? visitFunctionExpr(Expr.Function expr)
    {
        if (expr.modifiers["inline"] && !expr.modifiers["operator"])
        {
            return null;
        }

        leaf = true;

        emit(new Instruction.Procedure(ToMangedName(expr)));

        alloc.fncPushPreserved = new bool[5];
        int fncStartIdx = instructions.Count;

        int count = 0;

        if (!expr.modifiers["static"])
        {
            emit(new Instruction.Binary("MOV", new Instruction.Pointer(8, 8), new Instruction.Register(InstructionUtils.paramRegister[0], 8)));
            count++;
        }

        for (int i = 0, len = Math.Min(expr.arity, InstructionUtils.paramRegister.Length-count); i < len; i++)
        {
            var paramExpr = expr.parameters[i];

            if (paramExpr.modifiers["ref"])
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.stack.stackOffset, 8), new Instruction.Register(InstructionUtils.paramRegister[i + count], 8)));
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.stack.stackOffset, paramExpr.stack.size), new Instruction.Register(InstructionUtils.paramRegister[i+count], paramExpr.stack.size)));
            }
        }

        if (expr.constructor && expr.enclosing?.definitionType == Expr.Definition.DefinitionType.Class)
        {
            alloc.ListAccept(((Expr.Class)expr.enclosing).declarations, this);
        }

        foreach (var blockExpr in expr.block)
        {
            blockExpr.Accept(this);
            alloc.FreeAll();
        }
        
        bool leafFunc = (leaf || expr.size == 0) && expr.size <= 128;

        if (!leafFunc)
        {
            emitAt(new Instruction.Unary("PUSH", Instruction.Register.RegisterName.RBP), fncStartIdx++);
            emitAt(new Instruction.Binary("MOV", Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterName.RSP), fncStartIdx++);

            if (expr.size > 128)
            {
                emitAt(new Instruction.StackAlloc("SUB", new Instruction.Register(Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], (expr.size - 128).ToString())), fncStartIdx);
            }
            else
            {
                emitAt(new Instruction.StackAlloc("SUB", new Instruction.Register(Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], expr.size.ToString())), fncStartIdx++);
            }
        }
        else
        {
            emitAt(new Instruction.Unary("PUSH", Instruction.Register.RegisterName.RBP), fncStartIdx++);
            emitAt(new Instruction.Binary("MOV", Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterName.RSP), fncStartIdx++);
        }
        leaf = leafFunc;

        for (int i = 0; i < alloc.fncPushPreserved.Length; i++)
        {
            if (alloc.fncPushPreserved[i] == true)
            {
                emitAt(new Instruction.Unary("PUSH", InstructionUtils.storageRegisters[i+1]), fncStartIdx);
            }
        }

        if (Analyzer.Primitives.IsVoidType(expr._returnType.type) || expr.modifiers["unsafe"])
        {
            DoFooter();
        }
        return null;
    }

    public Instruction.Value? visitTypeReferenceExpr(Expr.TypeReference expr)
    {
        throw new Errors.ImpossibleError("Type accepted in assembler");
    }

    public Instruction.Value? visitGetReferenceExpr(Expr.GetReference expr)
    {
        for (int i = expr.offsets.Length-1; i >= 0; i--)
        {
            emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer((i == expr.offsets.Length-1) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.offsets[i].stackOffset, 8)));
        }
        return null;
    }

    public Instruction.Value? visitGroupingExpr(Expr.Grouping expr)
    {
        return expr.expression.Accept(this);
    }

    public Instruction.Value? visitLiteralExpr(Expr.Literal expr)
    {
        switch (expr.literal.type)
        {
            case Token.TokenType.STRING:
                string name = DataLabel;
                emitData(new Instruction.Data(name, InstructionUtils.dataSize[1], expr.literal.lexeme + ", 0"));
                dataCount++;
                return new Instruction.Literal(expr.literal.type, name);
            case Token.TokenType.INTEGER:
            case Token.TokenType.FLOATING:
            case Token.TokenType.BINARY:
            case Token.TokenType.HEX:
            case Token.TokenType.BOOLEAN:
                return new Instruction.Literal(expr.literal.type, expr.literal.lexeme);
            default:
                throw new Errors.ImpossibleError($"Invalid Literal Type ({expr.literal.type})");
        }
        
    }

    public virtual Instruction.Value? visitUnaryExpr(Expr.Unary expr)
    {
        Instruction.Register param;

        Instruction.Value operand = expr.operand.Accept(this);
        emit(new Instruction.Binary("MOV", (param = alloc.AllocParam(0, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[0].stack.size))), operand));

        alloc.Free(operand);

        if (param != null)
        {
            alloc.FreeParameter(0, param, this);
        }

        alloc.ReserveRegister(this);

        emitCall(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangedName(expr.internalFunction))));

        return alloc.CallAlloc(InstructionUtils.ToRegisterSize(expr.internalFunction._returnSize));
    }

    public Instruction.Value? visitVariableExpr(Expr.Variable expr)
    {
        if (expr.classScoped)
        {
            emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
        }

        var reg = expr.offsets[expr.offsets.Length - 1].stackRegister ? ((Expr.StackRegister)expr.offsets[expr.offsets.Length - 1]).register : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits);

        for (int i = expr.offsets.Length - 1; i >= 1; i--)
        {
            if (reg.IsRegister())
            {
                if (expr.offsets[i].stackRegister)
                {
                    if (i == 1)
                    {
                        return new Instruction.Pointer((Instruction.Register)reg, expr.stack.stackOffset, expr.stack.size, expr.stack.plus ? '+' : '-');
                    }
                    i--;
                }
                else
                {
                    emit(new Instruction.Binary("MOV", reg, new Instruction.Pointer(((i == expr.offsets.Length - 1) && !expr.classScoped) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : (Instruction.Register)reg, expr.offsets[i].stackOffset, 8)));
                }
            }
            else if (reg.IsPointer())
            {
                if (((Instruction.Pointer)reg).register.name == Instruction.Register.RegisterName.RBP)
                {
                    emit(new Instruction.Binary("MOV", alloc.CurrentRegister(((Instruction.Pointer)reg).size), reg));
                }
                else
                {
                    emit(new Instruction.Binary("MOV", ((Instruction.Pointer)reg).register, reg));
                }
            }
            else if (reg.IsLiteral())
            {
                throw new Errors.ImpossibleError("literal is stackRegister in variable");
            }
        }

        if (expr.stack.stackRegister)
        {
            return reg;
        }

        if (!expr.stack._ref)
        {
            return new Instruction.Pointer((expr.offsets.Length == 1 && !expr.classScoped) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.NextRegister(InstructionUtils.ToRegisterSize(expr.stack.size)), expr.stack.stackOffset, expr.stack.size, expr.stack.plus ? '+' : '-');
        }
        else
        {
            emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(new Instruction.Register(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits), expr.stack.stackOffset, 8, expr.stack.plus ? '+' : '-')));
            return new Instruction.Pointer(alloc.NextRegister(Instruction.Register.RegisterSize._64Bits), 0, expr.stack.size);
        }
    }

    public Instruction.Value? visitIfExpr(Expr.If expr)
    {
        expr.conditional.condition.Accept(this);
        var fJump = new Instruction.Unary(InstructionUtils.ConditionalJumpReversed[lastJump], Instruction.Register.RegisterName.TMP);
        emit(fJump);

        expr.conditional.block.Accept(this);


        var tJump = new Instruction.Unary("JMP", Instruction.Register.RegisterName.TMP);
        emit(tJump);


        foreach (Expr.ElseIf elif in expr.ElseIfs)
        {
            fJump.operand = new Instruction.LocalProcedureRef(ConditionalLabel);
            emit(new Instruction.LocalProcedure(ConditionalLabel));
            conditionalCount++;

            elif.conditional.condition.Accept(this);

            fJump = new Instruction.Unary(InstructionUtils.ConditionalJumpReversed[lastJump], Instruction.Register.RegisterName.TMP);

            emit(fJump);
            foreach (Expr blockExpr in elif.conditional.block.block)
            {
                blockExpr.Accept(this);
            }

            emit(tJump);
        }

        fJump.operand = new Instruction.LocalProcedureRef(ConditionalLabel);
        emit(new Instruction.LocalProcedure(ConditionalLabel));
        conditionalCount++;
        if (expr._else != null)
        {
            foreach (Expr blockExpr in expr._else.conditional.block.block)
            {
                blockExpr.Accept(this);
            }
        }
        emit(new Instruction.LocalProcedure(ConditionalLabel));
        tJump.operand = new Instruction.LocalProcedureRef(ConditionalLabel);

        conditionalCount++;
        return null;
    }

    public Instruction.Value? visitWhileExpr(Expr.While expr)
    {
        emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(ConditionalLabel)));

        var conditional = new Instruction.LocalProcedure(ConditionalLabel);

        conditionalCount++;

        emit(new Instruction.LocalProcedure(ConditionalLabel));

        expr.conditional.block.Accept(this);

        emit(conditional);
        expr.conditional.condition.Accept(this);
        emit(new Instruction.Unary(InstructionUtils.ConditionalJump[lastJump], new Instruction.LocalProcedureRef(ConditionalLabel)));
        conditionalCount++;

        return null;
    }

    public Instruction.Value? visitForExpr(Expr.For expr)
    {
        expr.initExpr.Accept(this);
        emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(ConditionalLabel)));

        var conditional = new Instruction.LocalProcedure(ConditionalLabel);

        conditionalCount++;

        emit(new Instruction.LocalProcedure(ConditionalLabel));

        expr.conditional.block.Accept(this);
        expr.updateExpr.Accept(this);

        emit(conditional);
        expr.conditional.condition.Accept(this);
        emit(new Instruction.Unary(InstructionUtils.ConditionalJump[lastJump], new Instruction.LocalProcedureRef(ConditionalLabel)));
        conditionalCount++;

        return null;
    }

    public Instruction.Value? visitBlockExpr(Expr.Block expr)
    {
        foreach (Expr blockExpr in expr.block)
        {
            blockExpr.Accept(this);
            alloc.FreeAll();
        }
        return null;
    }

    public virtual Instruction.Value? visitReturnExpr(Expr.Return expr)
    {
        if (!expr._void)
        {
            Instruction.Value operand = expr.value.Accept(this);

            if (operand.IsRegister())
            {
                var op = (Instruction.Register)operand;
                if (op.name != Instruction.Register.RegisterName.RAX)
                    emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, op.size), operand));
            }
            else if (operand.IsPointer())
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, ((Instruction.SizedValue)operand).size), operand));
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, InstructionUtils.ToRegisterSize(expr.size)), operand));
            }

            alloc.Free(operand);
        }
        else
        {
            emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "0")));
        }
        
        DoFooter();

        return null;
    }

    public virtual Instruction.Value? visitAssignExpr(Expr.Assign expr)
    {
        Instruction.Value operand2 = expr.value.Accept(this);
        Instruction.Value operand1 = expr.member.Accept(this);

        if (operand2.IsPointer())
        {
            Instruction.Register.RegisterName regName;
            var reg = alloc.NextRegister(((Instruction.Pointer)operand2).size);
            emit(new Instruction.Binary("MOV", reg, operand2));
            operand2 = reg;
        }

        emit(new Instruction.Binary("MOV", operand1, operand2));

        alloc.Free(operand1);
        alloc.Free(operand2);
        return null;
    }

    public Instruction.Value? visitPrimitiveExpr(Expr.Primitive expr)
    {
        alloc.ListAccept(expr.definitions, this);

        return null;
    }

    public Instruction.Value? visitKeywordExpr(Expr.Keyword expr)
    {
        switch (expr.keyword)
        {
            case "null":
                return new Instruction.Literal(Parser.Literals[0], "0");
            case "true":
                return new Instruction.Literal(Parser.Literals[0], "1");
            case "false":
                return new Instruction.Literal(Parser.Literals[0], "0");
            default:
                throw new Errors.ImpossibleError($"'{expr.keyword}' is not a keyword");
        }
    }

    public Instruction.Value? visitAssemblyExpr(Expr.Assembly expr)
    {
        assemblyOps ??= new(this);
        var count = assemblyOps.count;
        var variables = assemblyOps.vars;
        assemblyOps.count = 0;
        assemblyOps.vars = expr.variables;

        foreach (var instruction in expr.block)
        {
            instruction.Assign(assemblyOps);
        }

        assemblyOps.count = count;
        assemblyOps.vars = variables;
        return null;
    }

    public virtual Instruction.SizedValue FormatOperand1(Instruction.Value operand, Instruction.Register.RegisterSize? size)
    {
        if (operand.IsPointer())
        {
            if (((Instruction.Pointer)operand).register.name == Instruction.Register.RegisterName.RBP)
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(((Instruction.Pointer)operand).size), operand));
                return alloc.NextRegister(((Instruction.Pointer)operand).size);
            }
            else
            {
                emit(new Instruction.Binary("MOV", ((Instruction.Pointer)operand).register, operand));
                return ((Instruction.Pointer)operand).register;
            }
        }
        else if (operand.IsLiteral())
        {
            if (size == null) { throw new Errors.ImpossibleError("Null size in FormatOperand1 when operand is literal"); }

            emit(new Instruction.Binary("MOV", alloc.CurrentRegister((Instruction.Register.RegisterSize)size), operand));
            return alloc.NextRegister((Instruction.Register.RegisterSize)size);
        }
        return (Instruction.Register)operand;
    }

    public Instruction.Value? visitNewExpr(Expr.New expr)
    {
        // either dealloc on exit (handled by OS), require manual delete, or implement GC

        var rax = alloc.NextRegister(Instruction.Register.RegisterSize._64Bits);
        var rbx = alloc.NextRegister(Instruction.Register.RegisterSize._64Bits);
        // Move the following into a runtime procedure, and pass in the expr.internalClass.size as a parameter
        // {
        emit(new Instruction.Binary("MOV", rax, new Instruction.Literal(Parser.Literals[0], "12")));
        emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "0")));
        emit(new Instruction.Zero("SYSCALL"));

        var ptr = new Instruction.Pointer(rax, expr.internalClass.size, 8, '+');
        emit(new Instruction.Binary("LEA", rbx, ptr));

        emit(new Instruction.Binary("LEA", new Instruction.Register( Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), ptr));
        emit(new Instruction.Binary("MOV", rax, new Instruction.Literal(Parser.Literals[0], "12")));
        emit(new Instruction.Zero("SYSCALL"));
           
        emit(new Instruction.Binary("MOV", rax, rbx));
        // }

        emit(new Instruction.Binary("MOV", rbx, rax));

        alloc.FreeRegister(rax);
        expr.call.Accept(this);
        return new Instruction.Register( Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits);
    }

    public Instruction.Value? visitDefineExpr(Expr.Define expr)
    {
        return null;
    }

    public Instruction.Value? visitIsExpr(Expr.Is expr)
    {
        return new Instruction.Literal(Parser.Literals[5], expr.value);
    }

    private void DoFooter()
    {
        for (int i = 0; i < alloc.fncPushPreserved.Length; i++)
        {
            if (alloc.fncPushPreserved[i] == true)
            {
                emit(new Instruction.Unary("POP", InstructionUtils.storageRegisters[i+1]));
            }
        }

        if (leaf)
        {
            emit(new Instruction.Unary("POP", Instruction.Register.RegisterName.RBP));
            emit(new Instruction.Zero("RET"));
        }
        else
        {
            emit(new Instruction.Zero("LEAVE"));
            emit(new Instruction.Zero("RET"));
        }
    }

    internal void emitCall(Instruction instruction)
    {
        leaf = false;
        emit(instruction);
    }

    internal void emit(Instruction instruction)
    {
        instructions.Add(instruction);
    }
    internal void emitAt(Instruction instruction, int idx)
    {
        instructions.Insert(idx, instruction);
    }

    internal void emitData(Instruction.Data instruction)
    {
        data.Add(instruction);
    }

    public Instruction.Value PassByValue(Instruction.Value operand)
    {
        if (operand.IsPointer())
        {
            if (((Instruction.Pointer)operand).register.name == Instruction.Register.RegisterName.RBP)
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(((Instruction.Pointer)operand).size), operand));
                return alloc.NextRegister(((Instruction.Pointer)operand).size);
            }
            else
            {
                emit(new Instruction.Binary("MOV", ((Instruction.Pointer)operand).register, operand));
                return ((Instruction.Pointer)operand).register;
            }
        }
        return operand;
    }
    public Instruction.Register MovToRegister(Instruction.Value operand)
    {
        if (operand.IsPointer())
        {
            if (((Instruction.Pointer)operand).register.name == Instruction.Register.RegisterName.RBP)
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(((Instruction.Pointer)operand).size), operand));
                return alloc.NextRegister(((Instruction.Pointer)operand).size);
            }
            else
            {
                emit(new Instruction.Binary("MOV", ((Instruction.Pointer)operand).register, operand));
                return ((Instruction.Pointer)operand).register;
            }
        }
        else if (operand.IsLiteral())
        {
            emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._32Bits), operand));
            return alloc.NextRegister(Instruction.Register.RegisterSize._32Bits);
        }
        return (Instruction.Register)operand;
    }

    public static string ToMangedName(Expr.Function function)
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
                    res += (type.stack.type);
                }
            }
            return res;
        }
    }


    private int SizeOfLiteral(Token.TokenType type, string literal)
    { return 8; }

    private long _SizeOfLiteral(Token.TokenType type, string literal)
    {
        switch (type)
        {
            case var value when value == Parser.Literals[0]:
                return BigInteger.Parse(literal).GetBitLength();
            case var value when value == Parser.Literals[1]:
                return BigInteger.Parse(literal).GetBitLength();
            case var value when value == Parser.Literals[2]:
                // 1 Char = 8 Bits
                return (literal.Length * 8);
            case var value when value == Parser.Literals[3]:
                // Remove the '0b' prefix
                return literal[2..].Length;
            case var value when value == Parser.Literals[4]:
                // Remove the 'x' of the prefix
                return BigInteger.Parse(literal[0] + literal[2..], NumberStyles.AllowHexSpecifier).GetBitLength();
            case var value when value == Parser.Literals[5]:
                return 8;
            default:
                throw new Exception();
        };
    }
}

