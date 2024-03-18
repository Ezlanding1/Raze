using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal class RegisterState
{

    internal enum RegisterStates : byte
    {
        Free,
        // Prevents the register from being alloc-ed somewhere else
        Used,
        // Prevents the register from being freed (unless the 'force' option is specified). However, the register may change
        Locked,
        // Prevents the register from being changed. If use of this register is required, it will move its contents to a new register first
        Needed
    }
    private byte data;

    public bool HasState(RegisterStates state) => state switch
    {
        RegisterStates.Free => (data & (1 << 0)) == 0,
        RegisterStates.Used => (data & (1 << 0)) > 0,
        RegisterStates.Locked => (data & (1 << 1)) > 0,
        RegisterStates.Needed => (data & (1 << 2)) > 0
    };

    public void SetState(RegisterStates state) => data = state switch
    {
        RegisterStates.Free => 0,
        RegisterStates.Used => (byte)(data | (1 << 0)),
        RegisterStates.Locked => (byte)(data | (1 << 1)),
        RegisterStates.Needed => (byte)(data | (1 << 2))
    };
    public void RemoveState(RegisterStates state) => data = state switch
    {
        RegisterStates.Free => (byte)(data | (1 << 0)),
        RegisterStates.Used => 0,
        RegisterStates.Locked => (byte)(data & ~(1 << 1)),
        RegisterStates.Needed => (byte)(data & ~(1 << 2))
    };

    public void SetState(RegisterState state) => this.data = state.data;

    public void SetState(params RegisterStates[] states)
    {
        foreach (RegisterStates state in states)
        {
            this.SetState(state);
        }
    }
}

internal class NeedableRegisterState : RegisterState
{
    public AssemblyExpr.Register.RegisterSize neededSize;
    public int idx;
}