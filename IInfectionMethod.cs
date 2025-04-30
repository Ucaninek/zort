namespace zort
{
    public interface IInfectionMethod
    {
        void Start();
        void Stop();
        bool RequiresAdmin { get; }
    }
}
