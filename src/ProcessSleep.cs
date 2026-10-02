using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace QltkAccounts
{
    public sealed class ProcessSleep : IDisposable
    {
        readonly Process process;
        readonly Dictionary<int, IntPtr> suspended = new Dictionary<int, IntPtr>();
        bool fullySleeping;
        public ProcessSleep(Process process) { this.process = process; }
        public bool IsSleeping { get { return suspended.Count > 0; } }
        public bool FullySleeping { get { return fullySleeping && IsSleeping; } }
        public bool Sleep(out string error)
        {
            error = null;
            if (IsSleeping) { if (FullySleeping) return true; error = "Còn luồng chưa thức; bấm Thức để thử lại."; return false; }
            try {
                if (process.HasExited) return true;
                for (int pass = 0; pass < 4; pass++) {
                    bool added = false;
                    process.Refresh();
                    foreach (ProcessThread thread in process.Threads) {
                        try {
                            int id = thread.Id; if (suspended.ContainsKey(id)) continue;
                            IntPtr handle = OpenThread(0x00100042, false, (uint)id); // synchronize, query, suspend/resume
                            if (handle == IntPtr.Zero) {
                                int nativeError = Marshal.GetLastWin32Error();
                                if (nativeError == 87) continue; // thread exited during enumeration
                                throw new Win32Exception(nativeError);
                            }
                            bool held = false;
                            try {
                                if (GetProcessIdOfThread(handle) != (uint)process.Id) throw new InvalidOperationException("Luồng không thuộc tab này.");
                                if (WaitForSingleObject(handle, 0) == 0) continue;
                                if (SuspendThread(handle) == uint.MaxValue) {
                                    int nativeError = Marshal.GetLastWin32Error();
                                    if (WaitForSingleObject(handle, 0) == 0) continue;
                                    throw new Win32Exception(nativeError);
                                }
                                suspended.Add(id, handle); held = true; added = true;
                            } finally { if (!held) CloseHandle(handle); }
                        } finally { thread.Dispose(); }
                    }
                    if (!added) { fullySleeping = IsSleeping; return true; }
                }
                throw new InvalidOperationException("Tab đang tạo thêm luồng; đợi tải xong rồi thử Ngủ lại.");
            } catch (Exception ex) {
                string ignored; Wake(out ignored);
                error = ex.Message + (IsSleeping ? " Còn luồng chưa thức; bấm Thức để thử lại." : "");
                return false;
            }
        }
        public bool Wake(out string error)
        {
            error = null; fullySleeping = false;
            foreach (var thread in suspended.ToArray()) {
                uint ended = WaitForSingleObject(thread.Value, 0);
                if (ended == 0 || ResumeThread(thread.Value) != uint.MaxValue || WaitForSingleObject(thread.Value, 0) == 0) {
                    CloseHandle(thread.Value); suspended.Remove(thread.Key);
                } else error = "Không đánh thức được một số luồng: " + new Win32Exception(Marshal.GetLastWin32Error()).Message;
            }
            return !IsSleeping;
        }
        public void Dispose()
        {
            string ignored; Wake(out ignored);
            // A terminated process needs no resume; release any remaining handles.
            if (process.HasExited) foreach (var handle in suspended.Values.ToArray()) CloseHandle(handle);
            if (process.HasExited) suspended.Clear();
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenThread(uint access, bool inherit, uint threadId);
        [DllImport("kernel32.dll", SetLastError = true)] static extern uint SuspendThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] static extern uint GetProcessIdOfThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);
    }
}
