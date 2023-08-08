﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

internal abstract partial class ExprUtils
{
    public class Modifiers
    {
        private (string, bool)[] modifiers;

        public Modifiers(params string[] modifierNames) 
        {
            this.modifiers = new (string, bool)[modifierNames.Length];

            for (int i = 0; i < modifierNames.Length; i++)
            {
                modifiers[i] = new(modifierNames[i], false);
            }
        }

        public IEnumerable<string> EnumerateTrueModifiers()
        {
            return modifiers.Where(modifier => modifier.Item2).Select(modifier => modifier.Item1);
        }

        public bool ContainsModifier(string s)
        {
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Item1 == s)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetModifier(string s)
        {
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Item1 == s)
                {
                    return i;
                }
            }
            throw new Errors.ImpossibleError("Requested parameter not found");
        }

        public bool this[string s]
        {
            get { return modifiers[GetModifier(s)].Item2; }
            set { modifiers[GetModifier(s)].Item2 = value; }
        }
    }
}