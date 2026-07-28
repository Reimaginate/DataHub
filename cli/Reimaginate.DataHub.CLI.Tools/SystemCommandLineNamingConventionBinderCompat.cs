namespace System.CommandLine.NamingConventionBinder;

public static class CommandHandler
{
    public static Delegate Create(Delegate handler) => handler;

    public static Delegate Create<T1, T2, T3>(Func<T1, T2, T3, Task<int>> handler) => handler;
}
