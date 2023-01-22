using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
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
                MovToRegister(InstructionInfo.Registers[(InstructionInfo.paramRegister[i], expr.internalFunction.parameters[i].size)], arg);
            }

            string operand1 = expr.internalFunction.FullName;

            if (!expr.internalFunction.modifiers["static"])
            {
                // Note: Is this the proprer register? c++ uses it as the first param (RDI)
                emit(new Instruction.Binary("LEA", new Instruction.Register(InstructionInfo.InstanceRegister), new Instruction.Pointer((int)expr.stackOffset-8, 8)));
            }

            emit(new Instruction.Unary("CALL", operand1));
            return new Instruction.Register("RAX");
        }

        public Instruction.Register? visitClassExpr(Expr.Class expr)
        {
            index++;
            foreach (var blockExpr in expr.block.block)
            {
                if (blockExpr is Expr.Function)
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
            Declare(expr.size, expr.stackOffset, operand2.name);
            return null;
        }

        public Instruction.Register? visitFunctionExpr(Expr.Function expr)
        {

            if (expr.constructor)
            {
                return null;
            }

            index++;
            expr.keepStack = (expr.keepStack == true && expr.size != 0)? true : false;
            emit(new Instruction.Function(expr.FullName));


            Instruction.Binary sub = null;
            if (expr.keepStack)
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

            if (!expr.modifiers["static"])
            {
                emit(new Instruction.Binary("MOV", "RBP", InstructionInfo.InstanceRegister));
            }

            footerType.Add(expr.keepStack);

            for (int i = 0; i < expr.arity; i++)
            {
                var paramExpr = expr.parameters[i];
                emit(new Instruction.Binary("MOV", new Instruction.Pointer((int)paramExpr.stackOffset, (int)paramExpr.size), new Instruction.Register(InstructionInfo.Registers[(InstructionInfo.paramRegister[i], paramExpr.size)])));
                emit(new Instruction.Binary("MOV", new Instruction.Pointer((int)paramExpr.stackOffset, (int)paramExpr.size), new Instruction.Register(InstructionInfo.Registers[(InstructionInfo.paramRegister[i], paramExpr.size)])));
            }

            expr.block.Accept(this);
            if (expr.keepStack)
            {
                sub.operand2 = new Instruction.Register(expr.size.ToString());
            }
            DoFooter();

            footerType.RemoveAt(footerType.Count - 1);
            index--;
            return new Instruction.Register("RAX");
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
            return new Instruction.Literal(expr.literal.lexeme);
        }

        public Instruction.Register? visitSuperExpr(Expr.Super expr)
        {
            throw new NotImplementedException();
        }

        public Instruction.Register? visitThisExpr(Expr.This expr)
        {
            throw new NotImplementedException();
        }

        public Instruction.Register? visitUnaryExpr(Expr.Unary expr)
        {
            string instruction = InstructionInfo.ToType(expr.op.type, true);
            Instruction.Register operand1 = expr.operand.Accept(this);
            if (instruction == "RET")
            {
                MovToRegister("RAX", operand1);
                return new Instruction.Literal("RET");
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
                return new Instruction.Literal("0");
            }
            return new Instruction.Pointer((int)expr.stackOffset, (int)expr.size);
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
                    MovToRegister("RAX", register, expr.size);
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
                emit(new Instruction.Binary(instruction, new Instruction.Pointer(expr.variable.stackOffset, expr.variable.size), operand2));
            }
            else
            {
                Declare(expr.variable.size, expr.variable.stackOffset, operand2.name);
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
                    return new Instruction.Literal("0");
                case "true":
                    return new Instruction.Literal("1");
                case "false":
                    return new Instruction.Literal("0");
                default:
                    throw new Exception($"Raze Error: '{expr.keyword}' is not a primitive type (function)");
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
            Analyzer.SymbolTable.other.globalClassVarOffset = expr.call.stackOffset;
            foreach (var blockExpr in expr.internalClass.topLevelBlock.block)
            {
                blockExpr.Accept(this);
            }
            Analyzer.SymbolTable.other.globalClassVarOffset = null;

            expr.call.Accept(this);

            foreach (var ctorExpr in expr.internalClass.constructor.block.block)
            {
                ctorExpr.Accept(this);
            }
            return null;
        }

        public Instruction.Register? visitDefineExpr(Expr.Define expr)
        {
            return null;
        }

        public Instruction.Register? visitIsExpr(Expr.Is expr)
        {
            return new Instruction.Literal(expr.value);
        }

        private void Declare(int size, int stackOffset, string value)
        {
            if (size <= InstructionInfo.MaxLiteral)
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(stackOffset, size), new Instruction.Register(value)));
            }
            else
            {
                string name = DataLabel;
                emitData(new Instruction.Data(name, InstructionInfo.DataSizeOf(size, value), value));
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(stackOffset, size), new Instruction.DataRef(name)));
                dataCount++;
            }
        }

        private void DoFooter()
        {
            if (footerType[footerType.Count - 1])
            {
                emit(new Instruction.Zero("LEAVE"));
                emit(new Instruction.Zero("RET"));
            }
            else
            {
                emit(new Instruction.Unary("POP", "RBP"));
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
                emitData(new Instruction.Data(name, InstructionInfo.DataSizeOf(size, value.name), value.name));
                emit(new Instruction.Binary("MOV", new Instruction.Register(register), new Instruction.DataRef(name)));
                dataCount++;
            }
        }
        private void emit(Instruction instruction)
        {
            if (instructions.Count <= index)
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
