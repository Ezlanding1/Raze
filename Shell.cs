namespace Espionage
{
    internal class Shell
    {
        static void Main(string[] args)
        {
            if (args.Length == 1 && args[0].EndsWith(".es"))
            {
                Run(File.ReadAllText(args[0]));
            }
            else
            {
                Console.WriteLine("Usage: Espionage [script].es");
                Environment.Exit(64);
            }
        }

        static void Run(string text)
        {

        }
    }
}