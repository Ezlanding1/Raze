using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal class Assembler : Expr.IVisitor<Instruction.Value?>
    {
        List<Expr> expressions;
        List<Instruction> data;
        List<Instruction> instructions;

        bool footerType;
        int conditionalCount;
        string ConditionalLabel
        {
            get { return ".L" + conditionalCount; }
        }

        int dataCount;
        string DataLabel 
        { 
            get { return "LC" + dataCount; } 
        }
        Token.TokenType lastJump;

        RegisterAlloc alloc;

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
            }
            return (instructions, data);
        }

        public Instruction.Value? visitBinaryExpr(Expr.Binary expr)
        {
            string instruction = InstructionUtils.ToType(expr.op.type);
            Instruction.Value operand1 = expr.left.Accept(this);
            Instruction.Value operand2 = expr.right.Accept(this);

            if (InstructionUtils.ConditionalJump.ContainsKey(expr.op.type)) 
            {
                lastJump = expr.op.type;
            }

            if (operand1.IsPointer())
            {
                emit(new Instruction.Binary("MOV", ((Instruction.Pointer)operand1).register, operand1));
                operand1 = ((Instruction.Pointer)operand1).register;
            }
            else if (operand1.IsLiteral())
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(((Instruction.Pointer)operand1).size), operand1));
                operand1 = alloc.NextRegister(((Instruction.Pointer)operand1).size);
            }

            emit(new Instruction.Binary(instruction, operand1, operand2));

            if (operand2.IsPointer())
            {
                alloc.FreePtr((Instruction.Pointer)operand2);
            }
            else if (operand2.IsRegister())
            {
                alloc.FreeRegister((Instruction.Register)operand2);
            }
            
            return operand1;
        }

        public Instruction.Value? visitCallExpr(Expr.Call expr)
        {
            alloc.ReserveRax(this);

            bool instance = !expr.internalFunction.modifiers["static"];

            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Value arg = expr.arguments[i].Accept(this);
                if (i + Convert.ToUInt16(instance) < InstructionUtils.paramRegister.Length)
                {
                    emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionUtils.paramRegister[Convert.ToInt16(instance) + i], expr.internalFunction.parameters[i].stack.size), arg));
                }
                else
                {
                    emit(new Instruction.Unary("PUSH", arg));
                }
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
            
            

            emit(new Instruction.Unary("CALL", new Instruction.ProcedureRef(ToMangedName(expr.internalFunction))));

            if (expr.arguments.Count > InstructionUtils.paramRegister.Length && footerType)
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

            if (operand.IsPointer())
            {
                var reg = alloc.CurrentRegister(((Instruction.Pointer)operand).size);
                emit(new Instruction.Binary("MOV", reg, operand));
                operand = reg;
            }

            if (expr.classScoped)
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.stack.stackOffset, expr.stack.size), operand));
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(expr.stack.stackOffset, expr.stack.size), operand));
            }

            return null;
        }

        public Instruction.Value? visitFunctionExpr(Expr.Function expr)
        {
            bool leafFunc = ((expr.leaf && ((expr.constructor) ? ((Expr.Definition)expr.enclosing).leaf : true)) || expr.size == 0) && expr.size <= 128;
            emit(new Instruction.Procedure(ToMangedName(expr)));


            Instruction.Binary? sub = null;
            if (!leafFunc)
            {
                emit(new Instruction.Unary("PUSH", Instruction.Register.RegisterName.RBP));
                emit(new Instruction.Binary("MOV", Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterName.RSP));
                sub = new Instruction.StackAlloc("SUB", Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterName.TMP);
                emit(sub);
            }
            else
            {
                emit(new Instruction.Unary("PUSH", Instruction.Register.RegisterName.RBP));
                emit(new Instruction.Binary("MOV", Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterName.RSP));
            }

            alloc.fncPushPreserved = new bool[5];
            int fncPushIdx = instructions.Count;

            footerType = leafFunc;

            int count = 0;

            if (!expr.modifiers["static"])
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(8, 8), new Instruction.Register(InstructionUtils.paramRegister[0], 8)));
                count++;
            }

            for (int i = 0, len = Math.Min(expr.arity, InstructionUtils.paramRegister.Length-count); i < len; i++)
            {
                var paramExpr = expr.parameters[i];
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.stack.stackOffset, paramExpr.stack.size), new Instruction.Register(InstructionUtils.paramRegister[i+count], paramExpr.stack.size)));
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
            
            if (!leafFunc)
            {
                if (expr.size > 128)
                {

                    sub.operand2 = new Instruction.Literal(Parser.Literals[0], (expr.size - 128).ToString());
                }
                else
                {
                    sub.operand2 = new Instruction.Literal(Parser.Literals[0], expr.size.ToString());
                }
            }

            for (int i = 0; i < alloc.fncPushPreserved.Length; i++)
            {
                if (alloc.fncPushPreserved[i] == true)
                {
                    instructions.Insert(fncPushIdx, new Instruction.Unary("PUSH", InstructionUtils.storageRegisters[i+1]));
                }
            }

            DoFooter();
            
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

        public Instruction.Value? visitUnaryExpr(Expr.Unary expr)
        {
            string instruction = InstructionUtils.ToType(expr.op.type, true);
            Instruction.Value operand1 = expr.operand.Accept(this);

            if (operand1.IsRegister() || operand1.IsPointer())
            {
                emit(new Instruction.Unary(instruction, operand1));
                return operand1;
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register( Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), operand1));
                emit(new Instruction.Unary(instruction, new Instruction.Register( Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits)));
                return new Instruction.Register( Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits);
            }
        }

        public Instruction.Value? visitVariableExpr(Expr.Variable expr)
        {
            if (expr.classScoped)
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(Instruction.Register.RegisterName.RBP, 8, 8)));
            }

            for (int i = expr.offsets.Length - 1; i >= 1; i--)
            {
                emit(new Instruction.Binary("MOV", alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(((i == expr.offsets.Length-1) && !expr.classScoped) ? new (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.CurrentRegister(Instruction.Register.RegisterSize._64Bits), expr.offsets[i].stackOffset, 8)));
            }

            return new Instruction.Pointer((expr.offsets.Length == 1 && !expr.classScoped) ? new(Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) : alloc.NextRegister(InstructionUtils.ToRegisterSize(expr.stack.size)), expr.stack.stackOffset, expr.stack.size, expr.stack.plus ? '+' : '-');
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
                fJump.operand = new Instruction.ProcedureRef(ConditionalLabel);
                emit(new Instruction.Procedure(ConditionalLabel));
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

            fJump.operand = new Instruction.ProcedureRef(ConditionalLabel);
            emit(new Instruction.Procedure(ConditionalLabel));
            conditionalCount++;
            if (expr._else != null)
            {
                foreach (Expr blockExpr in expr._else.conditional.block.block)
                {
                    blockExpr.Accept(this);
                }
            }
            emit(new Instruction.Procedure(ConditionalLabel));
            tJump.operand = new Instruction.ProcedureRef(ConditionalLabel);

            conditionalCount++;
            return null;
        }

        public Instruction.Value? visitWhileExpr(Expr.While expr)
        {
            emit(new Instruction.Unary("JMP", new Instruction.ProcedureRef(ConditionalLabel)));

            var conditional = new Instruction.Procedure(ConditionalLabel);

            conditionalCount++;

            emit(new Instruction.Procedure(ConditionalLabel));

            expr.conditional.block.Accept(this);

            emit(conditional);
            expr.conditional.condition.Accept(this);
            emit(new Instruction.Unary(InstructionUtils.ConditionalJump[lastJump], new Instruction.ProcedureRef(ConditionalLabel)));
            conditionalCount++;

            return null;
        }

        public Instruction.Value? visitForExpr(Expr.For expr)
        {
            expr.initExpr.Accept(this);
            emit(new Instruction.Unary("JMP", new Instruction.ProcedureRef(ConditionalLabel)));

            var conditional = new Instruction.Procedure(ConditionalLabel);

            conditionalCount++;

            emit(new Instruction.Procedure(ConditionalLabel));

            expr.conditional.block.Accept(this);
            expr.updateExpr.Accept(this);

            emit(conditional);
            expr.conditional.condition.Accept(this);
            emit(new Instruction.Unary(InstructionUtils.ConditionalJump[lastJump], new Instruction.ProcedureRef(ConditionalLabel)));
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

        public Instruction.Value? visitReturnExpr(Expr.Return expr)
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
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits), new Instruction.Literal(Parser.Literals[0], "0")));
            }

            DoFooter();
            return null;
        }

        public Instruction.Value? visitAssignExpr(Expr.Assign expr)
        {
            Instruction.Value operand2 = expr.value.Accept(this);
            Instruction.Value operand1 = expr.member.Accept(this);

            // Note: Defualt instruction is assignment
            string instruction = "MOV";

            if (expr.op != null)
            {
                instruction = InstructionUtils.ToType(expr.op.type);
            }

            if (operand2.IsPointer())
            {
                Instruction.Register.RegisterName regName;
                var reg = alloc.NextRegister(((Instruction.Pointer)operand2).size);
                emit(new Instruction.Binary("MOV", reg, operand2));
                operand2 = reg;
            }

            emit(new Instruction.Binary(instruction, operand1, operand2));
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
            foreach (var variable in expr.variables.Keys)
            {
                expr.variables[variable].register.name = variable.classScoped ? Instruction.Register.RegisterName.RAX : Instruction.Register.RegisterName.RBP;
                expr.variables[variable].offset = variable.stack.stackOffset;
                expr.variables[variable].size = Enum.IsDefined(typeof(Instruction.Register.RegisterSize), variable.stack.size) ? ((Instruction.Register.RegisterSize)variable.stack.size) : throw new Errors.ImpossibleError($"Invalid Register Size ({variable.stack.size})");
            }
            foreach (var instruction in expr.block)
            {
                emit(instruction);
            }
            return null;
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

            if (footerType)
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

        internal void emit(Instruction instruction)
        {
            instructions.Add(instruction);
        }

        internal void emitData(Instruction.Data instruction)
        {
            data.Add(instruction);
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
    
}
