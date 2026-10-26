using System.Reflection;

namespace LocalScreenshot
{
    static class AppInfo
    {
        public static readonly string Version = ((AssemblyInformationalVersionAttribute)
            Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion;
        public static string DisplayName { get { return "FastScreen " + Version; } }
    }
}
