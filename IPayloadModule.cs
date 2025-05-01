namespace zort
{
    public interface IPayloadModule
    {
        void Start();
        void Stop();
        bool RequiresAdmin { get; }
        string ModuleName { get; }
    }
}
