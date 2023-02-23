using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze
{
    internal class Assembler : Expr.IVisitor<Instruction.Register?>
    {
        List<Expr> expressions;
        List<Instruction> data;
        List<List<Instruction>> instructions;

        List<bool> footerType;
        int index;
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

        public Assembler(List<Expr> expressions)
        {
            this.expressions = expressions;
            this.data = new();
            data.Add(new Instruction.Section("data"));
            this.instructions = new();
            this.conditionalCount = 0;
            this.dataCount = 0;
            this.index = -1;
            this.lastJump = "";
            this.footerType = new();
        }
        
        internal (List<List<Instruction>>, List<Instruction>) Assemble()
        {
            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
            }
            return (instructions, data);
        }

        public Instruction.Register? visitBinaryExpr(Expr.Binary expr)
        {
            string instruction = InstructionInfo.ToType(expr.op.type);
            Instruction.Register operand1 = expr.left.Accept(this);
            Instruction.Register operand2 = expr.right.Accept(this);

            if (InstructionInfo.ConditionalJump.ContainsKey(expr.op.type)) 
            {
                lastJump = expr.op.type;
            }
            if (operand1.IsLiteral() && operand2.IsLiteral())
            {
                MovToRegister("RAX", operand1);
                emit(new Instruction.Binary(instruction, new Instruction.Register("RAX"), operand2));
                return new Instruction.Register("RAX");
            }
            else if (operand1.IsPointer() && operand2.IsLiteral())
            {
                var pointer = ((Instruction.Pointer)operand1);
                MovToRegister("RAX", operand1);
                emit(new Instruction.Binary(instruction, new Instruction.Register(InstructionInfo.Registers[("RAX", pointer.size)]), operand2));
                return new Instruction.Register(InstructionInfo.Registers[("RAX", pointer.size)]);
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
            return operand1;
        }

        public Instruction.Register? visitCallExpr(Expr.Call expr)
        {

            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Register arg = expr.arguments[i].Accept(this);
                DeclareToRegister(expr.internalFunction.parameters[i].size, InstructionInfo.paramRegister[i], arg);
            }

            string operand1 = expr.internalFunction.QualifiedName;

            if (!expr.internalFunction.modifiers["static"])
            {
                // Note: Is this the proprer register? c++ uses it as the first param (RDI)
                emit(new Instruction.Binary("LEA", new Instruction.Register(InstructionInfo.InstanceRegister), new Instruction.Pointer(expr.stackOffset, 8)));
            }

            emit(new Instruction.Unary("CALL", operand1));
            
            if (expr.internalFunction._returnType != "void")
            {
                return new Instruction.Register(InstructionInfo.Registers[("RAX", expr.internalFunction._returnSize)]);
            }
            return null;
        }

        public Instruction.Register? visitClassExpr(Expr.Class expr)
        {
            index++;
            foreach (var blockExpr in expr.block.block)
            {
                blockExpr.Accept(this);
            }
            index--;
            return null;
        }

        public Instruction.Register? visitDeclareExpr(Expr.Declare expr)
        {
            Instruction.Register operand2 = expr.value.Accept(this);
            if (operand2 == null)
            {
                return null;
            }
            if (!operand2.IsLiteral() && operand2.name == null)
            {
                return null;
            }
            Declare(expr.size, expr.stackOffset, operand2, SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(expr));
            return null;
        }

        public Instruction.Register? visitFunctionExpr(Expr.Function expr)
        {
            index++;
            bool leafFunc = ((expr.leaf || expr.size == 0) && expr.size <= 128);
            emit(new Instruction.Function(expr.QualifiedName));


            Instruction.Binary? sub = null;
            if (!leafFunc)
            {
                emit(new Instruction.Unary("PUSH", "RBP"));
                emit(new Instruction.Binary("MOV", "RBP", "RSP"));
                    sub = new Instruction.Binary("SUB", "RSP", "TMP");
                    emit(sub);
                }
            else
            {
                emit(new Instruction.Unary("PUSH", "RBP"));
                emit(new Instruction.Binary("MOV", "RBP", "RSP"));
            }
            footerType.Add(leafFunc);

            for (int i = 0; i < expr.arity; i++)
            {
                var paramExpr = expr.parameters[i];
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.stackOffset, paramExpr.size), new Instruction.Register(InstructionInfo.Registers[(InstructionInfo.paramRegister[i], paramExpr.size)])));
            }

            expr.block.Accept(this);

            if (!leafFunc)
            {
                if (expr.size > 128)
            {

                sub.operand2 = new Instruction.Register((expr.size - 128).ToString());
            }
                else
                {
                    sub.operand2 = new Instruction.Register(expr.size.ToString());
                }
            }
            

            DoFooter();

            footerType.RemoveAt(footerType.Count - 1);
            index--;

            if (expr._returnType != "void")
            {
                return new Instruction.Register(InstructionInfo.Registers[("RAX", expr._returnSize)]);
            }
            else
            {
                return null;
            }
        }

        public Instruction.Register? visitGetExpr(Expr.Get expr)
        {
            return expr.get.Accept(this);
        }

        public Instruction.Register? visitGroupingExpr(Expr.Grouping expr)
        {
            return expr.expression.Accept(this);
        }

        public Instruction.Register? visitLiteralExpr(Expr.Literal expr)
        {
            return new Instruction.Literal(expr.literal.lexeme, expr.literal.type);
        }

        public Instruction.Register? visitUnaryExpr(Expr.Unary expr)
        {
            string instruction = InstructionInfo.ToType(expr.op.type, true);
            Instruction.Register operand1 = expr.operand.Accept(this);
            if (instruction == "RET")
            {
                MovToRegister("RAX", operand1);
                return new Instruction.Register("RAX");
            }
            if (operand1.IsRegister() || operand1.IsPointer())
            {
                emit(new Instruction.Unary(instruction, operand1));
                return operand1;
            }
            else
            {
                MovToRegister("RAX", new Instruction.Register(operand1));
                emit(new Instruction.Unary(instruction, new Instruction.Register("RAX")));
                return new Instruction.Register("RAX");

            }
            return operand1;
        }

        public Instruction.Register? visitVariableExpr(Expr.Variable expr)
        {
            if (expr.define.Item1)
            {
                return expr.define.Item2.Accept(this);
            }

            if (expr.stackOffset == null)
            {
                return new Instruction.Literal("0", Parser.Literals[0]);
            }
            return new Instruction.Pointer(SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(expr), (int)expr.stackOffset, (int)expr.size);
        }

        public Instruction.Register? visitConditionalExpr(Expr.Conditional expr)
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
                    fJump.operand = new Instruction.FunctionRef(ConditionalLabel);
                    emit(new Instruction.Function(ConditionalLabel));
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

                fJump.operand = new Instruction.FunctionRef(ConditionalLabel);
                emit(new Instruction.Function(ConditionalLabel));
                conditionalCount++;
                if (_if._else != null)
                {
                    foreach (Expr blockExpr in _if._else.block.block)
                    {
                        blockExpr.Accept(this);
                    }
                }
                emit(new Instruction.Function(ConditionalLabel));
                tJump.operand = new Instruction.FunctionRef(ConditionalLabel);

                index++;
                instructions.Add(new());
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
                emit(new Instruction.Function(ConditionalLabel));

                expr.block.Accept(this);

                conditionalCount--;
                
                emit(new Instruction.Function(ConditionalLabel));
                expr.condition.Accept(this);
                conditionalCount++;
                emit(new Instruction.Unary(InstructionInfo.ConditionalJump[lastJump], ConditionalLabel));
                conditionalCount--;
                conditionalCount += 2;

                return null;
            }
            throw new NotImplementedException();
        }

        public Instruction.Register? visitBlockExpr(Expr.Block expr)
        {
            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }
            return null;
        }

        public Instruction.Register? visitReturnExpr(Expr.Return expr)
        {
            if (!expr._void)
            {
                Instruction.Register register = expr.value.Accept(this);
                if (register != null)
                    DeclareToRegister(expr.size, "RAX", register);
            }
            DoFooter();
            return null;
        }

        public Instruction.Register? visitAssignExpr(Expr.Assign expr)
        {
            Instruction.Register operand2 = expr.value.Accept(this);
            
            if (expr.op != null)
            {
                string instruction = InstructionInfo.ToType(expr.op.type);
                emit(new Instruction.Binary(instruction, new Instruction.Pointer(SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(expr.variable), expr.variable.stackOffset, expr.variable.size), operand2));
            }
            else
            {
                Declare(expr.variable.size, expr.variable.stackOffset, operand2, SymbolTableSingleton.SymbolTable.other.classScopedVars.Contains(expr.variable));
            }
            return null;
        }

        public Instruction.Register? visitPrimitiveExpr(Expr.Primitive expr)
        {
            return null;
        }

        public Instruction.Register? visitKeywordExpr(Expr.Keyword expr)
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

        public Instruction.Register? visitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (string instruction in expr.block)
            {
                emit(new Instruction.AsmInstruction(instruction));
            }
            return null;
        }

        public Instruction.Register? visitNewExpr(Expr.New expr)
        {
            SymbolTableSingleton.SymbolTable.other.globalClassVarOffset = expr.call.stackOffset;
            foreach (var blockExpr in expr.internalClass.topLevelBlock.block)
            {
                blockExpr.Accept(this);
            }
            SymbolTableSingleton.SymbolTable.other.globalClassVarOffset = null;

            expr.call.Accept(this);

            return null;
        }

        public Instruction.Register? visitDefineExpr(Expr.Define expr)
        {
            return null;
        }

        public Instruction.Register? visitIsExpr(Expr.Is expr)
        {
            return new Instruction.Literal(expr.value, Parser.Literals[5]);
        }

        private void Declare(int size, int stackOffset, Instruction.Register value, bool isClassScoped)
        {
            if (value.IsLiteral())
            {
                // make sure type is always the type in the correct format (INTEGER, BOOLEAN, etc.)
                int literalSize = SizeOfLiteral(value.name, ((Instruction.Literal)value).type);
                if (size <= InstructionInfo.MaxLiteral)
                {
                    emit(new Instruction.Binary("MOV", new Instruction.Pointer(isClassScoped, stackOffset, size), new Instruction.Register(value.name)));
                }
                else
                {
                    string name = DataLabel;
                    emitData(new Instruction.Data(name, InstructionInfo.DataSizeOf(size, ref value.name), value.name));
                    emit(new Instruction.Binary("MOV", new Instruction.Pointer(isClassScoped,stackOffset, size), new Instruction.DataRef(name)));
                    dataCount++;
                }
            }
            else if (value.IsPointer() || value.IsRegister())
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(isClassScoped, stackOffset, size), value));
            }
        }

        private void DeclareToRegister(int size, string register, Instruction.Register value)
        {
            if (value.IsLiteral())
            {
                if (size <= InstructionInfo.MaxLiteral)
                {
                    emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.Registers[(register, size)]), new Instruction.Register(value.name)));
                }
                else
                {
                    string name = DataLabel;
                    emitData(new Instruction.Data(name, InstructionInfo.DataSizeOf(size, ref value.name), value.name));
                    emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.Registers[(register, size)]), new Instruction.DataRef(name)));
                    dataCount++;
                }
            }
            else if (value.IsPointer() || value.IsRegister())
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(InstructionInfo.Registers[(register, size)]), value));
            }
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

        // size=0 is temporary while primitive ops don't exist
        private void MovToRegister(string register, Instruction.Register value, int size = 0)
        {
            if (size <= InstructionInfo.MaxLiteral)
            {
                emit(new Instruction.Binary("MOV", new Instruction.Register(register), value));
            }
            else
            {
                string name = DataLabel;
                emitData(new Instruction.Data(name, InstructionInfo.DataSizeOf(size, ref value.name), value.name));
                emit(new Instruction.Binary("MOV", new Instruction.Register(register), new Instruction.DataRef(name)));
                dataCount++;
            }
        }
        private void emit(Instruction instruction)
        {
            while (instructions.Count <= index)
            {
                instructions.Add(new List<Instruction>());
            }
            if (index < 0)
            {
                throw new Errors.BackendError("Top Level Code", $"Top level code is not allowed");
            }
            instructions[index].Add(instruction);
        }
        private void emitData(Instruction.Data instruction)
        {
            data.Add(instruction);
        }

    }
    
}
