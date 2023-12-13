using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Assembler
{
    public partial struct Instruction
    {
        // Immediate Data. (1, 2, or 4 bytes). Optional

        // 1 Byte Immediate
        internal interface Immediate8 : IInstruction 
        {
            public static Immediate8SByte Generate(sbyte _data) => new(_data);
            public static Immediate8Half Generate(Half _data) => new(_data);
            public static Immediate8Byte Generate(char _data) => new((byte)_data);
            public static Immediate8Byte Generate(byte _data) => new(_data);
            public static Immediate8Byte Generate(bool _data) => new(Convert.ToByte(_data));
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate8SByte : Immediate8
        {
            sbyte _data;
            internal Immediate8SByte(sbyte _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate8Half : Immediate8
        {
            Half _data;
            internal Immediate8Half(Half _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate8Byte : Immediate8
        {
            byte _data;
            internal Immediate8Byte(byte _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }


        // 2 Byte Immediate
        internal interface Immediate16 : IInstruction
        {
            public static Immediate16Short Generate(short _data) => new(_data);
            public static Immediate16Half Generate(Half _data) => new(_data);
            public static Immediate16UShort Generate(string _data)
            {
                ushort res = 0;
                for (int i = 0; i < 2; i++)
                    res = (ushort)((res << 8) | _data[i]);
                return new(res);
            }
            public static Immediate16UShort Generate(ushort _data) => new(_data);
            public static Immediate16UShort Generate(bool _data) => new(Convert.ToUInt16(_data));
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate16Short : Immediate16
        {
            short _data;
            internal Immediate16Short(short _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate16Half : Immediate16
        {
            Half _data;
            internal Immediate16Half(Half _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate16UShort : Immediate16
        {
            ushort _data;
            internal Immediate16UShort(ushort _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }


        // 4 Byte Immediate
        internal interface Immediate32 : IInstruction
        {
            public static Immediate32Int Generate(int _data) => new(_data);
            public static Immediate32Float Generate(float _data) => new(_data);
            public static Immediate32UInt Generate(string _data)
            {
                uint res = 0;
                for (int i = 0; i < 4; i++)
                    res = (uint)((res << 8) | _data[i]);
                return new(res);
            }
            public static Immediate32UInt Generate(uint _data) => new(_data);
            public static Immediate32UInt Generate(bool _data) => new(Convert.ToUInt32(_data));
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate32Int : Immediate32
        {
            int _data;
            internal Immediate32Int(int _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate32Float : Immediate32
        {
            float _data;
            internal Immediate32Float(float _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate32UInt : Immediate32
        {
            uint _data;
            internal Immediate32UInt(uint _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }


        // 8 byte immediate
        internal interface Immediate64 : IInstruction
        {
            public static Immediate64Long Generate(long _data) => new(_data);
            public static Immediate64Double Generate(double _data) => new(_data);
            public static Immediate64ULong Generate(string _data)
            {
                ulong res = 0;
                for (int i = 0; i < 8; i++)
                    res = (ulong)((res << 8) | _data[i]);
                return new(res);
            }
            public static Immediate64ULong Generate(ulong _data) => new(_data);
            public static Immediate64ULong Generate(bool _data) => new(Convert.ToUInt64(_data));
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate64Long : Immediate64
        {
            long _data;
            internal Immediate64Long(long _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate64Double : Immediate64
        {
            double _data;
            internal Immediate64Double(double _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Immediate64ULong : Immediate64
        {
            ulong _data;
            internal Immediate64ULong(ulong _data) { this._data = _data; }

            public byte[] GetBytes() => BitConverter.GetBytes(this._data);
        }
    }
}
