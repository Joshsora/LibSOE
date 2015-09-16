namespace SOE.Database
{
    public interface IDatabaseBackend
    {
        void Connect(string username, string password, string host, int port);
        void Setup(string[] tables);
    }
}
