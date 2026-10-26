using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using LocalScreenshot;

static class SingleInstanceTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Process Child(string mode, string name, string path)
    {
        return Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
            mode + " " + name + (path == null ? "" : " \"" + path + "\"")) { UseShellExecute = false, CreateNoWindow = true });
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0)
            {
                using (SingleInstanceGate gate = new SingleInstanceGate(args[1]))
                {
                    if (args[0] == "contend") return !gate.IsPrimary && gate.NotifyPrimary() ? 0 : 1;
                    if (args[0] == "acquire") return gate.IsPrimary ? 0 : 1;
                    Check(gate.IsPrimary, "crash test owner acquired lock");
                    File.WriteAllText(args[2], "ready");
                    Thread.Sleep(30000);
                }
                return 0;
            }
            string name = @"Local\FastScreen.Test." + Guid.NewGuid().ToString("N");
            using (SingleInstanceGate first = new SingleInstanceGate(name))
            {
                Check(first.IsPrimary, "first process acquired lock");
                using (Process second = Child("contend", name, null))
                {
                    Check(first.Activation.WaitOne(5000), "existing process received activation signal");
                    Check(second.WaitForExit(5000) && second.ExitCode == 0, "second process cannot become primary");
                }
            }
            using (Process next = Child("acquire", name, null))
                Check(next.WaitForExit(5000) && next.ExitCode == 0, "lock released after normal exit");

            // Keep the kernel mutex alive while its owning process is killed.
            string ready = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Guid.NewGuid().ToString("N") + ".ready");
            using (Mutex witness = new Mutex(false, name + ".Mutex"))
            using (Process crashed = Child("crash", name, ready))
            {
                try
                {
                    Stopwatch timeout = Stopwatch.StartNew();
                    while (!File.Exists(ready) && timeout.ElapsedMilliseconds < 5000) Thread.Sleep(25);
                    Check(File.Exists(ready), "crash test process ready");
                    crashed.Kill(); Check(crashed.WaitForExit(5000), "crash test process stopped");
                    using (SingleInstanceGate recovered = new SingleInstanceGate(name))
                        Check(recovered.IsPrimary, "abandoned lock recovered after crash");
                }
                finally { if (!crashed.HasExited) { crashed.Kill(); crashed.WaitForExit(); } if (File.Exists(ready)) File.Delete(ready); }
            }
            Console.WriteLine("PASS: cross-process exclusion; activation signal; normal release; abandoned mutex recovery.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
