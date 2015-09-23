using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SOE.Database
{
    public interface IDatabaseBackend
    {
        // Setup
        void Connect(string host, int port);
        Task<bool> Setup(Type[] storables);
        
        // Very non-specified command run
        Task<TResult> RunCommand<TResult>(string command);

        // Very specific command runs
        Task<TResult> Query<TResult>(Dictionary<string, dynamic> filter) where TResult : new();
        Task<bool> Insert(dynamic obj);
        Task<bool> Update<TResult>(Dictionary<string, dynamic> filter, Dictionary<string, dynamic> update);

        // Misc
        bool IsStorable(Type type);
    }
}
