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
    public List<Instruction> data = new();
    private protected List<Instruction> instructions = new();

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

    public RegisterAlloc alloc = new();

    AssemblyOps assemblyOps;

    public Assembler(List<Expr> expressions)
    {
        this.expressions = expressions;
        data.Add(new Instruction.Section("data"));
        this.assemblyOps = new(this);
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

    public virtual Instruction.Value? VisitBinaryExpr(Expr.Binary expr)
    {
        var localParams = new Instruction.Register[2];

        Instruction.Value operand1 = expr.left.Accept(this);
        Emit(new Instruction.Binary("MOV", (localParams[0] = alloc.AllocParam(0, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[0].stack.size))), operand1));

        alloc.Free(operand1);

        Instruction.Value operand2 = expr.right.Accept(this);
        Emit(new Instruction.Binary("MOV", (localParams[1] = alloc.AllocParam(1, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[1].stack.size))), operand2));

        alloc.Free(operand2);

        for (int i = 0; i < 2; i++)
        {
            if (localParams[i] != null)
            {
                alloc.FreeParameter(i, localParams[i], this);
            }
        }

        alloc.ReserveRegister(this);

        EmitCall(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangledName(expr.internalFunction))));

        return alloc.CallAlloc(InstructionUtils.ToRegisterSize(expr.internalFunction._returnSize));
    }

    public virtual Instruction.Value? VisitCallExpr(Expr.Call expr)
    {
        bool instance = !expr.internalFunction.modifiers["static"];

        if (instance)
        {
            if (!expr.constructor)
            {
                ((Instruction.Binary)instructions[^1]).operand1 = new Instruction.Register(InstructionUtils.paramRegister[0], Instruction.Register.RegisterSize._64Bits);
            }
            else
            {
                Emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[0], Instruction.Register.RegisterSize._64Bits), new Instruction.Register(Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits)));
            }
        }

        var localParams = new Instruction.Register?[Math.Min(expr.arguments.Count + Convert.ToInt16(instance), 6)];

        for (int i = 0; i < expr.arguments.Count; i++)
        {
            Instruction.Value arg = expr.arguments[i].Accept(this);

            if (i + Convert.ToUInt16(instance) < InstructionUtils.paramRegister.Length)
            {
                if (expr.internalFunction.parameters[i].modifiers["ref"])
                {
                    Emit(new Instruction.Binary("LEA", alloc.AllocParam(Convert.ToInt16(instance) + i, Instruction.Register.RegisterSize._64Bits), arg));
                }
                else
                {
                    var paramReg = alloc.AllocParam(Convert.ToInt16(instance) + i, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[i].stack.size));

                    if (!(arg.IsRegister() && HandleSeteOptimization((Instruction.Register)arg, paramReg)))
                    {
                        Emit(new Instruction.Binary("MOV", paramReg, arg));
                    }
                }
                localParams[Convert.ToInt16(instance) + i] = alloc.paramRegisters[Convert.ToInt16(instance) + i];
            }
            else
            {
                if (expr.internalFunction.parameters[i].modifiers["ref"])
                {
                    Instruction.Register refRegister;
                    Emit(new Instruction.Binary("LEA", (refRegister = alloc.NextRegister(Instruction.Register.RegisterSize._64Bits)), arg));
                    Emit(new Instruction.Unary("PUSH", refRegister));
                    alloc.FreeRegister(refRegister);
                }
                else
                {
                    Emit(new Instruction.Unary("PUSH", arg));
                }
            }

            alloc.Free(arg);
        }

        for (int i = Convert.ToInt16(instance); i < localParams.Length; i++)
        {
            if (localParams[i] != null)
            {
                alloc.FreeParameter(i, localParams[i], this);
            }
        }

        alloc.ReserveRegister(this);

        EmitCall(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangledName(expr.internalFunction))));

        if (expr.arguments.Count > InstructionUtils.paramRegister.Length && alloc.fncPushPreserved.leaf)
        {
            Emit(new Instruction.Binary("ADD", new Instruction.Register(Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], ((expr.arguments.Count - InstructionUtils.paramRegister.Length) * 8).ToString())));
        }
        
        return alloc.CallAlloc(InstructionUtils.ToRegisterSize(expr.internalFunction._returnSize));
    }

    public Instruction.Value? VisitClassExpr(Expr.Class expr)
    {
        foreach (var blockExpr in expr.definitions)
        {
            blockExpr.Accept(this);
        }
        
        return null;
    }

    public Instruction.Value? VisitDeclareExpr(Expr.Declare expr)
    {
        Instruction.Value operand = expr.value.Accept(this);

        var _ref = expr.stack._ref;

        if (operand.IsPointer())
        {
            var reg = alloc.CurrentRegister(_ref? Instruction.Register.RegisterSize._64Bits : ((Instruction.Pointer)operand).size);
            Emit(new Instruction.Binary(_ref? "LEA" : "MOV", reg, operand));
            _ref = false;
            operand = reg;

        }
        else if (operand.IsRegister() && SafeGetCmpTypeRaw((Instruction.Register)operand, out var instruction))
        {
            alloc.Free((Instruction.Value)instruction.operand);

            if (expr.classScoped)
            {
                Emit(new Instruction.Binary(_ref ? "LEA" : "MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
                instruction.operand = new Instruction.Pointer(alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.stack.stackOffset, expr.stack._ref ? 8 : expr.stack.size);
            }
            else
            {
                instruction.operand = new Instruction.Pointer(expr.stack.stackOffset, expr.stack._ref ? 8 : expr.stack.size);
            }
            return null;
        }

        if (expr.classScoped)
        {
            Emit(new Instruction.Binary(_ref ? "LEA" : "MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
            Emit(new Instruction.Binary(_ref ? "LEA" : "MOV", new Instruction.Pointer(alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.stack.stackOffset, expr.stack._ref ? 8 : expr.stack.size), operand));
        }
        else
        {
            Emit(new Instruction.Binary(_ref ? "LEA" : "MOV", new Instruction.Pointer(expr.stack.stackOffset, expr.stack._ref ? 8 : expr.stack.size), operand));
        }

        alloc.Free(operand);

        return null;
    }

    public Instruction.Value? VisitFunctionExpr(Expr.Function expr)
    {
        if (expr.modifiers["inline"] && !expr.modifiers["operator"])
        {
            return null;
        }

        Emit(new Instruction.Procedure(ToMangledName(expr)));

        Emit(alloc.fncPushPreserved = new(expr.size));

        int count = 0;

        if (!expr.modifiers["static"])
        {
            Emit(new Instruction.Binary("MOV", new Instruction.Pointer(8, 8), new Instruction.Register(InstructionUtils.paramRegister[0], 8)));
            count++;
        }

        for (int i = 0, len = Math.Min(expr.Arity, InstructionUtils.paramRegister.Length-count); i < len; i++)
        {
            var paramExpr = expr.parameters[i];

            if (paramExpr.modifiers["ref"])
            {
                Emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.stack.stackOffset, 8), new Instruction.Register(InstructionUtils.paramRegister[i + count], 8)));
            }
            else
            {
                Emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.stack.stackOffset, paramExpr.stack.size), new Instruction.Register(InstructionUtils.paramRegister[i+count], paramExpr.stack.size)));
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

        if (Analyzer.Primitives.IsVoidType(expr._returnType.type) || expr.modifiers["unsafe"])
        {
            DoFooter();
        }
        return null;
    }

    public Instruction.Value? VisitTypeReferenceExpr(Expr.TypeReference expr)
    {
        throw new Errors.ImpossibleError("Type accepted in assembler");
    }

    public Instruction.Value? VisitGetReferenceExpr(Expr.GetReference expr)
    {
        if (expr.classScoped)
        {
            Emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
        }

        Instruction.Register reg = alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits);

        if (expr.getters.Count != 1)
        {
            var firstGetter = expr.getters[0].Accept(this);
            if (firstGetter.IsPointer())
            {
                if (expr.getters[0] is Expr.Get firstGet && firstGet.data.stackRegister)
                {
                    reg = (Instruction.Register)NonPointer(((Expr.StackRegister)firstGet.data).register);
                }
                else
                {
                    if (!expr.classScoped)
                    {
                        ((Instruction.Pointer)firstGetter).register = new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits);
                    }
                    Emit(new Instruction.Binary("MOV", reg, firstGetter));
                }
            }
            else
            {
                reg = (Instruction.Register)firstGetter;
            }
        }

        for (int i = 1; i < expr.getters.Count-1; i++)
        {
            var getter = expr.getters[i].Accept(this);

            if (getter.IsPointer())
            {
                ((Instruction.Pointer)getter).register = reg;
                Emit(new Instruction.Binary("MOV", reg, getter));
            }
            else
            {
                alloc.Free(reg);
                reg = (Instruction.Register)getter;
            }
        }
        alloc.Free(reg);

        var lastGetter = expr.getters[^1].Accept(this);
        if (expr.getters[^1] is Expr.Get)
        {
            var stack = ((Expr.Get)expr.getters[^1]).data;

            if (stack.stackRegister)
            {
                return ((Expr.StackRegister)stack).register;
            }
            if (stack._ref)
            {
                Emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(new Instruction.Register(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits), stack.stackOffset, 8, stack.plus ? '+' : '-')));
                alloc.NullReg();
                return new Instruction.Pointer(alloc.NextRegister(InstructionUtils.ToRegisterSize(stack.size)), 0, stack.size);
            }
            return new Instruction.Pointer(
                (expr.getters.Count == 1 && !expr.classScoped) ?
                    new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : 
                    alloc.NextRegister(InstructionUtils.ToRegisterSize(stack.size)), 
                stack.stackOffset, stack.size, stack.plus ? '+' : '-');
        }
        return lastGetter;
    }

    public Instruction.Value? VisitGetExpr(Expr.Get expr)
    {
        return new Instruction.Pointer(alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.data.stackOffset, 8);
    }

    public Instruction.Value? VisitLogicalExpr(Expr.Logical expr)
    {
        var operand1 = expr.left.Accept(this);

        if (operand1.IsLiteral())
        {
            if ((expr.op.type == Token.TokenType.AND) ^ (((Instruction.Literal)operand1).value == "1"))
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
            Emit(new Instruction.Binary("CMP", operand1, new Instruction.Literal(Token.TokenType.INTEGER, "0")));

            Emit(new Instruction.Unary((expr.op.type == Token.TokenType.AND) ? "JE" : "JNE", new Instruction.LocalProcedureRef(ConditionalLabel)));
        }
        else
        {
            if (SafeGetCmpTypeRaw((Instruction.Register)operand1, out Instruction.Unary instruction))
            {
                alloc.Free((Instruction.Value)instruction.operand);
                instructions.RemoveAt(instructions.Count - 1);

                Emit(new Instruction.Unary(
                    (expr.op.type == Token.TokenType.AND) ? 
                        InstructionUtils.ConditionalJumpReversed[instruction.instruction] : 
                        InstructionUtils.ConditionalJump[instruction.instruction], 
                    new Instruction.LocalProcedureRef(ConditionalLabel)));
            }
            else
            {
                Emit(new Instruction.Binary("TEST", operand1, operand1));

                Emit(new Instruction.Unary((expr.op.type == Token.TokenType.AND) ? "JE" : "JNE", new Instruction.LocalProcedureRef(ConditionalLabel)));
            }
        }

        alloc.Free(operand1);

        var operand2 = expr.right.Accept(this);

        if (operand2.IsLiteral())
        {
            instructions.RemoveRange(instructions.Count-2, 2);

            if ((expr.op.type == Token.TokenType.AND) ^ (((Instruction.Literal)operand2).value == "1"))
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
                Emit(new Instruction.Binary("CMP", operand1, new Instruction.Literal(Token.TokenType.INTEGER, "0")));

                Emit(new Instruction.Unary("JE", new Instruction.LocalProcedureRef(cLabel)));
            }
            else if (SafeGetCmpTypeRaw((Instruction.Register)operand2, out Instruction.Unary instruction))
            {
                alloc.Free((Instruction.Value)instruction.operand);
                instructions.RemoveAt(instructions.Count - 1);

                Emit(new Instruction.Unary(InstructionUtils.ConditionalJumpReversed[instruction.instruction], new Instruction.LocalProcedureRef(cLabel)));
            }
            else
            {
                Emit(new Instruction.Binary("TEST", operand2, operand2));

                Emit(new Instruction.Unary("JE", new Instruction.LocalProcedureRef(cLabel)));
            }
        }

        alloc.Free(operand2);

        if (expr.op.type == Token.TokenType.OR)
        {
            Emit(new Instruction.LocalProcedure(ConditionalLabel));

            conditionalCount++;
        }

        conditionalCount++;

        Emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._8Bits), new Instruction.Literal(Token.TokenType.INTEGER, "1")));
       
        Emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(ConditionalLabel)));

        Emit(new Instruction.LocalProcedure(CreateConditionalLabel(conditionalCount-1)));

        Emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._8Bits), new Instruction.Literal(Token.TokenType.INTEGER, "0")));

        Emit(new Instruction.LocalProcedure(ConditionalLabel));
        conditionalCount++;
        

        return alloc.NextRegister(Instruction.Register.RegisterSize._8Bits);
    }

    public Instruction.Value? VisitGroupingExpr(Expr.Grouping expr)
    {
        return expr.expression.Accept(this);
    }

    public Instruction.Value? VisitLiteralExpr(Expr.Literal expr)
    {
        switch (expr.literal.type)
        {
            case Token.TokenType.STRING:
                string name = DataLabel;
                EmitData(new Instruction.Data(name, InstructionUtils.dataSize[1], expr.literal.lexeme + ", 0"));
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

    public virtual Instruction.Value? VisitUnaryExpr(Expr.Unary expr)
    {
        Instruction.Register param;

        Instruction.Value operand = expr.operand.Accept(this);
        Emit(new Instruction.Binary("MOV", (param = alloc.AllocParam(0, InstructionUtils.ToRegisterSize(expr.internalFunction.parameters[0].stack.size))), operand));

        alloc.Free(operand);

        if (param != null)
        {
            alloc.FreeParameter(0, param, this);
        }

        alloc.ReserveRegister(this);

        EmitCall(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangledName(expr.internalFunction))));

        return alloc.CallAlloc(InstructionUtils.ToRegisterSize(expr.internalFunction._returnSize));
    }

    public Instruction.Value? VisitIfExpr(Expr.If expr)
    {
        Instruction.Unary tJump = new Instruction.Unary("JMP", Instruction.Register.RegisterName.TMP);

        for (int i = 0; i < expr.conditionals.Count; i++)
        {
            var condition = expr.conditionals[i].condition.Accept(this);

            string cmpType = HandleConditionalCmpType(condition);

            if (condition.IsLiteral())
            {
                if (((Instruction.Literal)condition).value == "1")
                {
                    expr.conditionals[i].block.Accept(this);

                    Emit(new Instruction.LocalProcedure(ConditionalLabel));
                    tJump.operand = new Instruction.LocalProcedureRef(ConditionalLabel);

                    conditionalCount++;
                    return null;
                }
                else
                {
                    continue;
                }
            }
            Emit(new Instruction.Unary(InstructionUtils.ConditionalJumpReversed[cmpType], new Instruction.LocalProcedureRef(ConditionalLabel)));

            expr.conditionals[i].block.Accept(this);
            Emit(tJump);

            Emit(new Instruction.LocalProcedure(ConditionalLabel));
            conditionalCount++;
        }

        if (expr._else != null)
        {
            foreach (Expr blockExpr in expr._else.block)
            {
                blockExpr.Accept(this);
            }
        }
        Emit(new Instruction.LocalProcedure(ConditionalLabel));
        tJump.operand = new Instruction.LocalProcedureRef(ConditionalLabel);

        conditionalCount++;

        return null;
    }

    public Instruction.Value? VisitWhileExpr(Expr.While expr)
    {
        Emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(ConditionalLabel)));

        var conditional = new Instruction.LocalProcedure(ConditionalLabel);

        conditionalCount++;

        Emit(new Instruction.LocalProcedure(ConditionalLabel));

        expr.conditional.block.Accept(this);

        Emit(conditional);

        Emit(new Instruction.Unary(InstructionUtils.ConditionalJump[HandleConditionalCmpType(expr.conditional.condition.Accept(this))],
            new Instruction.LocalProcedureRef(ConditionalLabel)));
        conditionalCount++;

        return null;
    }

    public Instruction.Value? VisitForExpr(Expr.For expr)
    {
        expr.initExpr.Accept(this);
        Emit(new Instruction.Unary("JMP", new Instruction.LocalProcedureRef(ConditionalLabel)));

        var conditional = new Instruction.LocalProcedure(ConditionalLabel);

        conditionalCount++;

        Emit(new Instruction.LocalProcedure(ConditionalLabel));

        var cmpType = HandleConditionalCmpType(expr.conditional.block.Accept(this));

        expr.updateExpr.Accept(this);

        Emit(conditional);
        expr.conditional.condition.Accept(this);
        Emit(new Instruction.Unary(InstructionUtils.ConditionalJump[cmpType], new Instruction.LocalProcedureRef(ConditionalLabel)));
        conditionalCount++;

        return null;
    }

    public Instruction.Value? VisitBlockExpr(Expr.Block expr)
    {
        foreach (Expr blockExpr in expr.block)
        {
            blockExpr.Accept(this);
            alloc.FreeAll();
        }
        return null;
    }

    public virtual Instruction.Value? VisitReturnExpr(Expr.Return expr)
    {
        if (!expr._void)
        {
            Instruction.Value operand = expr.value.Accept(this);

            if (operand.IsRegister())
            {
                var op = (Instruction.Register)operand;
                if (op.name != Instruction.Register.RegisterName.RAX)
                    Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, op.size), operand));
            }
            else if (operand.IsPointer())
            {
                Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, ((Instruction.SizedValue)operand).size), operand));
            }
            else
            {
                Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, InstructionUtils.ToRegisterSize(expr.size)), operand));
            }

            alloc.Free(operand);
        }
        else
        {
            Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "0")));
        }
        
        DoFooter();

        return null;
    }

    public virtual Instruction.Value? VisitAssignExpr(Expr.Assign expr)
    {
        Instruction.Value operand2 = expr.value.Accept(this);
        Instruction.Value operand1 = expr.member.Accept(this);

        if (operand2.IsPointer())
        {
            var reg = alloc.NextRegister(((Instruction.Pointer)operand2).size);
            Emit(new Instruction.Binary("MOV", reg, operand2));
            operand2 = reg;
        }
        else if (operand2.IsRegister() && HandleSeteOptimization((Instruction.Register)operand2, operand1))
        {
            return null;
        }

        Emit(new Instruction.Binary("MOV", operand1, operand2));

        alloc.Free(operand1);
        alloc.Free(operand2);
        return null;
    }

    public Instruction.Value? VisitPrimitiveExpr(Expr.Primitive expr)
    {
        alloc.ListAccept(expr.definitions, this);

        return null;
    }

    public Instruction.Value? VisitKeywordExpr(Expr.Keyword expr) => expr.keyword switch
    {
        "null" => new Instruction.Literal(Parser.Literals[0], "0"),
        "true" => new Instruction.Literal(Parser.Literals[0], "1"),
        "false" => new Instruction.Literal(Parser.Literals[0], "0"),
        _ => throw new Errors.ImpossibleError($"'{expr.keyword}' is not a keyword")
    };

    public Instruction.Value? VisitAssemblyExpr(Expr.Assembly expr)
    {
        assemblyOps.count = 0;
        assemblyOps.vars = expr.variables;

        foreach (var instruction in expr.block)
        {
            instruction.Assign(assemblyOps);
        }
        return null;
    }

    public Instruction.Value? VisitNewExpr(Expr.New expr)
    {
        // either dealloc on exit (handled by OS), require manual delete, or implement GC

        var rax = alloc.NextRegister(Instruction.Register.RegisterSize._64Bits);
        var rbx = alloc.NextRegister(Instruction.Register.RegisterSize._64Bits);
        // Move the following into a runtime procedure, and pass in the expr.internalClass.size as a parameter
        // {
        Emit(new Instruction.Binary("MOV", rax, new Instruction.Literal(Parser.Literals[0], "12")));
        Emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "0")));
        Emit(new Instruction.Zero("SYSCALL"));

        var ptr = new Instruction.Pointer(rax, expr.internalClass.size, 8, '+');
        Emit(new Instruction.Binary("LEA", rbx, ptr));

        Emit(new Instruction.Binary("LEA", new Instruction.Register( Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits), ptr));
        Emit(new Instruction.Binary("MOV", rax, new Instruction.Literal(Parser.Literals[0], "12")));
        Emit(new Instruction.Zero("SYSCALL"));
           
        Emit(new Instruction.Binary("MOV", rax, rbx));
        // }

        Emit(new Instruction.Binary("MOV", rbx, rax));

        alloc.FreeRegister(rax);
        expr.call.Accept(this);
        return new Instruction.Register( Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits);
    }

    public Instruction.Value? VisitIsExpr(Expr.Is expr)
    {
        return new Instruction.Literal(Parser.Literals[5], expr.value);
    }

    private void DoFooter()
    {
        Emit(alloc.fncPushPreserved);
    }

    internal void EmitCall(Instruction instruction)
    {
        alloc.fncPushPreserved.leaf = false;
        Emit(instruction);
    }

    internal void Emit(Instruction instruction)
    {
        instructions.Add(instruction);
    }

    internal void EmitData(Instruction.Data instruction)
    {
        data.Add(instruction);
    }

    internal string HandleConditionalCmpType(Instruction.Value conditional)
    {
        if (conditional.IsRegister() && SafeGetCmpTypeRaw((Instruction.Register)conditional, out var res))
        {
            instructions.RemoveAt(instructions.Count - 1);
            return res.instruction;
        }
        else if (!conditional.IsLiteral())
        {
            Emit(new Instruction.Binary("CMP", conditional, new Instruction.Literal(Token.TokenType.BOOLEAN, "1")));
        }

        return "SETE";
    }

    internal bool HandleSeteOptimization(Instruction.Register register, Instruction.Value newValue)
    {
        if (SafeGetCmpTypeRaw(register, out var instruction))
        {
            alloc.Free((Instruction.Value)instruction.operand);

            instruction.operand = newValue;

            return true;
        }
        return false;
    }

    internal bool SafeGetCmpTypeRaw(Instruction.Register register, out Instruction.Unary? res)
    {
        if (instructions[^1] is Instruction.Unary instruction)
        {
            if (instruction.instruction[..3] == "SET" && instruction.operand == register)
            {
                res = instruction;
                return true;
            }
        }
        res = null;
        return false;
    }

    public Instruction.Register MovToRegister(Instruction.Value operand, Instruction.Register.RegisterSize? size) => (Instruction.Register)(NonLiteral(NonPointer(operand), size));
    public Instruction.Value NonPointer(Instruction.Value operand)
    {
        if (operand.IsPointer())
        {
            if (((Instruction.Pointer)operand).register.name == Instruction.Register.RegisterName.RBP)
            {
                Emit(new Instruction.Binary("MOV", alloc.CurrentRegister(((Instruction.Pointer)operand).size), operand));
                return alloc.NextRegister(((Instruction.Pointer)operand).size);
            }
            else
            {
                Emit(new Instruction.Binary("MOV", ((Instruction.Pointer)operand).register, operand));
                return ((Instruction.Pointer)operand).register;
            }
        }
        return operand;
    }
    public Instruction.SizedValue NonLiteral(Instruction.Value operand, Instruction.Register.RegisterSize? size)
    {
        if (operand.IsLiteral())
        {
            if (size == null) { throw new Errors.ImpossibleError("Null size in NonLiteral when operand is literal"); }

            Emit(new Instruction.Binary("MOV", alloc.CurrentRegister((Instruction.Register.RegisterSize)size), operand));
            return alloc.NextRegister((Instruction.Register.RegisterSize)size);
        }
        return (Instruction.SizedValue)operand;
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
                    res += (type.stack.type);
                }
            }
            return res;
        }
    }
}

