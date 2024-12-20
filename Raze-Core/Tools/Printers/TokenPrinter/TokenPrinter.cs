﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze.Tools;

public class InputPrinter
{
    public static void PrintInput(FileInfo fileName)
    {
        Console.WriteLine(File.ReadAllText(fileName.FullName));
    }
}
public class TokenPrinter
{
    public static void PrintTokens(List<Token> tokens)
    {
        foreach (var item in tokens)
        {
            string str = item.ToString();
            Console.WriteLine(str);
        }
    }
}
