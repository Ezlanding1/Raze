using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class Expr
{
    public partial class InlineAssembly : Expr
    {
        public abstract class InlineAssemblyExpr
        {
            public abstract void Accept(CodeGen codeGen);
            public abstract List<Operand> GetOperands();
        }

        public abstract class Instruction : InlineAssemblyExpr
        {
            public bool _return = false;

            public abstract List<Variable?> GetAssignedVars();
        }

        public class BinaryInstruction(AssemblyExpr.Instruction instruction, Operand operand1, Operand operand2)
            : Instruction
        {
            public AssemblyExpr.Instruction instruction = instruction;
            public Operand operand1 = operand1;
            public Operand operand2 = operand2;

            public override void Accept(CodeGen codeGen)
            {
                if (CodeGen.InlineAssemblyOps.supportedInstructionsBinary.TryGetValue(instruction, out var value))
                {
                    value.Item2(codeGen, this);
                    return;
                }
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, instruction.ToString()));
            }

            public override List<Variable?> GetAssignedVars()
            {
                if (CodeGen.InlineAssemblyOps.supportedInstructionsBinary.TryGetValue(instruction, out var value))
                {
                    return value.Item1 switch
                    {
                        0 => [],
                        1 => [operand1.GetVariable()],
                        2 => [operand2.GetVariable()],
                        (1|2) => [operand1.GetVariable(), operand2.GetVariable()],
                    };
                }
                return [];
            }

            public override List<Operand> GetOperands() => [operand1, operand2];
        }

        public class UnaryInstruction(AssemblyExpr.Instruction instruction, Operand operand) 
            : Instruction
        {                             
            public AssemblyExpr.Instruction instruction = instruction;
            public Operand operand = operand;

            public override void Accept(CodeGen codeGen)
            {
                if (CodeGen.InlineAssemblyOps.supportedInstructionsUnary.TryGetValue(instruction, out var value))
                {
                    value.Item2(codeGen, this);
                    return;
                }
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, instruction.ToString()));
            }

            public override List<Variable?> GetAssignedVars()
            {
                if (CodeGen.InlineAssemblyOps.supportedInstructionsUnary.TryGetValue(instruction, out var value))
                {
                    return value.Item1 switch
                    {
                        0 => [],
                        1 => [operand.GetVariable()],
                    };
                }
                return [];
            }

            public override List<Operand> GetOperands() => [operand];
        }

        public class NullaryInstruction(AssemblyExpr.Instruction instruction) 
            : Instruction
        {
            public AssemblyExpr.Instruction instruction = instruction;

            public override void Accept(CodeGen codeGen)
            {
                if (CodeGen.InlineAssemblyOps.supportedInstructionsNullary.TryGetValue(instruction, out var value))
                {
                    value(codeGen, this);
                    return;
                }
                Diagnostics.Report(new Diagnostic.AnalyzerDiagnostic(Diagnostic.DiagnosticName.UnsupportedInstruction, instruction.ToString()));
            }

            public override List<Variable?> GetAssignedVars() => [];

            public override List<Operand> GetOperands() => [];
        }

        public abstract class Operand
        {
            public abstract AssemblyExpr.IValue ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize);
            public abstract Type Type();
            public abstract Variable? GetVariable();
        }

        internal abstract class Register : Operand
        {
            public abstract override AssemblyExpr.Register ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize);
            public override Variable? GetVariable() => null;
        }

        internal class NamedRegister() : Register
        {
            public AssemblyExpr.Register? register;

            private protected NamedRegister(AssemblyExpr.Register? register) : this()
            {
                this.register = register;
            }

            public override AssemblyExpr.Register ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize)
                => register!;

            public override Type Type()
            {
                return AssemblyExpr.Register.IsSseRegister(register.name) ?
                    Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Floating] :
                    Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer];
            }
        }

        internal class StandardRegister(AssemblyExpr.Register? register) : NamedRegister(register)
        {
            public override AssemblyExpr.Register ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize)
                => new(register!.name, register.Size);
        }

        internal class UnnamedRegister(Parser.LiteralTokenType type, AssemblyExpr.Register.RegisterSize size) : Register
        {
            Parser.LiteralTokenType type = type;
            AssemblyExpr.Register.RegisterSize size = size;

            public override AssemblyExpr.Register ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize)
            {
                return codeGen.alloc.CurrentRegister(size, Analyzer.TypeCheckUtils.literalTypes[type]);
            }

            public override Type Type() => Analyzer.TypeCheckUtils.literalTypes[type];
        }

        internal class Pointer(Operand value, int offset, AssemblyExpr.Register.RegisterSize? size) : Operand
        {
            internal Operand value = value;
            public int offset = offset;
            private AssemblyExpr.Register.RegisterSize? size = size;

            public override AssemblyExpr.IValue ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize)
            {
                return new AssemblyExpr.Pointer(value.ToOperand(codeGen, AssemblyExpr.Register.RegisterSize._64Bits).NonPointerNonLiteral(codeGen, value.Type()), offset, size ?? defaultSize);
            }

            public override Type Type() =>
                Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Integer];

            public override Variable? GetVariable() => value as Variable;
        }

        public class Variable(GetReference variable) : Operand
        {
            public GetReference variable = variable;

            public override AssemblyExpr.IValue ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize)
            {
                return variable.Accept(codeGen);
            }

            public override Type Type() => variable.GetLastType();
            public override Variable? GetVariable() => this;
        }

        public class Literal(Expr.Literal literal, Literal.FpSizeSuffix fpSizeSuffix) : Operand
        {
            Expr.Literal literal = literal;
            public enum FpSizeSuffix
            {
                None,
                Float = 4,
                Double = 8
            }
            FpSizeSuffix fpSizeSuffix = fpSizeSuffix;

            public override AssemblyExpr.IValue ToOperand(CodeGen codeGen, AssemblyExpr.Register.RegisterSize defaultSize)
            {
                if (fpSizeSuffix != FpSizeSuffix.None)
                    defaultSize = (AssemblyExpr.Register.RegisterSize)fpSizeSuffix;

                return ((AssemblyExpr.ILiteralBase)literal.Accept(codeGen)!).CreateLiteral(defaultSize);
            }

            public override Type Type()
            {
                return Analyzer.TypeCheckUtils.literalTypes[literal.literal.type];
            }

            public override Variable? GetVariable() => null;
        }

        internal abstract class Alloc(NamedRegister register) : InlineAssemblyExpr
        {
            public NamedRegister register = register;
            public override List<Operand> GetOperands() => [];
        }
        
        internal class NamedAlloc(NamedRegister register, AssemblyExpr.Register.RegisterName name, AssemblyExpr.Register.RegisterSize size) : Alloc(register)
        {
            AssemblyExpr.Register.RegisterName name = name;
            AssemblyExpr.Register.RegisterSize size = size;

            public override void Accept(CodeGen codeGen)
            {
                register.register = codeGen.alloc.ReserveRegister(name, size);
                codeGen.alloc.Lock(register.register);
            }

            public override List<Operand> GetOperands() => [];
        }
        
        internal class UnnamedAlloc(NamedRegister register, Parser.LiteralTokenType type, AssemblyExpr.Register.RegisterSize size) : Alloc(register)
        {
            Parser.LiteralTokenType type = type;
            AssemblyExpr.Register.RegisterSize size = size;

            public override void Accept(CodeGen codeGen)
            {
                register.register = codeGen.alloc.NextRegister(size, Analyzer.TypeCheckUtils.literalTypes[type]);
                codeGen.alloc.Lock(register.register);
            }

            public override List<Operand> GetOperands() => [];
        }

        internal class Free(NamedRegister register) : InlineAssemblyExpr
        {
            NamedRegister register = register;

            public override void Accept(CodeGen codeGen)
            {
                codeGen.alloc.UnlockAndFree(register.register);
            }

            public override List<Operand> GetOperands() => [];
        }

        public class Return(Operand value) : InlineAssemblyExpr
        {
            Operand value = value;

            public override void Accept(CodeGen codeGen)
            {
                ReturnOperand(codeGen, value.ToOperand(codeGen, (AssemblyExpr.Register.RegisterSize)((Function)codeGen.alloc.Current)._returnType.type.allocSize));
            }

            public static void ReturnOperand(CodeGen codeGen, AssemblyExpr.IValue op)
            {
                Function function = (Function)codeGen.alloc.Current;
                DataType retType = function._returnType.type;

                if (((InlinedCodeGen)codeGen).inlineState != null)
                {
                    AssemblyExpr.Instruction instruction = CodeGen.GetMoveInstruction(false, retType);

                    var returnSize = CodeGen.GetRegisterSize(
                        (AssemblyExpr.Register.RegisterSize)retType.allocSize,
                        retType,
                        function.refReturn
                    );

                    ((InlinedCodeGen)codeGen).InlinedReturnIValue(op, instruction, returnSize, retType);
                }
                else
                {
                    var instruction = CodeGen.GetMoveInstruction(false, retType);

                    var _returnRegister =
                        new AssemblyExpr.Register(
                            InstructionUtils.GetCallingConvention().returnRegisters.GetRegisters(function.refReturn, retType)[0],
                            CodeGen.GetRegisterSize(op.Size, retType, function.refReturn)
                        );

                    codeGen.Emit(new AssemblyExpr.Binary(instruction, _returnRegister, op));

                    codeGen.alloc.Free(op);
                }
                
                int retSize = function.refReturn ? 8 : retType.allocSize;
                            
                if (retSize != (int)op.Size)
                {
                    if (!(Analyzer.TypeCheckUtils.literalTypes[Parser.LiteralTokenType.Floating].Matches(retType) && op.Size == AssemblyExpr.Register.RegisterSize._128Bits && (retSize == 4 || retSize == 8)))
                        Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InlineAssemblySizeMismatchReturn_Primitive, op.Size, retType.name.lexeme));
                }
            }

            public override List<Operand> GetOperands() => [value];
        }
    }
}
