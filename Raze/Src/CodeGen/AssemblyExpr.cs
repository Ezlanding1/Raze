using System.Runtime.CompilerServices;

namespace Raze;

public abstract partial class AssemblyExpr
{
    public abstract T Accept<T>(IVisitor<T> visitor);

    public interface IVisitor<T>
    {
        public T VisitGlobal(Global instruction);
        public T VisitSection(Section instruction);
        public T VisitData(Data instruction);
        public T VisitProcedure(Procedure instruction);
        public T VisitLocalProcedure(LocalProcedure instruction);
        public T VisitBinary(Binary instruction);
        public T VisitUnary(Unary instruction);
        public T VisitZero(Zero instruction);
        public T VisitComment(Comment instruction);
    }

    public abstract class TopLevelExpr : AssemblyExpr { }
    public abstract class TextExpr : AssemblyExpr { }
    public abstract class DataExpr : AssemblyExpr { }

    public class Global : TopLevelExpr
    {
        public string name;
        public Global(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitGlobal(this);
        }
    }

    public class Section : TopLevelExpr
    {
        public string name;
        public Section(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitSection(this);
        }
    }

    public abstract class Value : Operand
    {
        // 0 = Register, 1 = Pointer, 2 = Literal
        public int valueType;

        public Value(int valueType)
        {
            this.valueType = valueType;
        }

        public bool IsRegister() => valueType == 0;
        public bool IsPointer() => valueType == 1;
        public bool IsLiteral() => valueType == 2;

        internal Register NonPointerNonLiteral(Register.RegisterSize? size, CodeGen assembler) =>
            (Register)this.NonPointer(assembler).NonLiteral(size, assembler);

        public Value NonPointer(CodeGen assembler)
        {
            if (IsPointer())
            {
                Register reg = ((Pointer)this).AsRegister(((Pointer)this).size, assembler);
                assembler.Emit(new Binary(Instruction.MOV, reg, this));
                return reg;
            }
            return this;
        }
        internal SizedValue NonLiteral(Register.RegisterSize? size, CodeGen assembler)
        {
            if (IsLiteral())
            {
                if (size == null) { Diagnostics.errors.Push(new Error.ImpossibleError("Null size in NonLiteral when operand is literal")); }

                assembler.Emit(new Binary(Instruction.MOV, assembler.alloc.CurrentRegister((Register.RegisterSize)size), this));
                return assembler.alloc.NextRegister((Register.RegisterSize)size);
            }
            return (SizedValue)this;
        }
    }

    public abstract class SizedValue : Value
    {
        public Register.RegisterSize size;

        internal abstract Register AsRegister(Register.RegisterSize size, CodeGen assembler);

        public SizedValue(int valueType) : base(valueType)
        {

        }
    }

    public class Register : SizedValue
    {
        public enum RegisterSize
        {
            _64Bits = 8,
            _32Bits = 4,
            _16Bits = 2,
            _8BitsUpper = 0,
            _8Bits = 1
        }

        // Register Encoding => if RRegister: -RegCode-1 else: RegCode. TMP = -9
        public enum RegisterName : sbyte
        {
            TMP = -9,
            RAX = 0b000,
            RCX = 0b001,
            RDX = 0b010,
            RBX = 0b011,
            RSI = 0b110,
            RDI = 0b111,
            RSP = 0b100,
            RBP = 0b101,
            R8 = -0b000 - 1,
            R9 = -0b001 - 1,
            R10 = -0b010 - 1,
            R11 = -0b011 - 1,
            R12 = -0b100 - 1,
            R13 = -0b101 - 1,
            R14 = -0b110 - 1,
            R15 = -0b111 - 1
        }

        public StrongBox<RegisterName> nameBox;
        public RegisterName Name
        {
            get => nameBox.Value;
            set => nameBox.Value = value;
        }

        public Register(RegisterName register, RegisterSize size) : base(0)
        {
            this.nameBox = new(register);
            this.size = size;
        }

        public Register(RegisterName register, int size) : this(register, InstructionUtils.ToRegisterSize(size))
        {
        }

        public Register(StrongBox<RegisterName> _name, RegisterSize size) : base(0)
        {
            this.nameBox = _name;
            this.size = size;
        }

        internal override Register AsRegister(RegisterSize size, CodeGen assembler)
        {
            return new Register(this.name, size);
        }

        public override T Accept<T>(IUnaryOperandVisitor<T> visitor)
        {
            return visitor.VisitRegister(this);
        }

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            return new(Assembler.Encoder.Operand.RegisterOperandType(this), (int)size);
        }

        public override T Accept<T>(IBinaryOperandVisitor<T> visitor, Operand operand)
        {
            return operand.VisitOperandRegister(visitor, this);
        }
        public override T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg)
        {   
            return visitor.VisitRegisterRegister(this, reg);
        }
        public override T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr)
        {
            return visitor.VisitRegisterMemory(this, ptr);
        }
        public override T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm)
        {
            return visitor.VisitRegisterImmediate(this, imm);
        }
    }

    public class Pointer : SizedValue
    {
        internal Register register;
        public int offset;

        internal Pointer(Register register, int offset, Register.RegisterSize size) : base(1)
        {
            this.register = register;
            this.size = size;
            this.offset = -offset;
        }
        internal Pointer(Register register, int offset, int size) : this(register, offset, InstructionUtils.ToRegisterSize(size))
        {
        }
        internal Pointer(Register.RegisterName register, int offset, Register.RegisterSize size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }
        internal Pointer(Register.RegisterName register, int offset, int size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }
        internal Pointer(int offset, Register.RegisterSize size) : this(Register.RegisterName.RBP, offset, size)
        {
        }
        public Pointer(int offset, int size) : this(Register.RegisterName.RBP, offset, size)
        {
        }

        public bool IsOnStack() => register.name == Register.RegisterName.RBP;

        internal override Register AsRegister(Register.RegisterSize size, CodeGen assembler)
        {
            if (!IsOnStack())
            {
                return new Register(register.name, size);
            }
            return assembler.alloc.NextRegister(size);
        }

        public override T Accept<T>(IUnaryOperandVisitor<T> visitor)
        {
            return visitor.VisitMemory(this);
        }

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            Assembler.Encoder.Operand.ThrowTMP(register);
            return new(Assembler.Encoder.Operand.OperandType.M, (int)size);
        }

        public override T Accept<T>(IBinaryOperandVisitor<T> visitor, Operand operand)
        {
            return operand.VisitOperandMemory(visitor, this);
        }
        public override T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm) => visitor.VisitMemoryImmediate(this, imm);
        public override T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr) => visitor.VisitMemoryMemory(this, ptr);
        public override T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg) => visitor.VisitMemoryRegister(this, reg);
    }

    public class Literal : Value
    {
        internal enum LiteralType
        {
            Integer = Parser.LiteralTokenType.Integer,
            UnsignedInteger = Parser.LiteralTokenType.UnsignedInteger,
            Floating = Parser.LiteralTokenType.Floating,
            String = Parser.LiteralTokenType.String,
            Binary = Parser.LiteralTokenType.Binary,
            Hex = Parser.LiteralTokenType.Hex,
            Boolean = Parser.LiteralTokenType.Boolean,
            RefData,
            RefProcedure,
            RefLocalProcedure
        }
        internal LiteralType type;
        public string value;

        internal Literal(LiteralType type, string value) : base(2)
        {
            this.type = type;
            this.value = value;
        }

        public override T Accept<T>(IUnaryOperandVisitor<T> visitor)
        {
            return visitor.VisitImmediate(this);
        }

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            return new(Assembler.Encoder.Operand.OperandType.IMM, (Assembler.Encoder.Operand.OperandSize)CodeGen.SizeOfLiteral(this));
        }

        public override T Accept<T>(IBinaryOperandVisitor<T> visitor, Operand operand)
        {
            return operand.VisitOperandImmediate(visitor, this);
        }

        public override T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm)
        {
            Assembler.Encoder.EncodingUtils.ThrowIvalidEncodingType("Immediate", "Immediate");
            return default;
        }
        public override T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr)
        {
            Assembler.Encoder.EncodingUtils.ThrowIvalidEncodingType("Immediate", "Memory");
            return default;
        }
        public override T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg)
        {
            Assembler.Encoder.EncodingUtils.ThrowIvalidEncodingType("Immediate", "Register");
            return default;
        }
    }

    public class Data : DataExpr
    {
        public string name;
        public Register.RegisterSize size;
        internal (Literal.LiteralType type, string) value;

        internal Data(string name, Register.RegisterSize size, (Literal.LiteralType type, string) value)
        {
            this.name = name;
            this.size = size;
            this.value = value;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitData(this);
        }
    }

    public class DataRef : Literal
    {
        public DataRef(string dataName) : base(LiteralType.RefData, dataName)
        {
        }
    }

    public class Procedure : TextExpr
    {
        public string name;
        public Procedure(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitProcedure(this);
        }
    }

    public class LocalProcedure : TextExpr
    {
        public string name;
        public LocalProcedure(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitLocalProcedure(this);
        }
    }
    
    public class ProcedureRef : Literal
    {
        public ProcedureRef(string name) : base(LiteralType.RefProcedure, name)
        {
        }
    }

    public class LocalProcedureRef : Literal
    {
        public LocalProcedureRef(string name) : base(LiteralType.RefLocalProcedure, name)
        {
        }
    }

    public interface IUnaryOperandVisitor<T>
    {
        public T VisitRegister(Register reg);
        public T VisitMemory(Pointer ptr);
        public T VisitImmediate(Literal imm);
    }
    public interface IBinaryOperandVisitor<T>
    {
        public T VisitRegisterRegister(Register reg1, Register reg2);
        public T VisitRegisterMemory(Register reg1, Pointer ptr2);
        public T VisitRegisterImmediate(Register reg1, Literal imm2);

        public T VisitMemoryRegister(Pointer ptr1, Register reg2);
        public T VisitMemoryMemory(Pointer ptr1, Pointer ptr2);
        public T VisitMemoryImmediate(Pointer ptr1, Literal imm2);
    }
    public abstract class Operand
    {
        public abstract T Accept<T>(IBinaryOperandVisitor<T> visitor, Operand operand);
        public abstract T Accept<T>(IUnaryOperandVisitor<T> visitor);

        public abstract T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg);
        public abstract T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr);
        public abstract T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm);

        public abstract Assembler.Encoder.Operand ToAssemblerOperand();
    }


    public class Binary : TextExpr
    {
        public Instruction instruction;
        public Operand operand1, operand2;

        public Binary(Instruction instruction, Operand operand1, Operand operand2)
        {
            this.instruction = instruction;
            this.operand1 = operand1;
            this.operand2 = operand2;
        }

        internal Binary(Instruction instruction, Register.RegisterName operand1, AssemblyExpr.Register.RegisterName operand2)
            : this(instruction, new Register(operand1, Register.RegisterSize._64Bits), new Register(operand2, Register.RegisterSize._64Bits))
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitBinary(this);
        }
    }

    public class Unary : TextExpr
    {
        public Operand operand;
        public Instruction instruction;

        public Unary(Instruction instruction, Operand operand)
        {
            this.instruction = instruction;
            this.operand = operand;
        }

        internal Unary(Instruction instruction, AssemblyExpr.Register.RegisterName operand) : this(instruction, new AssemblyExpr.Register(operand, Register.RegisterSize._64Bits))
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnary(this);
        }
    }

    public class Zero : TextExpr
    {
        public Instruction instruction;
        public Zero(Instruction instruction)
        {
            this.instruction = instruction;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitZero(this);
        }
    }

    public class Comment : TopLevelExpr
    {
        public string comment;
        public Comment(string comment)
        {
            this.comment = comment;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitComment(this);
        }
    }
}
