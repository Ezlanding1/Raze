using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Assembler : Expr.IVisitor<Instruction.Register?>
    {
        List<Expr> expressions;
        List<Instruction> data;
        List<List<Instruction>> instructions;
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
        Dictionary<string, List<string>> dataHashMap;
        string lastJump;

        InstructionInfo.Macros macros;
        public Assembler(List<Expr> expressions)
        {
            this.expressions = expressions;
            this.data = new();
            data.Add(new Instruction.Section("data"));
            dataHashMap = new();
            this.instructions = new();
            this.conditionalCount = 0;
            this.dataCount = 0;
            this.index = -1;
            this.lastJump = "";
            this.macros = new();
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
            if ((operand1.IsLiteral() && operand2.IsLiteral()) || (operand1.IsPointer() && operand2.IsPointer()))
            {
                MovToRegister("RAX", operand1);
                emit(new Instruction.Binary(instruction, new Instruction.Register("RAX"), operand2));
                return new Instruction.Register("RAX");
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
                MovToRegister(InstructionInfo.paramRegister[i], arg);
            }

            string operand1 = expr.callee.variable.lexeme;
            emit(new Instruction.Unary("CALL", operand1));
            return new Instruction.Literal("RAX");
        }

        public Instruction.Register? visitClassExpr(Expr.Class expr)
        {
            return null;
            //throw new NotImplementedException();
            //emit(new Instruction.Class(expr.name.lexeme));
            //expr.block.Accept(this);
            //return null;
        }

        public Instruction.Register? visitDeclareExpr(Expr.Declare expr)
        {
            string type = expr.type.lexeme;
            string operand1 = expr.name.lexeme;
            Instruction.Register operand2 = expr.value.Accept(this);
            if (operand1 == null || operand2 == null)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Invalid Expression", "");
            }
            if (!operand2.IsLiteral() && operand2.name == "CLASS")
            {
                return null;
            }
            if (operand2.name != "null")
            {
                Declare(type, expr.offset, operand1, operand2.name);
            }
            return null;
        }

        public Instruction.Register? visitFunctionExpr(Expr.Function expr)
        {
            if (expr.dead)
            {
                return null;
            }
            if (expr.constructor)
            {
                macros.SwitchMacro(expr);
                expr.block.Accept(this);

                return new Instruction.Register("RAX");
            }
            index++;
            emit(new Instruction.Function(expr.name.lexeme));
            macros.SwitchMacro(expr);
            macros.DoMacroHeader(instructions[index]);
            for (int i = 0; i < expr.arity; i++)
            {
                var paramExpr = expr.parameters[i];
                if (!HandleParamEmit(paramExpr.type.lexeme, paramExpr.offset, paramExpr.size, paramExpr.variable.lexeme, InstructionInfo.paramRegister[i].ToString()))
                {
                    emit(new Instruction.Binary("MOV", new Instruction.Pointer(paramExpr.offset, paramExpr.size), new Instruction.Register(InstructionInfo.paramRegister[i].ToString())));
                }
            }
            macros.SwitchMacro(InstructionInfo.Macros.MacroType.EmptyHeader, (InstructionInfo.Macros.MacroType)macros.footer);
            expr.block.Accept(this);

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
            string instruction = InstructionInfo.ToType(expr.op.type);
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
            if (expr.type == "string")
            {
                return new Instruction.Register(dataHashMap[expr.variable.lexeme][dataHashMap[expr.variable.lexeme].Count - 1]);
            }
            return new Instruction.Pointer(expr.offset, expr.size);
        }

        public Instruction.Register? visitConditionalExpr(Expr.Conditional expr)
        {
            if (expr.type.lexeme == "if")
            {
                var _if = (Expr.If)expr;

                expr.condition.Accept(this);
                var fJump = new Instruction.Unary(InstructionInfo.ConditionalJump[lastJump], "TMP");
                emit(fJump);

                macros.SwitchMacro(expr);
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

                macros.SwitchMacro(expr);
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
            // Emit Block Header
            macros.DoMacroHeader(instructions[index]);
            var footer = macros.footer;
            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }

            // Emit Block Footer
            macros.DoMacro(instructions[index], footer);

            return null;
        }

        public Instruction.Register? visitReturnExpr(Expr.Return expr)
        {
            if (!expr._void)
            {
                Instruction.Register register = expr.value.Accept(this);
                MovToRegister("RAX", register);
            }
            macros.DoMacroFooter(instructions[index]);
            return null;
        }

        public Instruction.Register? visitAssignExpr(Expr.Assign expr)
        {
            string type = expr.variable.type;
            string operand1 = expr.variable.variable.lexeme;
            Instruction.Register operand2 = expr.value.Accept(this);
            if (operand2.name != "null")
            {
                Declare(type, expr.variable.offset, operand1, operand2.name);
            }
            return null;
        }

        public Instruction.Register? visitPrimitiveExpr(Expr.Primitive expr)
        {
            Declare(expr);
            return null;
        }

        public Instruction.Register? visitKeywordExpr(Expr.Keyword expr)
        {
            return new Instruction.Register("null");
        }

        public Instruction.Register? visitAssemblyExpr(Expr.Assembly expr)
        {
            foreach (string instruction in expr.block)
            {
                emit(new Instruction.AsmInstruction(instruction));
            }
            return null;
        }

        private void Declare(string type, int stackOffset, string name, object value)
        {
            int size = Analyzer.SizeOf(type);
            if (!HandleEmit(type, stackOffset, size, name, value))
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(stackOffset, size), new Instruction.Register(value.ToString())));
            }
        }
        private void Declare(Expr.Primitive primitive)
        {
            string name = primitive.literal.name.lexeme;
            string type = primitive.literal.type.lexeme;
            int size = primitive.literal.size;
            Instruction.Register operand2 = primitive.literal.value.Accept(this);
            string value = operand2.name;
            if (!HandleEmit(type, primitive.stackOffset, size, name, value))
            {
                throw new Exception("Espionage Error: Internal Type Not Implemented (declare)");
            }
        }

        private bool HandleParamEmit(string type, int stackOffset, int size, string name, object value)
        {
            if (type == "string")
            {
                return true;
            }
            return HandleEmit(type, stackOffset, size, name, value);
            return false;
        }
        private bool HandleEmit(string type, int stackOffset, int size, string name, object value)
        {
            if (type == "string")
            {
                emitData(name, new Instruction.Data(name, InstructionInfo.dataSize[1], value + ", 0"));
                return true;
            }
            else if (type == "number")
            {
                emit(new Instruction.Binary("MOV", new Instruction.Pointer(stackOffset, size), new Instruction.Register(value.ToString())));
                return true;
            }
            return false;
        }
        private void MovToRegister(string register, Instruction.Register literal)
        {
            emit(new Instruction.Binary("MOV", new Instruction.Register(register), literal));
        }
        private void emit(Instruction instruction)
        {
            if (instructions.Count <= index)
            {
                instructions.Add(new List<Instruction>());
            }
            if (index < 0)
            {
                throw new Errors.BackendError(ErrorType.BackendException, "Top Level Code", $"Top level code is not allowed");
            }
            instructions[index].Add(instruction);
        }
        private void emitData(string name, Instruction.Data instruction)
        {
            if (dataHashMap.TryGetValue(name, out List<string> dataUnderVar))
            {
                dataUnderVar.Add(DataLabel);
                data.Add(new Instruction.Data(DataLabel, instruction.size, instruction.value));
                dataCount++;
            }
            else
            {
                dataUnderVar = new List<string>() { DataLabel };
                dataHashMap.Add(name, dataUnderVar);
                data.Add(new Instruction.Data(DataLabel, instruction.size, instruction.value));
                dataCount++;
            }
        }

        public Instruction.Register? visitNewExpr(Expr.New expr)
        {
            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Register arg = expr.arguments[i].Accept(this);
                MovToRegister(InstructionInfo.paramRegister[i], arg);
            }

            foreach (var blockExpr in expr.internalClass.block.block)
            {
                blockExpr.Accept(this);
            }
            return new Instruction.Register("CLASS");
        }

    }
    
}
