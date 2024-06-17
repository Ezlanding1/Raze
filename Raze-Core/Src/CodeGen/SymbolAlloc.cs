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
        public Expr.Definition current;
        private Stack<int> frameSize = new();

        public void UpContext()
        {
            if (current == null)
                Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Up Context Called On 'GLOBAL' context (no enclosing) in assembler"));

            current = (Expr.Definition)current.enclosing;
        }

        public static void AllocateVariable(Expr.Definition current, Expr.StackData variable, AssemblyExpr.Register.RegisterName registerName=AssemblyExpr.Register.RegisterName.RBP)
        {
            var variableSize = variable._ref ? 8 : variable.size;
            current.size += variableSize;
            variable.value = new AssemblyExpr.Pointer(registerName, current.size, variableSize);
        }
        public void AllocateVariable(Expr.StackData variable, AssemblyExpr.Register.RegisterName registerName=AssemblyExpr.Register.RegisterName.RBP) =>
            AllocateVariable(current, variable, registerName);

        public void AllocateParameters(Expr.Function function, CodeGen codeGen)
        {
            bool instance = !function.modifiers["static"];

            if (instance)
            {
                codeGen.Emit(new AssemblyExpr.Binary(AssemblyExpr.Instruction.MOV, new AssemblyExpr.Pointer(8, 8), new AssemblyExpr.Register(InstructionUtils.paramRegister[0], 8)));
                function.size += (int)InstructionUtils.SYS_SIZE;
            }

            for (int i = 0; i < function.Arity; i++)
            {
                int instanceCount = i + Convert.ToInt32(instance);

                var paramExpr = function.parameters[i];

                AllocateParameter(paramExpr.stack, instanceCount, function.Arity);

                if (instanceCount < InstructionUtils.paramRegister.Length)
                {
                    if (paramExpr.modifiers["ref"])
                    {
                        codeGen.Emit(new AssemblyExpr.Binary(
                            AssemblyExpr.Instruction.MOV,
                            new AssemblyExpr.Pointer(-paramExpr.stack.ValueAsPointer.offset, 8),
                            new AssemblyExpr.Register(InstructionUtils.paramRegister[instanceCount], 8)
                        ));
                    }
                    else
                    {
                        codeGen.Emit(new AssemblyExpr.Binary(
                            GetMoveInstruction(type: paramExpr.stack.type),
                            new AssemblyExpr.Pointer(-paramExpr.stack.ValueAsPointer.offset, paramExpr.stack.size),
                            IsFloatingType(paramExpr.stack.type) ? 
                                new AssemblyExpr.Register(InstructionUtils.storageRegisters[instanceCount + InstructionUtils.SseRegisterOffset].Name, 16) :
                                new AssemblyExpr.Register(InstructionUtils.paramRegister[instanceCount], paramExpr.stack.size)
                        ));
                    }
                }
            }
        }

        private void AllocateParameter(Expr.StackData parameter, int i, int arity)
        {
            if (i < InstructionUtils.paramRegister.Length)
            {
                AllocateVariable(parameter);
            }
            else
            {
                var variableSize = parameter._ref ? 8 : parameter.size;
                parameter.value = new AssemblyExpr.Pointer(-((8 * (arity - i)) + 8), variableSize);
            }
        }

        public void InitializeFunction(Expr.Function function, CodeGen codeGen)
        {
            fncPushPreserved = new(codeGen.assembly.text.Count);
            current = function;
            CreateBlock();
        }

        public void CreateBlock() => frameSize.Push(current.size);

        public void RemoveBlock()
        {
            if (current is Expr.Function)
            {
                fncPushPreserved.size = Math.Max(fncPushPreserved.size, current.size);
            }
            current.size = frameSize.Pop();
        }
    }
}
