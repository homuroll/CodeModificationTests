using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

using RGiesecke.DllExport;

namespace ManagedInjector
{
    public static class Injector
    {
        [DllImport("kernel32.dll")]
        private static extern bool GetModuleHandleEx(int dwFlags, IntPtr address, out IntPtr phModule);

        private const int GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, uint processId);

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [Flags]
        public enum ThreadAccess
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200),

            ALL = TERMINATE | SUSPEND_RESUME | GET_CONTEXT | SET_CONTEXT | SET_INFORMATION | QUERY_INFORMATION
                  | SET_THREAD_TOKEN | IMPERSONATE | DIRECT_IMPERSONATION
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
                                                    IntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection
        {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, FreeType dwFreeType);

        [Flags]
        public enum FreeType
        {
            Decommit = 0x4000,
            Release = 0x8000,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
                                                      IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer,
                                                     IntPtr dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(HookType hookType, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

        public enum HookType : int
        {
            WH_JOURNALRECORD = 0,
            WH_JOURNALPLAYBACK = 1,
            WH_KEYBOARD = 2,
            WH_GETMESSAGE = 3,
            WH_CALLWNDPROC = 4,
            WH_CBT = 5,
            WH_SYSMSGFILTER = 6,
            WH_MOUSE = 7,
            WH_HARDWARE = 8,
            WH_DEBUG = 9,
            WH_SHELL = 10,
            WH_FOREGROUNDIDLE = 11,
            WH_CALLWNDPROCRET = 12,
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL = 14
        }

        public enum HookCode
        {
            HC_ACTION = 0,
            HC_GETNEXT = 1,
            HC_SKIP = 2,
            HC_NOREMOVE = 3,
            HC_NOREM = HC_NOREMOVE,
            HC_SYSMODALON = 4,
            HC_SYSMODALOFF = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CWPSTRUCT
        {
            public readonly IntPtr lParam;
            public readonly IntPtr wParam;
            public readonly uint message;
            public readonly IntPtr hwnd;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, HookCode nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return : MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return : MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return : MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll")]
        private static extern void OutputDebugString(string lpOutputString);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint CreateThread(IntPtr lpThreadAttributes, IntPtr dwStackSize, IntPtr lpStartAddress,
                                                IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        private const int CREATE_SUSPENDED = 0x00000004;

        private const string launchMessageName = "Launch_12ba19044dc74318ac7511b2947cdfb8";

        public static unsafe void Launch(IntPtr windowHandle, string assembly, string className, string methodName)
        {
            var assemblyClassAndMethod = assembly + "$" + className + "$" + methodName;
            var acmLocal = new byte[(assemblyClassAndMethod.Length + 1) * sizeof(char)];
            fixed(char* c = assemblyClassAndMethod)
            {
                fixed(byte* b = &acmLocal[0])
                {
                    var source = c;
                    var dest = (char*)b;
                    for(int i = 0; i < assemblyClassAndMethod.Length; ++i)
                        *dest++ = *source++;
                }
            }

            var managedInjectorHandle = GetModuleHandle("ManagedInjector.dll");
            var messageHookProcAddr = GetProcAddress(managedInjectorHandle, "Inject");

            IntPtr hinstDLL;

            if(GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, messageHookProcAddr, out hinstDLL))
            {
                Console.WriteLine("GetModuleHandleEx successful");
                uint processID;
                uint threadID = GetWindowThreadProcessId(windowHandle, out processID);

                if(processID != 0)
                {
                    Console.WriteLine("Got process id");
                    var hProcess = OpenProcess(ProcessAccessFlags.All, false, processID);
                    if(hProcess != IntPtr.Zero)
                    {
                        Console.WriteLine("Got process handle");
                        var bufLen = acmLocal.Length;
                        IntPtr acmRemote = VirtualAllocEx(hProcess, IntPtr.Zero, new IntPtr(bufLen), AllocationType.Commit, MemoryProtection.ReadWrite);

                        if(acmRemote != IntPtr.Zero)
                        {
                            Console.WriteLine("VirtualAllocEx successful");

                            IntPtr bytesWritten;
                            if(WriteProcessMemory(hProcess, acmRemote, acmLocal, new IntPtr(bufLen), out bytesWritten) || bytesWritten.ToInt32() != bufLen)
                            {
                                Console.WriteLine("Successfully wrote to process memory");

                                var messageHookHandle = SetWindowsHookEx(HookType.WH_CALLWNDPROC, messageHookProcAddr, hinstDLL, threadID);

                                if(messageHookHandle != IntPtr.Zero)
                                {
                                    Console.WriteLine("SetWindowsHookEx successful");
                                    var result = VirtualAllocEx(hProcess, IntPtr.Zero, new IntPtr(4), AllocationType.Commit, MemoryProtection.ReadWrite);

                                    Console.WriteLine(launchMessage);
                                    SendMessage(windowHandle, launchMessage, acmRemote, result);

                                    UnhookWindowsHookEx(messageHookHandle);
                                    var buf = new byte[4];
                                    IntPtr bytesRead;

                                    ReadProcessMemory(hProcess, result, buf, new IntPtr(4), out bytesRead);

                                    VirtualFreeEx(hProcess, result, IntPtr.Zero, FreeType.Release);

                                    var killingThreadId = BitConverter.ToUInt32(buf, 0);

                                    Console.WriteLine(killingThreadId);

                                    if(killingThreadId != 0)
                                    {
                                        Console.WriteLine("About to open thread");
                                        var thread = OpenThread(ThreadAccess.ALL, false, killingThreadId);
                                        if(thread != IntPtr.Zero)
                                        {
                                            Console.WriteLine("About to resume thread");
                                            if(ResumeThread(thread) == -1)
                                                Console.WriteLine($"Error while resuming thread, error: {GetLastError()}");
                                            else
                                                Console.WriteLine("Thread successfully resumed");

                                            CloseHandle(thread);
                                        }
                                    }
                                }

                                VirtualFreeEx(hProcess, acmRemote, IntPtr.Zero, FreeType.Release);
                            }
                        }
                        CloseHandle(hProcess);
                    }
                }

                FreeLibrary(hinstDLL);
            }
        }

        [DllExport]
        public static unsafe IntPtr Inject(HookCode nCode, IntPtr wparam, IntPtr lparam)
        {
            if(nCode != HookCode.HC_ACTION)
                return CallNextHookEx(IntPtr.Zero, nCode, wparam, lparam);
            CWPSTRUCT* msg = (CWPSTRUCT*)lparam;
            OutputDebugString($"Got '{msg->message}' message");
            if(msg != null && msg->message == launchMessage)
            {
                OutputDebugString($"Got '{launchMessageName}' message");

                char* acmRemote = (char*)msg->wParam;

                var acmLocal = new string(acmRemote);
                OutputDebugString($"acmLocal = {acmLocal}");
                var acmSplit = acmLocal.Split('$');

                OutputDebugString($"About to load assembly {acmSplit[0]}");
                var assemblyContent = File.ReadAllBytes(acmSplit[0]);
                var assembly = Assembly.Load(assemblyContent);
                if(assembly != null)
                {
                    OutputDebugString($"About to load type {acmSplit[1]}");
                    var type = assembly.GetType(acmSplit[1]);
                    if(type != null)
                    {
                        OutputDebugString($"Just loaded the type {acmSplit[1]}");
                        var methodInfo = type.GetMethod(acmSplit[2], BindingFlags.Static | BindingFlags.Public);
                        if(methodInfo != null)
                        {
                            OutputDebugString($"About to invoke {methodInfo.Name} on type {acmSplit[1]}");
                            var returnValue = methodInfo.Invoke(null, null);
                            if(returnValue == null)
                                returnValue = "NULL";
                            OutputDebugString($"Return value of {methodInfo.Name} on type {acmSplit[1]} is {returnValue}");
                            var injectedModule = GetModuleHandle("ManagedInjector.dll");
                            if(injectedModule != IntPtr.Zero)
                            {
                                OutputDebugString("Got 'ManagedInjector' module");
                                var kernel32Module = GetModuleHandle("kernel32.dll");
                                if(kernel32Module != IntPtr.Zero)
                                {
                                    OutputDebugString("Got 'kernel32' module");
                                    var addr = GetProcAddress(kernel32Module, "FreeLibrary");
                                    if(addr != IntPtr.Zero)
                                    {
                                        OutputDebugString("Got FreeLibrary address");
                                        uint threadId;
                                        var thread = CreateThread(IntPtr.Zero, IntPtr.Zero, addr, injectedModule, CREATE_SUSPENDED, out threadId);
                                        if(thread != 0)
                                        {
                                            OutputDebugString("Thread successfully created");
                                            *(uint*)(msg->lParam) = threadId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wparam, lparam);
        }

        private static readonly uint launchMessage = RegisterWindowMessage(launchMessageName);
    }
}