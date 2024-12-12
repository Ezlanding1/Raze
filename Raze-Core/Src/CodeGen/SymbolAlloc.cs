using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;
public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal partial class RegisterAlloc
    {
        public Expr.Definition Current { get => currentInlined ?? current; set => current = value; }
        private Expr.Definition current;
        public Expr.Function? currentInlined;

        private Stack<int> frameSize = new();

        public void UpContext()
        {
            if (current == null)
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Up Context Called On 'GLOBAL' context (no enclosing) in assembler"));

            current = (Expr.Definition)Current.enclosing;
        }

        public static void AllocateHeapVariable(Expr.Definition current, Expr.StackData variable, AssemblyExpr.Register.RegisterName registerName = AssemblyExpr.Register.RegisterName.RBP)
        {
            var variableSize = variable._ref ? 8 : variable.size;
            variable.value = new AssemblyExpr.Pointer(registerName, current.size, (AssemblyExpr.Register.RegisterSize)variableSize);
            current.size += variableSize;
        }
        public void AllocateVariable(Expr.StackData variable, AssemblyExpr.Register.RegisterName registerName=AssemblyExpr.Register.RegisterName.RBP)
        {
            var variableSize = variable._ref ? 8 : variable.size;
            current.size += variableSize;
            variable.value = new AssemblyExpr.Pointer(registerName, -current.size, (AssemblyExpr.Register.RegisterSize)variableSize);
        }

        public void AllocateParameters(Expr.Function function, CodeGen codeGen)
        {
            bool instance = !function.modifiers["static"];

            (var paramRegisters, var floatParamRegisters) = InstructionUtils.GetParamRegisters();

            if (instance)
            {
                var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(function);
                int size = enclosing!.allocSize;
                codeGen.Emit(new AssemblyExpr.Binary(
                    AssemblyExpr.Instruction.MOV, 
                    new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, -size, (AssemblyExpr.Register.RegisterSize)size), 
                    new AssemblyExpr.Register(paramRegisters[0], size))
                );
                function.size += size;
            }

            for (int i = 0; i < function.Arity; i++)
            {
                int instanceCount = i + Convert.ToInt32(instance);

                var paramExpr = function.parameters[i];

                AllocateParameter(paramExpr.stack, instanceCount, function.Arity);

                if (instanceCount < paramRegisters.Length)
                {
                    if (paramExpr.modifiers["ref"])
                    {
                        codeGen.Emit(new AssemblyExpr.Binary(
                            AssemblyExpr.Instruction.MOV,
                            new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, paramExpr.stack.ValueAsPointer.offset, InstructionUtils.SYS_SIZE),
                            new AssemblyExpr.Register(paramRegisters[instanceCount], 8)
                        ));
                    }
                    else
                    {
                        codeGen.Emit(new AssemblyExpr.Binary(
                            GetMoveInstruction(false, paramExpr.stack.type),
                            new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, paramExpr.stack.ValueAsPointer.offset, (AssemblyExpr.Register.RegisterSize)paramExpr.stack.size),
                            IsFloatingType(paramExpr.stack.type) ? 
                                new AssemblyExpr.Register(floatParamRegisters[instanceCount], 16) :
                                new AssemblyExpr.Register(paramRegisters[instanceCount], paramExpr.stack.size)
                        ));
                    }
                }
            }
        }

        private void AllocateParameter(Expr.StackData parameter, int i, int arity)
        {
            if (i < InstructionUtils.GetParamRegisters(false).Length)
            {
                AllocateVariable(parameter);
            }
            else
            {
                var variableSize = parameter._ref ? 8 : parameter.size;
                parameter.value = new AssemblyExpr.Pointer(AssemblyExpr.Register.RegisterName.RBP, ((8 * (arity - i)) + 8), (AssemblyExpr.Register.RegisterSize)variableSize);
            }
        }

        public void InitializeFunction(Expr.Function function)
        {
            Current = function;
            CreateBlock();
            FunctionPushPreserved.totalSizes.Add(0);
        }

        public void CreateBlock() => frameSize.Push(Current.size);
        public void RemoveBlock()
        {
            if (Current is Expr.Function)
            {
                FunctionPushPreserved.totalSizes[^1] = Math.Max(FunctionPushPreserved.totalSizes[^1], Current.size);
            }
            Current.size = frameSize.Pop();
        }
    }
}
