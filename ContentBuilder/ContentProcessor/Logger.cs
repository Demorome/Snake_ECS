using System;

static class Logger
{
    public static void LogWarn(string str)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("WARN: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(str);
    }
}