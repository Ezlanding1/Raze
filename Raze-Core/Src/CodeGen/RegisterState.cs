using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal struct RegisterState
{
    [Flags]
    internal enum RegisterStates : byte
    {
        Free = 0,
        // Prevents the register from being alloc-ed somewhere else
        Used = 1,
        // Prevents the register from being freed (unless the 'force' option is specified). However, the register may change
        Locked = 2,
        // Prevents the register from being changed. If use of this register is required, it will move its contents to a new register first
        Needed = 4
    }
    private RegisterStates data;

    public bool HasState(RegisterStates state) =>
        state == RegisterStates.Free ?
            data == 0 :
            data.HasFlag(state);

    public void SetState(RegisterStates state) => data =
        state == RegisterStates.Free ?
            0 :
            data | state;

    public void RemoveState(RegisterStates state) => data =
        state == RegisterStates.Used ?
            0 :
            data & ~state;

    public void SetState(RegisterState state) => this.data = state.data;

    public void SetState(params RegisterStates[] states)
    {
        foreach (RegisterStates state in states)
        {
            this.SetState(state);
        }
    }
}
