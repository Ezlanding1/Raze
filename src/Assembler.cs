using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Assembler : Expr.IVisitor<Instruction.Register?>
    {
        List<Expr> expressions;
        Dictionary<string, Instruction> data;
        List<List<Instruction>> instructions;
        int index;
        int conditionalCount;
        public Assembler(List<Expr> expressions)
        {
            this.expressions = expressions;
            this.data = new();
            data.Add("", new Instruction.Section("data"));
            this.instructions = new();
            this.conditionalCount = 0;
            this.index = -1;
        }
        
        internal (List<List<Instruction>>, List<Instruction>) Assemble()
        {
            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
            }
            return (instructions, data.Values.ToList());
        }

        public Instruction.Register? visitBinaryExpr(Expr.Binary expr)
        {
            string instruction = InstructionTypes.ToType(expr.op.type);
            Instruction.Register operand1 = expr.left.Accept(this);
            Instruction.Register operand2 = expr.right.Accept(this);

            if (operand1.name == "RAX") 
            {
                emit(new Instruction.Binary(instruction, operand1, operand2));
            }
            else if (operand2.name == "RAX")
            {
                emit(new Instruction.Binary(instruction, operand2, operand1));
            }
            else
            {
                MovToRegister("RAX", new Instruction.Register(operand1));
                operand1.name = "RAX";
                emit(new Instruction.Binary(instruction, operand1, operand2));
            }
            return new Instruction.Register(false, "RAX");
        }

        public Instruction.Register? visitCallExpr(Expr.Call expr)
        {
            //Important Note: Emit arguments (to stack) first
            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Register arg = expr.arguments[i].Accept(this);
                MovToRegister(InstructionTypes.paramRegister[i], arg);
            }

            string operand1 = expr.callee.variable.lexeme;
            emit(new Instruction.Unary("CALL", operand1));
            return new Instruction.Register(true, "RAX");
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
            if (!operand2.simple && operand2.name == "CLASS")
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
            if (expr.constructor)
            {
                foreach (var blockExpr in expr.block.block)
                {
                    blockExpr.Accept(this);
                }
                return new Instruction.Register(false, "RAX");
            }
            index++;
            emit(new Instruction.Function(expr.name.lexeme));
            expr.block.Accept(this);
            index--;
            return new Instruction.Register(false, "RAX");
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
            return new Instruction.Register(true, expr.literal.lexeme);
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
            string instruction = InstructionTypes.ToType(expr.op.type);
            Instruction.Register operand1 = expr.operand.Accept(this);
            if (instruction == "RET")
            {
                MovToRegister("RAX", operand1);
                return new Instruction.Register(true, "RET");
            }
            throw new NotImplementedException();
        }

        public Instruction.Register? visitVariableExpr(Expr.Variable expr)
        {
            if (expr.register)
                return new Instruction.Register(false, expr.stackPos);
            else
                return new Instruction.Register(false, "[RBP-" + expr.stackPos + "]");
        }

        public Instruction.Register? visitConditionalExpr(Expr.Conditional expr)
        {
            if (expr.type.lexeme == "if")
            {
                instructions.ForEach(x => x = x);
                var _if = (Expr.If)expr;

                expr.condition.Accept(this);
                var fJump = new Instruction.Unary("JNE", "TMP");
                emit(fJump);

                foreach (Expr blockExpr in expr.block.block)
                {
                    blockExpr.Accept(this);
                }


                var tJump = new Instruction.Unary("JMP", "TMP");
                emit(tJump);


                foreach (Expr.ElseIf elif in _if.ElseIfs)
                {
                    fJump.operand = new Instruction.FunctionRef(".L" + conditionalCount);
                    emit(new Instruction.Function(".L" + conditionalCount));
                    conditionalCount++;

                    elif.condition.Accept(this);

                    fJump = new Instruction.Unary("JNE", "TMP");

                    emit(fJump);
                    foreach (Expr blockExpr in elif.block.block)
                    {
                        blockExpr.Accept(this);
                    }

                    emit(tJump);
                }

                fJump.operand = new Instruction.FunctionRef(".L" + conditionalCount);
                emit(new Instruction.Function(".L" + conditionalCount));
                conditionalCount++;
                if (_if._else != null)
                {
                    foreach (Expr blockExpr in _if._else.block.block)
                    {
                        blockExpr.Accept(this);
                    }
                }
                emit(new Instruction.Function(".L" + conditionalCount));
                tJump.operand = new Instruction.FunctionRef(".L" + conditionalCount);

                index++;

                conditionalCount++;

            }
            return null;
        }

        public Instruction.Register? visitBlockExpr(Expr.Block expr)
        {
            // Emit Block Header
            emit(new Instruction.Unary("PUSH", "RBP"));
            emit(new Instruction.Binary("MOV", "RBP", "RSP"));

            foreach (Expr blockExpr in expr.block)
            {
                blockExpr.Accept(this);
            }

            // Emit Block Footer
            emit(new Instruction.Unary("POP", "RBP"));
            emit(new Instruction.Zero("RET"));
            return null;
        }

        public Instruction.Register? visitReturnExpr(Expr.Return expr)
        {
            Instruction.Register register = expr.value.Accept(this);
            MovToRegister("RAX", register);
            emit(new Instruction.Unary("POP", "RBP"));
            emit(new Instruction.Zero("RET"));
            return register;
        }

        public Instruction.Register? visitAssignExpr(Expr.Assign expr)
        {
            string type = expr.variable.type;
            string operand1 = expr.variable.Accept(this).name;
            Instruction.Register operand2 = expr.value.Accept(this);
            if (operand2.name != "null")
            {
                Declare(type, expr.offset, operand1, operand2.name);
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
            return new Instruction.Register(false, "null");
        }

        private void Declare(string type, int stackOffset, string name, object value)
        {
            int size = Analyzer.SizeOf(type);
            if (type == "string")
            {
                emitData(name, new Instruction.Data(name, InstructionTypes.dataSize[1], value.ToString()));
                return;
            }
            emit(new Instruction.Binary("MOV", $"{InstructionTypes.wordSize[size]} [RBP-{stackOffset}]", value.ToString()));
        }
        private void Declare(Expr.Primitive primitive)
        {
            string name = primitive.literal.name.lexeme;
            string type = primitive.literal.type.lexeme;
            int size = primitive.literal.size;
            Instruction.Register operand2 = primitive.literal.value.Accept(this);
            string value = operand2.name;
            if (type == "string")
            {
                emitData(name, new Instruction.Data(name, InstructionTypes.dataSize[1], value));
            }
            else if (type == "number")
            {
                emit(new Instruction.Binary("MOV", $"{InstructionTypes.wordSize[size]} [RBP-{primitive.stackOffset}]", value));
            }
            else
            {
                throw new Exception("Espionage Error: Internal Type Not Implemented (declare)");
            }
        }

        private void MovToRegister(string register, Instruction.Register literal)
        {
            emit(new Instruction.Binary("MOV", new Instruction.Register(false, register), literal));
        }
        private void emit(Instruction instruction)
        {
            if (instructions.Count <= index)
            {
                instructions.Add(new List<Instruction>());
            }
            instructions[index].Add(instruction);
        }
        private void emitData(string name, Instruction instruction)
        {
            data[name] = instruction;
        }

        public Instruction.Register? visitNewExpr(Expr.New expr)
        {
            //Important Note: Emit arguments (to stack) first
            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Register arg = expr.arguments[i].Accept(this);
                MovToRegister(InstructionTypes.paramRegister[i], arg);
            }

            foreach (var blockExpr in expr.internalClass.block.block)
            {
                blockExpr.Accept(this);
            }
            return new Instruction.Register(false, "CLASS");
        }
    }
    internal abstract class Instruction
    {
        public abstract string Accept(IVisitor visitor);

        public interface IVisitor
        {
            public string visitGlobal(Global instruction);
            public string visitSection(Section instruction);
            public string visitRegister(Register instruction);
            public string visitData(Data instruction);
            public string visitFunction(Function instruction);
            public string visitReference(FunctionRef instruction);
            public string visitClass(Class instruction);
            public string visitBinary(Binary instruction);
            public string visitUnary(Unary instruction);
            public string visitZero(Zero instruction);
            public string visitComment(Comment instruction);
        }

        internal class Global : Instruction
        {
            public string name;
            public Global(string name)
            {
                this.name = name;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitGlobal(this);
            }
        }

        internal class Section : Instruction
        {
            public string name;
            public Section(string name)
            {
                this.name = name;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitSection(this);
            }
        }
        internal class Register : Instruction
        {
            public bool simple;
            public string name;
            public Register(bool simple, string name)
            {
                this.simple = simple;
                this.name = name;
            }

            public Register(Register @this)
            {
                this.simple = @this.simple;
                this.name = @this.name;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitRegister(this);
            }
        }
        internal class Data : Instruction
        {
            public string name;
            public string size;
            public string value;
            public Data(string name, string size, string value)
            {
                this.name = name;
                this.size = size;
                this.value = value;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitData(this);
            }
        }

        internal class Function : Instruction
        {
            public string name;
            public Function(string name)
            {
                this.name = name;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitFunction(this);
            }
        }

        internal class FunctionRef : Instruction
        {
            public string name;
            public FunctionRef(string name)
            {
                this.name = name;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitReference(this);
            }
        }

        internal class Class : Instruction
        {
            public string name;
            public Class(string name)
            {
                this.name = name;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitClass(this);
            }
        }

        internal class Binary : Instruction
        {
            public string instruction;
            public Instruction operand1, operand2;
            public Binary(string instruction, Instruction operand1, Instruction operand2)
            {
                this.instruction = instruction;
                this.operand1 = operand1;
                this.operand2 = operand2;
            }

            public Binary(string instruction, string operand1, string operand2)
            {
                this.instruction = instruction;
                this.operand1 = new Register(false, operand1);
                this.operand2 = new Register(false, operand2);
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitBinary(this);
            }
        }

        internal class Unary : Instruction
        {
            public Instruction operand;
            public string instruction;
            public Unary(string instruction, Instruction operand)
            {
                this.instruction = instruction;
                this.operand = operand;
            }

            public Unary(string instruction, string operand)
            {
                this.instruction = instruction;
                this.operand = new Instruction.Register(false, operand);
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitUnary(this);
            }
        }

        internal class Zero : Instruction 
        {
            public string instruction;
            public Zero(string instruction)
            {
                this.instruction = instruction;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitZero(this);
            }
        }

        internal class Comment : Instruction
        {
            public string comment;
            public Comment(string comment)
            {
                this.comment = comment;
            }

            public override string Accept(IVisitor visitor)
            {
                return visitor.visitComment(this);
            }
        }
    }

    internal class InstructionTypes
    {
        internal static string ToType(string input)
        {
            return StringToOperatorType[input];
        }
        internal static string ToRegister(int input, bool bits=false, string register="RAX")
        {
            input = bits ? (input / 8) : input;
            return raxRegister[input];
        }
        private readonly static Dictionary<string, string> StringToOperatorType = new()
        {
            // Binary
            { "SHIFTRIGHT" , "SHR" },
            { "SHIFTLEFT" , "SHL" },
            { "DIVIDE" , "DIV" },
            { "MULTIPLY" , "IMUL" },
            { "B_NOT" , "NOT" },
            { "B_OR" , "OR" },
            { "B_AND" , "AND" },
            { "B_XOR" , "XOR" },
            { "MINUS" , "SUB" },
            { "PLUS" , "ADD" },
            { "EQUALTO" , "CMP" },

            // Unary
            { "return",  "RET" }

            // Zero
        };

        internal readonly static string[] paramRegister = new string[] 
        {
            "RDI",
            "RSI",
            "RDX",
            "RCX",
            "R8",
            "R9"
        };

        internal readonly static Dictionary<int, string> raxRegister = new()
        {
            { 8, "RAX"}, // 64-Bits
            { 4, "EAX"}, // Lower 32-Bits
            { 2, "AX"}, // Lower 16-Bits
            { 1, "AL"}, // Lower 8-Bits
            { 0, "AH"} // Upper 8-Bits
        };

        internal readonly static Dictionary<int, string> wordSize = new()
        {
            { 8, "QWORD"}, // 64-Bits
            { 4, "DWORD"}, // 32-Bits
            { 2, "WORD"}, // 16-Bits
            { 1, "BYTE"}, // 8-Bits
        };

        internal readonly static Dictionary<int, string> dataSize = new()
        {
            { 8, "dq"}, // 64-Bits
            { 4, "dd"}, // 32-Bits
            { 2, "dw"}, // 16-Bits
            { 1, "db"}, // 8-Bits
        };
    }
}
