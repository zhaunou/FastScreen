using System;
using System.Security.Principal;
using System.Threading;

namespace LocalScreenshot
{
    sealed class SingleInstanceGate : IDisposable
    {
        readonly Mutex mutex;
        readonly string eventName;
        public bool IsPrimary { get; private set; }
        public EventWaitHandle Activation { get; private set; }

        public SingleInstanceGate(string name)
        {
            eventName = name + ".Activate";
            mutex = new Mutex(false, name + ".Mutex");
            try
            {
                try { IsPrimary = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { IsPrimary = true; }
                if (IsPrimary) Activation = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            }
            catch { Dispose(); throw; }
        }

        public static SingleInstanceGate ForCurrentUser()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                return new SingleInstanceGate(@"Local\FastScreen." + identity.User.Value);
        }

        public bool NotifyPrimary()
        {
            // A second process may arrive before the first has created its event.
            for (int attempt = 0; attempt < 40; attempt++)
            {
                try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting(eventName)) { signal.Set(); return true; } }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(50); }
            }
            return false;
        }

        public void Dispose()
        {
            if (Activation != null) { Activation.Dispose(); Activation = null; }
            if (IsPrimary) { IsPrimary = false; mutex.ReleaseMutex(); }
            mutex.Dispose();
        }
    }
}
