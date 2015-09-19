using System.Collections.Generic;
using System.Threading.Tasks;

namespace SOE.Database
{
    public interface IDatabaseBackend
    {
        // Setup
        void Connect(string host, int port);
        void Setup(string[] tables);
        
        // Very non-specified command run
        Task<TResult> RunCommand<TResult>(string table, string command);

        // Very specific command runs
        Task<TResult> Query<TResult>(string table, Dictionary<string, dynamic> filter) where TResult : new();
        // Task<uint> Insert(string table, dynamic obj);
        // Task<bool> Delete(string table, uint id);
    }
}
