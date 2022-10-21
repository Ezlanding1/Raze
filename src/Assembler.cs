using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espionage
{
    internal class Assembler : Expr.IVisitor<Instruction.Register?>
    {
        List<Expr> expressions;
        List<Instruction> data;
        List<Instruction> instructions;
        public Assembler(List<Expr> expressions)
        {
            this.expressions = expressions;
            this.data = new();
            this.instructions = new();
        }
        
        internal List<Instruction> Assemble()
        {
            foreach (Expr expr in expressions)
            {
                expr.Accept(this);
            }
            return instructions;
        }

        public Instruction.Register? visitBinaryExpr(Expr.Binary expr)
        {
            string instruction = InstructionTypes.ToType(expr.op.type);
            Instruction.Register operand1 = expr.left.Accept(this);
            Instruction.Register operand2 = expr.right.Accept(this);

            if (operand1.name == "RAX")
            {
                emit(new Instruction.Binary(instruction, operand1.name, operand2.name));
            }
            else if (operand2.name == "RAX")
            {
                emit(new Instruction.Binary(instruction, operand2.name, operand1.name));
            }
            else
            {
                MovToRegister("RAX", operand1.name);
                operand1.name = "RAX";
                emit(new Instruction.Binary(instruction, operand1.name, operand2.name));
            }
            return new Instruction.Register(false, "RAX");
        }

        public Instruction.Register? visitCallExpr(Expr.Call expr)
        {
            //Important Note: Emit arguments (to stack) first
            for (int i = 0; i < expr.arguments.Count; i++)
            {
                Instruction.Register arg = expr.arguments[i].Accept(this);
                MovToRegister(InstructionTypes.paramRegister[i], arg.name);
            }

            string operand1 = expr.callee.lexeme;
            emit(new Instruction.Unary("CALL", operand1));
            return new Instruction.Register(true, "RAX");
        }

        public Instruction.Register? visitClassExpr(Expr.Class expr)
        {
            throw new NotImplementedException();
            //emit(new Instruction.Class(expr.name.lexeme));
            //expr.block.Accept(this);
            //return null;
        }

        public Instruction.Register? visitDeclareExpr(Expr.Declare expr)
        {
            string type = expr.type.lexeme;
            string operand1 = expr.name.lexeme;
            Instruction.Register operand2 = expr.value.Accept(this);
            if (operand2.name != "null")
            {
                Declare(type, expr.offset, operand1, operand2.name);
            }
            return null;
        }

        public Instruction.Register? visitFunctionExpr(Expr.Function expr)
        {
            emit(new Instruction.Function(expr.name.lexeme));
            expr.block.Accept(this);
            return new Instruction.Register(false, "RAX");
        }

        public Instruction.Register? visitGetExpr(Expr.Get expr)
        {
            throw new NotImplementedException();
        }

        public Instruction.Register? visitGroupingExpr(Expr.Grouping expr)
        {
            return expr.expression.Accept(this);
        }

        public Instruction.Register? visitLiteralExpr(Expr.Literal expr)
        {
            return new Instruction.Register(true, expr.literal.lexeme);
        }

        public Instruction.Register? visitSetExpr(Expr.Set expr)
        {
            throw new NotImplementedException();
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
                MovToRegister("RAX", operand1.name);
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
                Instruction.Register contidional = expr.condition.Accept(this);
                emit(new Instruction.Unary("JNE", "TMP"));
                int jmpindex = (instructions.Count - 1);

                expr.block.Accept(this);

                emit(new Instruction.Zero("NOP"));
                ((Instruction.Unary)instructions[jmpindex]).operand = (instructions.Count - 1).ToString();
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
            MovToRegister("RAX", register.name);
            emit(new Instruction.Unary("POP", "RBP"));
            emit(new Instruction.Zero("RET"));
            return register;
        }

        public Instruction.Register? visitAssignExpr(Expr.Assign expr)
        {
            string type = expr.type;
            string operand1 = expr.variable.lexeme;
            Instruction.Register operand2 = expr.value.Accept(this);
            if (operand2.name != "null")
            {
                Declare(type, expr.offset, operand1, operand2.name);
            }
            return null;
        }

        public Instruction.Register? visitKeywordExpr(Expr.Keyword expr)
        {
            return new Instruction.Register(false, "null");
        }

        private void Declare(string type, int stackOffset, string name, object value)
        {
            int size = Analyzer.SizeOf(type);
            emit(new Instruction.Binary("MOV", $"{InstructionTypes.wordSize[size]} [RBP-{stackOffset}]", value.ToString()));
        }
        private void MovToRegister(string register, string literal)
        {
            emit(new Instruction.Binary("MOV", register, literal));
        }
        private void emit(Instruction instruction)
        {
            instructions.Add(instruction);
        }
        private void emitData(Instruction instruction)
        {
            data.Add(instruction);
        }
    }
    internal class Instruction
    {
        internal class Register
        {
            public bool simple;
            public string name;
            public Register(bool simple, string name)
            {
                this.simple = simple;
                this.name = name;
            }
        }
        internal class Data : Instruction
        {
            string name;
            string size;
            string value;
            public Data(string name, string size, string value)
            {
                this.name = name;
                this.size = size;
                this.value = value;
            }
        }

        internal class Function : Instruction
        {
            public string name;
            public Function(string name)
            {
                this.name = name;
            }
        }
        
        internal class Class : Instruction
        {
            public string name;
            public Class(string name)
            {
                this.name = name;
            }
        }

        internal class Binary : Instruction
        {
            public string instruction, operand1, operand2;
            public Binary(string instruction, string operand1, string operand2)
            {
                this.instruction = instruction;
                this.operand1 = operand1;
                this.operand2 = operand2;
            }
        }

        internal class Unary : Instruction
        {
            public string instruction, operand;
            public Unary(string instruction, string operand)
            {
                this.instruction = instruction;
                this.operand = operand;
            }
        }

        internal class Zero : Instruction 
        {
            public string instruction;
            public Zero(string instruction)
            {
                this.instruction = instruction;
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
    }
}
