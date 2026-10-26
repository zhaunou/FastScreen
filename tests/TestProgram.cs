using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalScreenshot
{
    static class TestProgram
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 1 || (args.Length == 1 && args[0] != "--full" && args[0] != "--render" && args[0] != "--logic"))
            { Console.Error.WriteLine("Usage: FastScreen.Tests.exe [--logic|--render|--full]"); return 2; }
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastScreen", "TestResults", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string report = Path.Combine(root, "report.txt");
            try
            {
                using (LocalFolderGuard guard = LocalFolderGuard.Acquire(root, true))
                {
                    try
                    {
                        string mode = args.Length == 0 ? "--logic" : args[0];
                        if (mode == "--full") SelfTest.Run(report);
                        else if (mode == "--render") BehaviorTests.RenderInterface(report);
                        else BehaviorTests.Run(report);
                        Console.WriteLine(File.ReadAllText(report));
                        Console.WriteLine("Results: " + root);
                        return 0;
                    }
                    catch (Exception ex) { File.AppendAllText(report, "FAIL: " + ex + Environment.NewLine); throw; }
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
