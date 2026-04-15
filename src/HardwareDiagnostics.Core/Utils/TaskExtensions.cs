using System.Diagnostics;
using System.Threading.Tasks;

namespace HardwareDiagnostics.Core.Utils
{
    public static class TaskExtensions
    {
        public static Task WaitForExitAsync(this Process process)
        {
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (process.HasExited)
            {
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }
    }
}
