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

        List<bool> footerType;
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
        string lastJump;
        bool firstGet;
        int registerIdx;

        public Assembler(List<Expr> expressions)
        {
            this.expressions = expressions;
            this.data = new();
            data.Add(new Instruction.Section("data"));
            this.instructions = new();
            this.conditionalCount = 0;
            this.dataCount = 0;
            this.lastJump = "";
            this.footerType = new();
        }
        
        internal (List<Instruction>, List<Instruction>) Assemble()
        {
            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
                registerIdx = 0;
            }
            return (instructions, data);
        }

        public Instruction.Value? visitBinaryExpr(Expr.Binary expr)
        {
            string instruction = InstructionInfo.ToType(expr.op.type);
            Instruction.Value operand1 = expr.left.Accept(this);
            Instruction.Value operand2 = expr.right.Accept(this);

            if (InstructionInfo.ConditionalJump.ContainsKey(expr.op.type)) 
            {
                lastJump = expr.op.type;
            }
            if (operand1.IsLiteral() && operand2.IsLiteral())
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), operand1));
                emit(new Instruction.Binary(instruction, new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), operand2));
                return new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits);
            }
            else if (operand1.IsRegister() || operand1.IsPointer()) 
            {
                emit(new Instruction.Binary(instruction, operand1, operand2));
                return operand1;
            }
            else if (operand2.IsRegister() || operand2.IsPointer())
            {
                emit(new Instruction.Binary(instruction, operand2, operand1));
                return operand2;
            }
            else
            {
                emit(new Instruction.Binary(instruction, operand1, operand2));
            }
            return operand1;
        }

        public Instruction.Value? visitCallExpr(Expr.Call expr)
        {
            int paramReg = 0;
            if (!expr.internalFunction.modifiers["static"])
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.paramRegister[0], Instruction.Register.RegisterSize._64Bits), expr.constructor? new Instruction.Register("RBX", Instruction.Register.RegisterSize._64Bits) : new Instruction.Pointer(expr.stackOffset, 8)));
                paramReg++;
            }


            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Value arg = expr.arguments[i].Accept(this);
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.paramRegister[paramReg + i], expr.internalFunction.parameters[i].member.variable.stack.size), arg));
            }


            string operand1 = expr.internalFunction.QualifiedName;


            emit(new Instruction.Unary("CALL", new Instruction.ProcedureRef(operand1)));
            
            if (expr.internalFunction._returnType.type.name.type != "void")
            {
                return new Instruction.Register("RAX", expr.internalFunction._returnSize);
            }
            return null;
        }

        public Instruction.Value? visitClassExpr(Expr.Class expr)
        {
            foreach (var blockExpr in expr.block.block)
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
                var reg = new Instruction.Register(InstructionInfo.storageRegisters[registerIdx-1], ((Instruction.Pointer)operand).size);
                emit(new Instruction.Binary("MOV", reg, operand));
                operand = reg;
            }

            if (SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(expr.stack))
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.storageRegisters[registerIdx], Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer("RBP", 8, 8)));
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(InstructionInfo.storageRegisters[registerIdx], expr.stack.stackOffset, expr.stack.size), operand));
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(expr.stack.stackOffset, expr.stack.size), operand));
            }

            return null;
        }

        public Instruction.Value? visitFunctionExpr(Expr.Function expr)
        {
            bool leafFunc = ((expr.leaf || expr.size == 0) && expr.size <= 128);
            emit(new Instruction.Procedure(expr.QualifiedName));


            Instruction.Binary? sub = null;
            if (!leafFunc)
            {
                emit(new Instruction.Unary("PUSH", "RBP"));
                emit(new Instruction.Binary("MOV", "RBP", "RSP"));
                sub = new Instruction.StackAlloc("SUB", "RSP", "TMP");
                emit(sub);
            }
            else
            {
                emit(new Instruction.Unary("PUSH", "RBP"));
                emit(new Instruction.Binary("MOV", "RBP", "RSP"));
            }
            footerType.Add(leafFunc);

            int count = 0;

            if (!expr.modifiers["static"])
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(8, 8), new Instruction.Register(InstructionInfo.paramRegister[0], 8)));
                count++;
            }
            for (int i = 0; i < expr.arity; i++)
            {
                var paramExpr = expr.parameters[i];
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.member.variable.stack.stackOffset, paramExpr.member.variable.stack.size), new Instruction.Register(InstructionInfo.paramRegister[count], paramExpr.member.variable.stack.size)));
            }

            expr.block.Accept(this);

            if (!leafFunc)
            {
                if (expr.size > 128)
                {

                    sub.operand2 = new Instruction.Literal((expr.size - 128).ToString(), Parser.Literals[0]);
                }
                else
                {
                    sub.operand2 = new Instruction.Literal(expr.size.ToString(), Parser.Literals[0]);
                }
            }
            

            DoFooter();

            footerType.RemoveAt(footerType.Count - 1);

            if (expr._returnType.type.name.type != "void")
            {
                return new Instruction.Register("RAX", expr._returnSize);
            }
            else
            {
                return null;
            }
        }

        public Instruction.Value? visitGetExpr(Expr.Get expr)
        {
            emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.storageRegisters[registerIdx], Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer(firstGet ? "RBP" : InstructionInfo.storageRegisters[registerIdx], expr.stackOffset, 8)));
            firstGet = false;
            return expr.get.Accept(this);
        }

        public Instruction.Value? visitThisExpr(Expr.This expr)
        {
            if (expr.get == null) 
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.storageRegisters[registerIdx], Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer("RBP", 8, 8))); 
                return new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits);  
            }

            return this.visitGetExpr(expr);
        }

        public Instruction.Value? visitGroupingExpr(Expr.Grouping expr)
        {
            return expr.expression.Accept(this);
        }

        public Instruction.Value? visitLiteralExpr(Expr.Literal expr)
        {
            switch (expr.literal.type)
            {
                case "STRING":
                    string name = DataLabel;
                    emitData(new Instruction.Data(name, InstructionInfo.dataSize[1], expr.literal.lexeme));
                    dataCount++;
                    return new Instruction.Literal(name, expr.literal.type);
                case "INTEGER":
                case "FLOAT":
                case "BINARY":
                case "HEX":
                case "BOOLEAN":
                    return new Instruction.Literal(expr.literal.lexeme, expr.literal.type);
                default:
                    throw new Errors.ImpossibleError($"Invalid Literal Type ({expr.literal.type})");
            }
            
        }

        public Instruction.Value? visitUnaryExpr(Expr.Unary expr)
        {
            string instruction = InstructionInfo.ToType(expr.op.type, true);
            Instruction.Value operand1 = expr.operand.Accept(this);

            if (operand1.IsRegister() || operand1.IsPointer())
            {
                emit(new Instruction.Unary(instruction, operand1));
                return operand1;
            }
            else
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), operand1));
                emit(new Instruction.Unary(instruction, new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits)));
                return new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits);
            }
        }

        public Instruction.Value? visitVariableExpr(Expr.Variable expr)
        {
            if (expr.define.Item1)
            {
                return expr.define.Item2.Accept(this);
            }

            if (expr.stack.stackOffset == null)
            {
                return new Instruction.Literal("0", Parser.Literals[0]);
            }
            bool frst = firstGet;
            firstGet = false;

            if (SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(expr.stack))
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.storageRegisters[registerIdx], Instruction.Register.RegisterSize._64Bits), new Instruction.Pointer("RBP", 8, 8)));
                frst = false;
            }

            return new Instruction.Pointer(frst ? "RBP" : InstructionInfo.NextRegister(ref registerIdx), expr.stack.stackOffset, expr.stack.size);
        }

        public Instruction.Value? visitConditionalExpr(Expr.Conditional expr)
        {
            if (expr.type.lexeme == "if")
            {
                var _if = (Expr.If)expr;

                expr.condition.Accept(this);
                var fJump = new Instruction.Unary(InstructionInfo.ConditionalJump[lastJump], "TMP");
                emit(fJump);

                expr.block.Accept(this);


                var tJump = new Instruction.Unary("JMP", "TMP");
                emit(tJump);


                foreach (Expr.ElseIf elif in _if.ElseIfs)
                {
                    fJump.operand = new Instruction.ProcedureRef(ConditionalLabel);
                    emit(new Instruction.Procedure(ConditionalLabel));
                    conditionalCount++;

                    elif.condition.Accept(this);

                    fJump = new Instruction.Unary(InstructionInfo.ConditionalJump[lastJump], "TMP");

                    emit(fJump);
                    foreach (Expr blockExpr in elif.block.block)
                    {
                        blockExpr.Accept(this);
                    }

                    emit(tJump);
                }

                fJump.operand = new Instruction.ProcedureRef(ConditionalLabel);
                emit(new Instruction.Procedure(ConditionalLabel));
                conditionalCount++;
                if (_if._else != null)
                {
                    foreach (Expr blockExpr in _if._else.block.block)
                    {
                        blockExpr.Accept(this);
                    }
                }
                emit(new Instruction.Procedure(ConditionalLabel));
                tJump.operand = new Instruction.ProcedureRef(ConditionalLabel);

                conditionalCount++;
                return null;
            }
            else if (expr.type.lexeme == "else" || expr.type.lexeme == "else if")
            {
                return null;
            }
            else if (expr.type.lexeme == "while")
            {
                emit(new Instruction.Unary("JMP", ConditionalLabel));
                
                conditionalCount++;
                emit(new Instruction.Procedure(ConditionalLabel));

                expr.block.Accept(this);

                conditionalCount--;
                
                emit(new Instruction.Procedure(ConditionalLabel));
                expr.condition.Accept(this);
                conditionalCount++;
                emit(new Instruction.Unary(InstructionInfo.ConditionalJump[lastJump], ConditionalLabel));
                conditionalCount--;
                conditionalCount += 2;

                return null;
            }
            throw new NotImplementedException();
        }

        public Instruction.Value? visitBlockExpr(Expr.Block expr)
        {
            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
                registerIdx = 0;
            }
            return null;
        }

        public Instruction.Value? visitReturnExpr(Expr.Return expr)
        {
            if (!expr._void)
            {
                Instruction.Value operand = expr.value.Accept(this);
                emit(new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), operand));
            }
            DoFooter();
            return null;
        }

        public Instruction.Value? visitAssignExpr(Expr.Assign expr)
        {
            Instruction.Value operand1 = expr.member.Accept(this);
            Instruction.Value operand2 = expr.value.Accept(this);

            // Note: Defualt instruction is assignment
            string instruction = "MOV";

            if (expr.op != null)
            {
                instruction = InstructionInfo.ToType(expr.op.type);
            }

            if (operand2.IsPointer())
            {
                Instruction.Register reg = new Instruction.Register(InstructionInfo.storageRegisters[registerIdx - 1], ((Instruction.Pointer)operand2).size);
                emit(new Instruction.Binary(instruction, reg, operand2));
                operand2 = reg;
            }

            emit(new Instruction.Binary("MOV", operand1, operand2));
            return null;
        }

        public Instruction.Value? visitPrimitiveExpr(Expr.Primitive expr)
        {
            foreach (var blockExpr in expr.block.block)
            {
                blockExpr.Accept(this);
            }
            return null;
        }

        public Instruction.Value? visitKeywordExpr(Expr.Keyword expr)
        {
            switch (expr.keyword)
            {
                case "null":
                    return new Instruction.Literal("0", Parser.Literals[0]);
                case "true":
                    return new Instruction.Literal("1", Parser.Literals[0]);
                case "false":
                    return new Instruction.Literal("0", Parser.Literals[0]);
                default:
                    throw new Errors.ImpossibleError($"'{expr.keyword}' is not a keyword");
            }
        }

        public Instruction.Value? visitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (var variable in expr.variables.Keys)
            {
                expr.variables[variable].name = (SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(variable.stack) ? "RAX" : "RBP") + " - " + variable.stack.stackOffset;
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

            // Move the following into a runtime procedure, and pass in the expr.internalClass.size as a parameter
            // {
            emit(new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), new Instruction.Literal("12", Parser.Literals[0])));
            emit(new Instruction.Binary("MOV", new Instruction.Register("RDI", Instruction.Register.RegisterSize._64Bits), new Instruction.Literal("0", Parser.Literals[0])));
            emit(new Instruction.Zero("SYSCALL"));

            var ptr = new Instruction.Pointer("RAX", expr.internalClass.size, 8, '+');
            emit(new Instruction.Binary("LEA", new Instruction.Register("RBX", Instruction.Register.RegisterSize._64Bits), ptr));

            emit(new Instruction.Binary("LEA", new Instruction.Register("RDI", Instruction.Register.RegisterSize._64Bits), ptr));
            emit(new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), new Instruction.Literal("12", Parser.Literals[0])));
            emit(new Instruction.Zero("SYSCALL"));
               
            emit(new Instruction.Binary("MOV", new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits), new Instruction.Register("RBX", Instruction.Register.RegisterSize._64Bits)));
            // }

            emit(new Instruction.Binary("MOV", new Instruction.Register("RBX", Instruction.Register.RegisterSize._64Bits), new Instruction.Register("RAX", Instruction.Register.RegisterSize._64Bits)));

            expr.call.Accept(this);

            return new Instruction.Register("RBX", Instruction.Register.RegisterSize._64Bits);
        }

        public Instruction.Value? visitDefineExpr(Expr.Define expr)
        {
            return null;
        }

        public Instruction.Value? visitIsExpr(Expr.Is expr)
        {
            return new Instruction.Literal(expr.value, Parser.Literals[5]);
        }

        public Instruction.Value? visitMemberExpr(Expr.Member expr)
        {
            firstGet = true;
            return expr.get.Accept(this);
        }

        private void DoFooter()
        {
            if (footerType[footerType.Count - 1])
            {
                emit(new Instruction.Unary("POP", "RBP"));
                emit(new Instruction.Zero("RET"));
            }
            else
            {
                emit(new Instruction.Zero("LEAVE"));
                emit(new Instruction.Zero("RET"));
            }
        }

        private void emit(Instruction instruction)
        {
            instructions.Add(instruction);
        }

        private void emitData(Instruction.Data instruction)
        {
            data.Add(instruction);
        }

        private int SizeOfLiteral(string literal, string type)
        { return 8; }

        private long _SizeOfLiteral(string literal, string type)
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
