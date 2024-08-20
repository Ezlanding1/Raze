using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal Assembly assembly = new();

    private protected int conditionalCount;
    private protected string ConditionalLabel => CreateConditionalLabel(conditionalCount);
    private protected string CreateConditionalLabel(int i) => "L" + i;

    public int dataCount;
    public string DataLabel => CreateDatalLabel(dataCount);
    public string CreateDatalLabel(int i) => "LC" + i;

    static readonly AssemblyExpr.Literal NullLiteral = new(
        AssemblyExpr.Literal.LiteralType.Integer, 
        Enumerable.Repeat<byte>(0, (int)InstructionUtils.SYS_SIZE).ToArray()
    );
    internal RegisterAlloc alloc = new();

    public Assembly Generate()
    {
        Analyzer.SpecialObjects.ParentExprsToImportTopLevelWrappers();
        SymbolTableSingleton.SymbolTable.IterateImports(import =>
        {
            foreach (Expr expr in import.expressions)
            {
                alloc.Free(expr.Accept(this));
            }
        });
        return assembly;
    }

    public virtual AssemblyExpr.IValue? VisitBinaryExpr(Expr.Binary expr) => HandleInvokable(expr);

    public virtual AssemblyExpr.IValue? VisitCallExpr(Expr.Call expr) => HandleInvokable(expr);

    private AssemblyExpr.IValue? HandleInvokable(Expr.Invokable invokable)
    {
        bool instance = !invokable.internalFunction.modifiers["static"];

        var localParams = new AssemblyExpr.Register[Math.Min(invokable.Arguments.Count + Convert.ToInt32(instance), 6)];

        if (instance)
        {
            var call = (Expr.Call)invokable;
            if (!call.constructor)
            {
                if (call.callee != null)
                {
                    var callee = call.callee.Accept(this);
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.AllocParam(0, InstructionUtils.ToRegisterSize(call.callee.GetLastSize()), localParams, call.callee.GetLastType(), this), callee));
                    alloc.Free(callee);
                }
                else
                {
                    var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(call.internalFunction);
                    var size = enclosing.allocSize;
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, alloc.AllocParam(0, InstructionUtils.ToRegisterSize(size), localParams, enclosing, this), new AssemblyExpr.Pointer(8, size)));
                }
            }
            else
            {

                localParams[0] =
                    alloc.ReserveScratchRegister(this, AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits);
            }
        }

        for (int i = 0; i < invokable.Arguments.Count; i++)
        {
            AssemblyExpr.IValue arg = invokable.Arguments[i].Accept(this)
                .IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)invokable.internalFunction.parameters[i].stack.size);

            if (i + Convert.ToUInt16(instance) < InstructionUtils.paramRegister.Length)
            {
                bool _ref = invokable.internalFunction.parameters[i].modifiers["ref"];
                if (_ref)
                {
                    (var instruction, arg) = PreserveRefPtrVariable(invokable.Arguments[i], (AssemblyExpr.Pointer)arg);
                    _ref = instruction == AssemblyExpr.Instruction.LEA;
                }

                if (_ref)
                {
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.LEA, alloc.AllocParam(Convert.ToInt16(instance) + i, InstructionUtils.SYS_SIZE, localParams, null, this), arg));
                }
                else
                {
                    var paramReg = alloc.AllocParam(Convert.ToInt16(instance) + i, InstructionUtils.ToRegisterSize(invokable.internalFunction.parameters[i].stack.size), localParams, invokable.internalFunction.parameters[i].stack.type, this);

                    if (!(arg.IsRegister() && HandleSeteOptimization((AssemblyExpr.Register)arg, paramReg)))
                    {
                        if (arg.IsRegister(out var register) && !alloc.IsNeededOrNeededPreserved(((AssemblyExpr.Register)arg).Name))
                        {
                            alloc.FreeRegister(register);
                            register.nameBox.Value = paramReg.Name;
                        }
                        else
                        {
                            Emit(new AssemblyExpr.Binary(GetMoveInstruction(type: invokable.internalFunction.parameters[i].stack.type), paramReg, arg));
                        }
                    }
                }
            }
            else
            {
                if (invokable.internalFunction.parameters[i].modifiers["ref"])
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

        alloc.SaveScratchRegistersBeforeCall(this, invokable.Arguments.Count + Convert.ToInt32(instance));

        for (int i = 0; i < localParams.Length; i++)
        {
            alloc.FreeParameter(i, localParams[i], this);
        }

        if (invokable.internalFunction.modifiers["virtual"])
        {
            var call = (Expr.Call)invokable;

            var callee = (call.callee != null) ?
                call.callee.GetLastType() :
                SymbolTableSingleton.SymbolTable.NearestEnclosingClass(call.internalFunction);

            var reg = alloc.NextRegister(InstructionUtils.SYS_SIZE);
            Emit(new AssemblyExpr.Binary(
                AssemblyExpr.Instruction.MOV,
                reg,
                new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RDI, 8, InstructionUtils.SYS_SIZE)
            ));
            EmitCall(new AssemblyExpr.Unary(AssemblyExpr.Instruction.CALL,
                new AssemblyExpr.Pointer(reg, -callee.GetOffsetOfVTableMethod(call.internalFunction), InstructionUtils.SYS_SIZE)
            ));

            alloc.FreeRegister(reg);
        }
        else
        {
            EmitCall(new AssemblyExpr.Unary(AssemblyExpr.Instruction.CALL, new AssemblyExpr.ProcedureRef(ToMangledName(invokable.internalFunction))));
        }


        if (invokable.Arguments.Count > InstructionUtils.paramRegister.Length && alloc.fncPushPreserved.leaf)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.ADD, new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RSP, InstructionUtils.SYS_SIZE), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, BitConverter.GetBytes((invokable.Arguments.Count - InstructionUtils.paramRegister.Length) * 8))));
        }

        return invokable.internalFunction.constructor ?
            alloc.ReAllocConstructorReturnRegister(localParams[0].Name) :
            HandleRefVariableDeref(
                invokable.internalFunction.refReturn, 
                alloc.NeededAlloc(
                    InstructionUtils.ToRegisterSize(invokable.internalFunction._returnType.type.allocSize), 
                    this, 
                    IsFloatingType(invokable.internalFunction._returnType.type) ? 
                        AssemblyExpr.Register.RegisterName.XMM0 : 
                        AssemblyExpr.Register.RegisterName.RAX
                ), 
                invokable.internalFunction._returnType.type
            );
    }

    public AssemblyExpr.IValue? VisitClassExpr(Expr.Class expr)
    {
        alloc.current = expr;

        if (!expr.trait)
        {
            GenerateVirtualTable(expr);
        }
        foreach (var blockExpr in expr.definitions)
        {
            blockExpr.Accept(this);
        }

        alloc.UpContext();
        return null;
    }

    private void GenerateVirtualTable(Expr.Class expr)
    {
        IEnumerable<Expr.Function> virtualMethods = expr.GetVirtualMethods();

        if (expr.emitVTable || virtualMethods.Any())
        {
            EmitData(new AssemblyExpr.Data("VTABLE_FOR_" + expr.name.lexeme,
                new(
                    AssemblyExpr.Literal.LiteralType.RefData, 
                    AssemblyExpr.ImmediateGenerator.Generate(AssemblyExpr.Literal.LiteralType.RefData, "TYPEINFO_FOR_" + expr.name.lexeme, AssemblyExpr.Register.RegisterSize._64Bits))
                )
            );
            foreach (Expr.Function function in virtualMethods)
            {
                if (!function.dead)
                {
                    EmitData(new AssemblyExpr.Data(null, new AssemblyExpr.ProcedureRef(ToMangledName(function))));
                }
                else
                {
                    EmitData(new AssemblyExpr.Data(null, NullLiteral));
                }
            }
            EmitData(new AssemblyExpr.Data("TYPEINFO_FOR_" + expr.name.lexeme,
                AssemblyExpr.Literal.LiteralType.String, AssemblyExpr.ImmediateGenerator.ParseRefString(expr.name.lexeme))
            );
        }
    }

    public AssemblyExpr.IValue? VisitDeclareExpr(Expr.Declare expr)
    {
        AssemblyExpr.IValue? operand = expr.value?.Accept(this)
            ?.IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)expr.stack.size, true);

        if (alloc.current is not Expr.Class)
        {
            alloc.AllocateVariable(expr.stack);
        }

        var _ref = expr.stack._ref;

        if (operand == null)
        {
            if (alloc.current is Expr.Class)
            {
                operand = GetDefaultValueOfType(expr.stack.type);
            }
            else return null;
        }

        if (operand.IsPointer(out var ptr))
        {
            AssemblyExpr.Register reg = ptr.AsRegister(this);
            reg.Size = _ref ? InstructionUtils.SYS_SIZE : ptr.Size;

            if (!_ref && IsFloatingType(expr.stack.type))
            {
                alloc.Free(reg);
                reg = alloc.NextSseRegister();
                Emit(new AssemblyExpr.Binary(GetMoveInstruction(_ref, expr.stack.type), reg, operand));
            }
            else
            {
                Emit(new AssemblyExpr.Binary(GetMoveInstruction(_ref), reg, operand));
            }
            _ref = false;
            operand = reg;
        }
        else if (operand.IsRegister(out var register))
        {
            if (SafeGetCmpTypeRaw(register, out var instruction))
            {
                alloc.Free(instruction.operand);

                if (expr.classScoped)
                {
                    var stackPtr = (AssemblyExpr.Pointer)expr.stack.value;
                    Emit(new AssemblyExpr.Binary(GetMoveInstruction(_ref), alloc.CurrentRegister(InstructionUtils.SYS_SIZE), new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE)));
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
            var chunks = ChunkString((AssemblyExpr.Literal)operand);
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
            Emit(new AssemblyExpr.Binary(GetMoveInstruction(_ref), alloc.CurrentRegister(InstructionUtils.SYS_SIZE), new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE)));
            Emit(new AssemblyExpr.Binary(GetMoveInstruction(_ref, operand.IsLiteral() ? null : expr.stack.type), new AssemblyExpr.Pointer(alloc.CurrentRegister(InstructionUtils.SYS_SIZE), -expr.stack.ValueAsPointer.offset, expr.stack._ref ? InstructionUtils.SYS_SIZE : InstructionUtils.ToRegisterSize(expr.stack.size)), operand));
        }
        else
        {
            Emit(new AssemblyExpr.Binary(GetMoveInstruction(_ref, (operand.IsLiteral() || expr.stack._ref) ? null : expr.stack.type), new AssemblyExpr.Pointer(-expr.stack.ValueAsPointer.offset, expr.stack._ref ? InstructionUtils.SYS_SIZE : InstructionUtils.ToRegisterSize(expr.stack.size)), operand));
        }

        dealloc:
        alloc.Free(operand);
        return null;
    }

    public AssemblyExpr.IValue? VisitFunctionExpr(Expr.Function expr)
    {
        if (expr.modifiers["inline"] || expr.dead || expr.Abstract)
        {
            return null;
        }

        Emit(new AssemblyExpr.Procedure(ToMangledName(expr)));

        alloc.InitializeFunction(expr, this);

        alloc.AllocateParameters(expr, this);

        if (expr.constructor && expr.enclosing is Expr.Class _class)
        {
            alloc.current = _class;
            alloc.ListAccept(_class.declarations, this);
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

    public AssemblyExpr.IValue? VisitTypeReferenceExpr(Expr.TypeReference expr)
    {
        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Type accepted in assembler"));
    }

    public AssemblyExpr.IValue? VisitAmbiguousGetReferenceExpr(Expr.AmbiguousGetReference expr)
    {
        if (!expr.instanceCall)
        {
            return null;
        }

        AssemblyExpr.Register register = null;

        if (expr.classScoped)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, (register = alloc.NextRegister(InstructionUtils.SYS_SIZE)), Expr.DataType._this.value));
        }

        var stack = expr.datas[0];

        if (stack.inlinedData)
        {
            if (expr.datas.Length == 1)
            {
                return stack.value.IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)stack.size);
            }
            register = stack.value.NonPointerNonLiteral(this, stack.type);
        }
        else if (register == null)
        {
            if (expr.datas.Length == 1)
            {
                return HandleRefVariableDeref(stack._ref, stack.value, InstructionUtils.ToRegisterSize(stack.size), stack.type);
            }
            register = alloc.NextRegister(InstructionUtils.SYS_SIZE);
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, stack.value));
        }
        else
        {
            if (expr.datas.Length == 1)
            {
                return HandleRefVariableDeref(stack._ref, new AssemblyExpr.Pointer(register, -stack.ValueAsPointer.offset, stack.size), InstructionUtils.ToRegisterSize(stack.size), stack.type);
            }
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, new AssemblyExpr.Pointer(register, -stack.ValueAsPointer.offset, stack.size)));
        }

        for (int i = 1; i < expr.datas.Length-1; i++)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, new AssemblyExpr.Pointer(register, -expr.datas[i].ValueAsPointer.offset, expr.datas[i].size)));
        }
        return HandleRefVariableDeref(expr.GetLastData()?._ref == true, new AssemblyExpr.Pointer(register, -expr.datas[^1].ValueAsPointer.offset, expr.datas[^1].size), expr.datas[^1].type);
    }

    public AssemblyExpr.IValue? VisitInstanceGetReferenceExpr(Expr.InstanceGetReference expr)
    {
        AssemblyExpr.Register register;

        var firstGet = expr.getters[0].Accept(this);
        
        if (expr.getters.Count == 1)
        {
            return firstGet;
        }
        else
        {
            register = (AssemblyExpr.Register)(firstGet.IsPointer(out var ptr)? ptr.value : firstGet);
        }

        for (int i = 1; i < expr.getters.Count-1; i++)
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, register, new AssemblyExpr.Pointer(register, -((Expr.Get)expr.getters[i]).data.ValueAsPointer.offset, ((Expr.Get)expr.getters[i]).data.size)));
        }
        return new AssemblyExpr.Pointer(register, -((Expr.Get)expr.getters[^1]).data.ValueAsPointer.offset, ((Expr.Get)expr.getters[^1]).data.size);
    }

    public AssemblyExpr.IValue? VisitGetExpr(Expr.Get expr)
    {
        throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Get accepted in assembler"));
    }

    public AssemblyExpr.IValue? VisitLogicalExpr(Expr.Logical expr)
    {
        var operand1 = expr.left.Accept(this);

        if (operand1.IsLiteral(out var unresolvedLiteral))
        {
            var literal = unresolvedLiteral.CreateRawLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
            if ((expr.op.type == Token.TokenType.AND) ^ IsTrueBooleanLiteral(literal))
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
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, operand1, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [0])));

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

        if (operand2.IsLiteral(out var unresolvedLiteral2))
        {
            var literal = unresolvedLiteral2.CreateRawLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
            assembly.text.RemoveRange(assembly.text.Count-2, 2);

            if ((expr.op.type == Token.TokenType.AND) ^ IsTrueBooleanLiteral(literal))
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
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, operand1, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [0])));

                Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.JE, new AssemblyExpr.LocalProcedureRef(cLabel)));
            }
            else if (SafeGetCmpTypeRaw((AssemblyExpr.Register)operand2, out AssemblyExpr.Unary instruction))
            {
                alloc.Free(instruction.operand);
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

    public AssemblyExpr.IValue? VisitGroupingExpr(Expr.Grouping expr)
    {
        return expr.expression.Accept(this);
    }

    public AssemblyExpr.IValue? VisitLiteralExpr(Expr.Literal expr)
    {
        switch (expr.literal.type)
        {
            case Parser.LiteralTokenType.RefString:
                string name = DataLabel;
                EmitData(
                    new AssemblyExpr.Data(
                        name,
                        AssemblyExpr.Literal.LiteralType.String, 
                        AssemblyExpr.ImmediateGenerator.ParseRefString(expr.literal.lexeme)
                    )
                );
                dataCount++;
                return new AssemblyExpr.UnresolvedLiteral(AssemblyExpr.Literal.LiteralType.RefData, name);
            case Parser.LiteralTokenType.Floating:
                return new AssemblyExpr.UnresolvedDataLiteral(AssemblyExpr.Literal.LiteralType.Floating, expr.literal.lexeme, this);
            case Parser.LiteralTokenType.String:
            case Parser.LiteralTokenType.Integer:
            case Parser.LiteralTokenType.UnsignedInteger:
            case Parser.LiteralTokenType.Binary:
            case Parser.LiteralTokenType.Hex:
            case Parser.LiteralTokenType.Boolean:
                return new AssemblyExpr.UnresolvedLiteral((AssemblyExpr.Literal.LiteralType)expr.literal.type, expr.literal.lexeme);
            default:
                throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"Invalid Literal Type ({expr.literal.type})"));
        }
        
    }

    public virtual AssemblyExpr.IValue? VisitUnaryExpr(Expr.Unary expr) => HandleInvokable(expr);

    public AssemblyExpr.IValue? VisitIfExpr(Expr.If expr)
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

            if (condition.IsLiteral(out var unresolvedLiteral))
            {
                var literal = unresolvedLiteral.CreateRawLiteral(AssemblyExpr.Register.RegisterSize._8Bits);
                if (IsTrueBooleanLiteral(literal))
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

    AssemblyExpr.IValue? Expr.IVisitor<AssemblyExpr.IValue?>.VisitWhileExpr(Expr.While expr)
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

    public AssemblyExpr.IValue? VisitForExpr(Expr.For expr)
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

    public AssemblyExpr.IValue? VisitBlockExpr(Expr.Block expr)
    {
        alloc.CreateBlock();
        foreach (Expr blockExpr in expr.block)
        {
            alloc.Free(blockExpr.Accept(this));
        }
        alloc.RemoveBlock();
        return null;
    }

    public virtual AssemblyExpr.IValue? VisitReturnExpr(Expr.Return expr)
    {
        if (!expr.IsVoid(alloc.current))
        {
            var function = (Expr.Function)alloc.current;
            AssemblyExpr.Instruction instruction = GetMoveInstruction(function.refReturn, function._returnType.type);

            AssemblyExpr.IValue operand = expr.value.Accept(this)
                .IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)Math.Max((int)AssemblyExpr.Register.RegisterSize._32Bits, function._returnType.type.allocSize));

            if (((Expr.Function)alloc.current).refReturn)
            {
                (instruction, operand) = PreserveRefPtrVariable(expr.value, (AssemblyExpr.Pointer)operand);
            }

            var _returnRegister = IsFloatingType(function._returnType.type) ? 
                new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.XMM0, AssemblyExpr.Register.RegisterSize._128Bits) :
                new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, operand.Size);

            if ((operand.IsRegister(out var register) && (register.Name != _returnRegister.Name))
                || operand.IsPointer())
            {
                if (operand.Size < AssemblyExpr.Register.RegisterSize._32Bits)
                {
                    Emit(PartialRegisterOptimize(((Expr.Function)alloc.current)._returnType.type, _returnRegister, operand));
                }
                else
                {
                    Emit(new AssemblyExpr.Binary(instruction, _returnRegister, operand));
                }
            }
            else if (operand.IsLiteral())
            {
                Emit(new AssemblyExpr.Binary(instruction, _returnRegister, operand));
            }

            alloc.Free(operand);
        }
        else
        {
            Emit(new AssemblyExpr.Binary(
                AssemblyExpr.Instruction.MOV, 
                new AssemblyExpr.Register(AssemblyExpr.Register.RegisterName.RAX, InstructionUtils.SYS_SIZE), 
                new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [0])
            ));
        }

        DoFooter();

        return null;
    }

    public virtual AssemblyExpr.IValue? VisitAssignExpr(Expr.Assign expr)
    {
        AssemblyExpr.IValue? operand2 = expr.value.Accept(this)
            ?.IfLiteralCreateLiteral((AssemblyExpr.Register.RegisterSize)expr.member.GetLastSize(), true);
        AssemblyExpr.IValue operand1 = expr.member.Accept(this).NonLiteral(this, expr.member.GetLastType());

        if (operand2 == null) return null;

        if (Analyzer.TypeCheckUtils.IsVariableWithRefModifier(expr.value))
        {
            operand1 = (AssemblyExpr.Pointer)((AssemblyExpr.Binary)assembly.text[^1]).operand2;
            assembly.text.RemoveAt(assembly.text.Count - 1);

            operand2 = PreserveRefPtrVariable(true, operand2);
        }

        if (operand2.IsPointer(out var op2Ptr))
        {
            var reg = alloc.NextRegister(op2Ptr.Size, expr.member.GetLastType());
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, operand2));
            operand2 = reg;
        }
        else if (operand2.IsRegister(out var register))
        {
            if (HandleSeteOptimization(register, operand1))
            {
                return null;
            }
        }
        else
        {
            if (operand1.IsPointer())
            {
                var chunks = ChunkString((AssemblyExpr.Literal)operand2);
                if (chunks.Item1 != -1)
                {
                    var ptr = (AssemblyExpr.Pointer)operand1;
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(ptr.value, ptr.offset - 4, AssemblyExpr.Register.RegisterSize._32Bits), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, Encoding.ASCII.GetBytes(chunks.Item1.ToString()))));
                    Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(ptr.value, ptr.offset, InstructionUtils.ToRegisterSize((int)ptr.Size-4)), new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, Encoding.ASCII.GetBytes(chunks.Item2.ToString()))));

                    goto dealloc;
                }
            }
        }

        Emit(new AssemblyExpr.Binary(GetMoveInstruction(type: (operand2.IsLiteral() || expr.member.GetLastData()?._ref == true) ? null : expr.member.GetLastType()), operand1, operand2));

        dealloc:
        alloc.Free(operand1);
        alloc.Free(operand2);
        return null;
    }

    public AssemblyExpr.IValue? VisitPrimitiveExpr(Expr.Primitive expr)
    {
        alloc.current = expr;
        alloc.ListAccept(expr.definitions, this);
        alloc.UpContext();
        return null;
    }

    public AssemblyExpr.IValue? VisitKeywordExpr(Expr.Keyword expr)
    {
        return expr.keyword switch
        {
            "null" => NullLiteral,
            "true" => new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [1]),
            "false" => new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [0]),
            _ => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic($"'{expr.keyword}' is not a keyword")),
        };
    }
    public AssemblyExpr.IValue? VisitInlineAssemblyExpr(Expr.InlineAssembly expr)
    {
        foreach (var inlineAsmExpr in expr.instructions)
        {
            inlineAsmExpr.Accept(this);
        }
        return null;
    }

    public AssemblyExpr.IValue? VisitNewExpr(Expr.New expr)
    {
        bool hasVTable = expr.internalClass.emitVTable || expr.internalClass.GetVirtualMethods().Count != 0;
        int size = Math.Max(1, expr.internalClass.size);

        new Expr.HeapAlloc(new Expr.Literal(new(Parser.LiteralTokenType.Integer, size.ToString()))).Accept(this);

        if (hasVTable)
        {
            Emit(new AssemblyExpr.Binary(
                AssemblyExpr.Instruction.MOV,
                new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RDI, (int)InstructionUtils.SYS_SIZE, InstructionUtils.SYS_SIZE),
                new AssemblyExpr.DataRef("VTABLE_FOR_" + expr.internalClass.name.lexeme)
            ));
        }

        return expr.call.Accept(this);
    }

    public AssemblyExpr.IValue? VisitIsExpr(Expr.Is expr)
    {
        if (expr.value != null)
        {
            return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Boolean, ((bool)expr.value)? [1] : [0]);
        }
        else
        {
            var left = CompareVTables(expr);
            alloc.Free(left);
            var register = alloc.NextRegister(AssemblyExpr.Register.RegisterSize._8Bits);
            Emit(new AssemblyExpr.Unary(AssemblyExpr.Instruction.SETE, register));
            return register;
        }
    }

    public AssemblyExpr.IValue VisitAsExpr(Expr.As expr)
    {
        if (expr._is.value != null)
        {
            return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Boolean, ((bool)expr._is.value) ? [1] : [0]);
        }
        else
        {
            var left = CompareVTables(expr._is);
            var register = alloc.NextRegister(AssemblyExpr.Register.RegisterSize._64Bits);
            Emit(new AssemblyExpr.Binary(
                AssemblyExpr.Instruction.MOV, 
                register, 
                left
            ));
            alloc.Free(left);
            var register2 = alloc.NextRegister(AssemblyExpr.Register.RegisterSize._64Bits);
            Emit(new AssemblyExpr.Binary(
                AssemblyExpr.Instruction.MOV, 
                register2, 
                NullLiteral
            ));
            Emit(new AssemblyExpr.Binary(
                AssemblyExpr.Instruction.CMOVNZ,
                register,
                register2
            ));
            alloc.Free(register2);
            return register;
        }
    }

    public AssemblyExpr.IValue VisitHeapAllocExpr(Expr.HeapAlloc expr)
    {
        var size = expr.size.Accept(this)
            .NonPointer(this, null)
            .IfLiteralCreateLiteral(AssemblyExpr.Register.RegisterSize._64Bits);

        // either dealloc on exit (handled by OS), require manual delete, or implement GC
        alloc.ReserveRegister(this, 0);
        var rax = alloc.GetRegister(AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._64Bits);
        alloc.ReserveRegister(this, AssemblyExpr.Register.RegisterName.RDI);
        var rdi = alloc.GetRegister(AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits);

        // Move the following into a runtime procedure, and pass in the expr.internalClass.size as a parameter
        // {
        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [12])));
        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rdi, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [0])));
        Emit(new AssemblyExpr.Nullary(AssemblyExpr.Instruction.SYSCALL));

        if (size.IsLiteral())
        {
            var ptr = new AssemblyExpr.Pointer(rax, -BitConverter.ToInt32(((AssemblyExpr.Literal)size).value), 8);
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.LEA, rdi, ptr));
        }
        else
        {
            if (size.Size < AssemblyExpr.Register.RegisterSize._32Bits)
            {
                alloc.Free(size);
                var newReg = alloc.NextRegister(InstructionUtils.SYS_SIZE);
                Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOVZX, newReg, size));
                size = newReg;
            }
            ((AssemblyExpr.IRegisterPointer)size).Size = AssemblyExpr.Register.RegisterSize._64Bits;

            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.ADD, rax, size));
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rdi, rax));
        }

        Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Integer, [12])));

        alloc.NullReg(AssemblyExpr.Register.RegisterName.RDI);
        alloc.NeededAlloc(AssemblyExpr.Register.RegisterSize._64Bits, this, AssemblyExpr.Register.RegisterName.RDI);

        Emit(new AssemblyExpr.Nullary(AssemblyExpr.Instruction.SYSCALL));

        //Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, rax, rbx));
        // }


        alloc.Free(size);
        alloc.FreeRegister(rax);
        return rdi;
    }
    
    private AssemblyExpr.IValue CompareVTables(Expr.Is expr)
    {
        var left = expr.left.Accept(this);
        if (left.IsPointer())
        {
            var reg = alloc.NextRegister(AssemblyExpr.Register.RegisterSize._64Bits);
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, reg, left));
            left = reg;
        }
        Emit(new AssemblyExpr.Binary(
            AssemblyExpr.Instruction.CMP,
            new AssemblyExpr.Pointer((AssemblyExpr.Register)left, 8, AssemblyExpr.Register.RegisterSize._64Bits),
            new AssemblyExpr.DataRef("VTABLE_FOR_" + expr.right.type.name.lexeme)
        ));
        return left;
    }

    public AssemblyExpr.IValue? VisitImportExpr(Expr.Import expr)
    {
        return null;
    }

    public AssemblyExpr.IValue? VisitNoOpExpr(Expr.NoOp expr)
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

    internal static bool IsFloatingType(Expr.Type? type) =>
        type != null && Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Floating].Matches(type);

    internal AssemblyExpr.Instruction HandleConditionalCmpType(AssemblyExpr.IValue conditional)
    {
        if (conditional.IsRegister(out var register) && SafeGetCmpTypeRaw(register, out var res))
        {
            assembly.text.RemoveAt(assembly.text.Count - 1);
            return res.instruction;
        }
        else if (!conditional.IsLiteral())
        {
            Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.CMP, conditional, new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.Boolean, [1])));
        }

        return AssemblyExpr.Instruction.SETE;
    }

    internal bool HandleSeteOptimization(AssemblyExpr.Register register, AssemblyExpr.IValue newValue)
    {
        if (SafeGetCmpTypeRaw(register, out var instruction))
        {
            alloc.Free(instruction.operand);

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

    internal static AssemblyExpr.Binary PartialRegisterOptimize(Expr.Type operand1Type, AssemblyExpr.IRegisterPointer operand1, AssemblyExpr.IValue operand2)
    {
        operand1.Size = AssemblyExpr.Register.RegisterSize._32Bits;
        return new AssemblyExpr.Binary(GetMoveWithExtendInstruction(operand1Type), operand1, operand2);
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

    private AssemblyExpr.Literal GetDefaultValueOfType(Expr.Type type)
    {
        //return type switch
        //{
        //    _ when type.Matches(Analyzer.TypeCheckUtils.objectType) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.UnsignedInteger]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Floating]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.String]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Binary]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Hex]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Boolean]) ||
        //    type.Matches(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.RefString]) => new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.UnsignedInteger, [0]),
        //};
        return new AssemblyExpr.Literal(AssemblyExpr.Literal.LiteralType.UnsignedInteger, [0]);
    }

    private protected static AssemblyExpr.Instruction GetMoveInstruction(bool isRef=false, Expr.Type? type=null) =>
        isRef ? AssemblyExpr.Instruction.LEA : IsFloatingType(type) ? AssemblyExpr.Instruction.MOVSS : AssemblyExpr.Instruction.MOV;

    private protected AssemblyExpr.IValue? HandleRefVariableDeref(bool _ref, AssemblyExpr.IValue register, Expr.Type type) =>
        (register != null) ? HandleRefVariableDeref(_ref, register, register.Size, type) : null;

    private protected AssemblyExpr.IValue HandleRefVariableDeref(bool _ref, AssemblyExpr.IValue register, AssemblyExpr.Register.RegisterSize size, Expr.Type type) =>
        _ref ? new AssemblyExpr.Pointer(register.NonPointerNonLiteral(this, null).nameBox, 0, size) : register;

    private protected static (AssemblyExpr.Instruction, AssemblyExpr.IValue) PreserveRefPtrVariable(Expr expr, AssemblyExpr.Pointer pointer) =>
      Analyzer.TypeCheckUtils.IsRefVariable(expr) ? (AssemblyExpr.Instruction.MOV, new AssemblyExpr.Register(((AssemblyExpr.Register)pointer.value).nameBox, InstructionUtils.SYS_SIZE)) : (AssemblyExpr.Instruction.LEA, pointer);

    private protected static AssemblyExpr.IValue PreserveRefPtrVariable(bool _ref, AssemblyExpr.IValue pointer) =>
        _ref ? new AssemblyExpr.Register(((AssemblyExpr.Register)((AssemblyExpr.Pointer)pointer).value).nameBox, InstructionUtils.SYS_SIZE) : pointer;

    private static AssemblyExpr.Instruction GetMoveWithExtendInstruction(Expr.Type type) =>
        Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer].Matches(type) ? AssemblyExpr.Instruction.MOVSX : AssemblyExpr.Instruction.MOVZX;
}
