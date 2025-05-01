using System;

namespace zort
{
    public interface IPayloadModule
    {
        void Start();
        void Stop();
        ElevationType ElevationType { get; }
        string ModuleName { get; }
    }

    public enum ElevationType
    {
        Both,
        Elevated,
        NonElevated
    }

    public static class ModuleLogger
    {
        public static void Log(IPayloadModule module, string message)
        {
            Console.WriteLine($"## [{module.ModuleName}] {message}");
        }
        public static void Log(Type moduleType, string message)
        {
            Console.WriteLine($"## [{moduleType.Name}] {message}");
        }
    }
}
