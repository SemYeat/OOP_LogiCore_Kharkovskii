namespace LogiCore.App;

internal static class Program
{
    private static void Main(string[] args)
    {
        ApplicationState state = Demo.Run();
        if (!args.Contains("--demo-only")) Menu.Run(state);
    }
}
