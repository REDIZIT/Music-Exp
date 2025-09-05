#if UNITY_STANDALONE_WIN
// created: 2020-07-13
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Security.Permissions;
using System.Text;

namespace Virtofy.IO
{
    /// <summary>
    /// Helper system for process (to allow working with IL2CPP generated code)
    /// </summary>
    public static class ProcessHelper
    {
        #region Variables

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessW(
            string lpApplicationName,
            [In] string lpCommandLine,
            IntPtr procSecAttrs,
            IntPtr threadSecAttrs,
            bool bInheritHandles,
            ProcessCreationFlags dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            ref PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessRights access, bool inherit, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [Flags]
        private enum ProcessAccessRights : uint
        {
            PROCESS_CREATE_PROCESS = 0x0080, //  Required to create a process.
            PROCESS_CREATE_THREAD = 0x0002, //  Required to create a thread.
            PROCESS_DUP_HANDLE = 0x0040, // Required to duplicate a handle using DuplicateHandle.
            PROCESS_QUERY_INFORMATION = 0x0400, //  Required to retrieve certain information about a process, such as its token, exit code, and priority class (see OpenProcessToken, GetExitCodeProcess, GetPriorityClass, and IsProcessInJob).
            PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, //  Required to retrieve certain information about a process (see QueryFullProcessImageName). A handle that has the PROCESS_QUERY_INFORMATION access right is automatically granted PROCESS_QUERY_LIMITED_INFORMATION. Windows Server 2003 and Windows XP/2000:  This access right is not supported.
            PROCESS_SET_INFORMATION = 0x0200, //    Required to set certain information about a process, such as its priority class (see SetPriorityClass).
            PROCESS_SET_QUOTA = 0x0100, //  Required to set memory limits using SetProcessWorkingSetSize.
            PROCESS_SUSPEND_RESUME = 0x0800, // Required to suspend or resume a process.
            PROCESS_TERMINATE = 0x0001, //  Required to terminate a process using TerminateProcess.
            PROCESS_VM_OPERATION = 0x0008, //   Required to perform an operation on the address space of a process (see VirtualProtectEx and WriteProcessMemory).
            PROCESS_VM_READ = 0x0010, //    Required to read memory in a process using ReadProcessMemory.
            PROCESS_VM_WRITE = 0x0020, //   Required to write to memory in a process using WriteProcessMemory.
            DELETE = 0x00010000, // Required to delete the object.
            READ_CONTROL = 0x00020000, //   Required to read information in the security descriptor for the object, not including the information in the SACL. To read or write the SACL, you must request the ACCESS_SYSTEM_SECURITY access right. For more information, see SACL Access Right.
            SYNCHRONIZE = 0x00100000, //    The right to use the object for synchronization. This enables a thread to wait until the object is in the signaled state.
            WRITE_DAC = 0x00040000, //  Required to modify the DACL in the security descriptor for the object.
            WRITE_OWNER = 0x00080000, //    Required to change the owner in the security descriptor for the object.
            STANDARD_RIGHTS_REQUIRED = 0x000f0000,
            PROCESS_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0xFFF //    All possible access rights for a process object.
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            internal IntPtr hProcess;
            internal IntPtr hThread;
            internal uint dwProcessId;
            internal uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            internal uint cb;
            internal IntPtr lpReserved;
            internal IntPtr lpDesktop;
            internal IntPtr lpTitle;
            internal uint dwX;
            internal uint dwY;
            internal uint dwXSize;
            internal uint dwYSize;
            internal uint dwXCountChars;
            internal uint dwYCountChars;
            internal uint dwFillAttribute;
            internal uint dwFlags;
            internal ushort wShowWindow;
            internal ushort cbReserved2;
            internal IntPtr lpReserved2;
            internal IntPtr hStdInput;
            internal IntPtr hStdOutput;
            internal IntPtr hStdError;
        }

        [Flags]
        private enum ProcessCreationFlags : uint
        {
            NONE = 0,
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SECURE_PROCESS = 0x00400000,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }

        private const UInt32 INFINITE = 0xFFFFFFFF;
        private const UInt32 WAIT_ABANDONED = 0x00000080;
        private const UInt32 WAIT_OBJECT_0 = 0x00000000;
        private const UInt32 WAIT_TIMEOUT = 0x00000102;

        private const int ERROR_NO_MORE_FILES = 0x12;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeSnapshotHandle CreateToolhelp32Snapshot(SnapshotFlags flags, uint id);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

        [Flags]
        private enum SnapshotFlags : uint
        {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            All = (HeapList | Process | Thread | Module),
            Inherit = 0x80000000,
            NoHeaps = 0x40000000
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        };

        // [SuppressUnmanagedCodeSecurity, HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeSnapshotHandle : SafeHandleMinusOneIsInvalid
        {
            internal SafeSnapshotHandle() : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            internal SafeSnapshotHandle(IntPtr handle) : base(true)
            {
                base.SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success), DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
            private static extern bool CloseHandle(IntPtr handle);
        }

        #endregion

        #region Core
        /// <summary>
        /// Starts the given file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="arguments"></param>
        /// <param name="dir"></param>
        /// <param name="hidden"></param>
        /// <param name="processID"></param>
        /// <returns></returns>
        public static bool Start(string path,
            string arguments,
            string dir,
            bool hidden,
            out uint processID)
        {
            processID = 0;
            ProcessCreationFlags flags = hidden ? ProcessCreationFlags.CREATE_NO_WINDOW : ProcessCreationFlags.NONE;
            STARTUPINFO startupinfo = new STARTUPINFO {
                cb = (uint)Marshal.SizeOf<STARTUPINFO>()
            };
            PROCESS_INFORMATION processinfo = new PROCESS_INFORMATION();
            if (!CreateProcessW(null,
                path + " " + arguments,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                flags,
                IntPtr.Zero,
                dir,// + " " + arguments,
                ref startupinfo,
                ref processinfo))
            {
                return (false);
            }
            processID = processinfo.dwProcessId;
            //return processinfo.dwProcessId;
            return (true);
        }

        private static IntPtr GetProcessHandle(uint processID)
        {
            return (OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, processID));
        }

        /// <summary>
        /// Checks if the given process has ended
        /// </summary>
        /// <param name="processID"></param>
        /// <returns></returns>
        public static bool IsProcessEnded(uint processID)
        {
            IntPtr handle = GetProcessHandle(processID);
            if (GetExitCodeProcess(handle, out uint lpExitCode)) {
                CloseHandle(handle); // Не забываем закрывать дескриптор
                return (lpExitCode != 259); // 259 (STILL_ACTIVE)
            } else {
                if (handle != IntPtr.Zero) CloseHandle(handle);
                return (true);
            }
        }

        /// <summary>
        /// Kills the given process
        /// </summary>
        /// <param name="processID"></param>
        /// <returns></returns>
        public static bool KillProcess(uint processID)
        {
            IntPtr handle = GetProcessHandle(processID);
            if (handle == IntPtr.Zero) {
                return(false);
            }
            if (!TerminateProcess(handle, 0)) {
                CloseHandle(handle); // Закрываем дескриптор даже при ошибке
                return (false);
            }
            if (!CloseHandle(handle)) {
                return (false);
            }
            return (true);
        }

        /// <summary>
        /// Waits till the given process has exited.
        /// This will stall the main thread, e.g. will freezes the app!
        /// </summary>
        /// <param name="processID"></param>
        public static void WaitForExit(uint processID)
        {
            IntPtr handle = GetProcessHandle(processID);
            if (handle == IntPtr.Zero) {
                return;
            }
            try
            {
                WaitForSingleObject(handle, INFINITE);
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        /// <summary>
        /// Get my current process ID
        /// </summary>
        /// <returns></returns>
        public static int GetCurrentProcessID()
        {
            uint id = GetCurrentProcessId();
            return(Convert.ToInt32(id));
        }

        /// <summary>
        /// Get the parent process ID
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public static int GetParentProcessID(int Id)
        {
            PROCESSENTRY32 pe32 = new PROCESSENTRY32 { };
            pe32.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            using (SafeSnapshotHandle hSnapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, 0)) // 0 для всех процессов
            {
                if (hSnapshot.IsInvalid)
                {
                    return (-1);
                }

                if (!Process32First(hSnapshot, ref pe32))
                {
                    return (-1);
                }
                do
                {
                    if (pe32.th32ProcessID == (uint)Id)
                        return (int)pe32.th32ParentProcessID;
                } while (Process32Next(hSnapshot, ref pe32));
            }

            return (-1);
        }

        #endregion


        // --- НОВЫЙ КОД ДЛЯ ЗАПУСКА ОТ ИМЕНИ АДМИНИСТРАТОРА ---

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetProcessId(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const uint SEE_MASK_NOCLOSEPROCESS = 0x00000040;
        private const int ERROR_CANCELLED = 1223; // Пользователь отказался от повышения прав (UAC)

        /// <summary>
        /// Запускает процесс с правами администратора и перехватывает его вывод.
        /// </summary>
        /// <param name="path">Путь к исполняемому файлу.</param>
        /// <param name="arguments">Аргументы командной строки.</param>
        /// <param name="dir">Рабочая директория.</param>
        /// <param name="hidden">Запустить ли процесс в скрытом окне.</param>
        /// <param name="processID">ID созданного процесса (оболочки cmd.exe).</param>
        /// <param name="output">Вывод процесса (stdout + stderr).</param>
        /// <returns>True в случае успеха, иначе false.</returns>
        public static bool StartWithOutput(string path,
            string arguments,
            string dir,
            bool hidden,
            out uint processID,
            out string output)
        {
            processID = 0;
            output = null;

            // Создаем временный файл для хранения вывода
            string tempOutputFile = Path.GetTempFileName();

            try
            {
                // Формируем команду для cmd.exe. Она запустит нужный процесс и перенаправит
                // его стандартный вывод (1) и вывод ошибок (2) в наш временный файл.
                // `>` перенаправляет stdout, `2>&1` перенаправляет stderr туда же, куда и stdout.
                string command = $"\"{path}\" {arguments} > \"{tempOutputFile}\" 2>&1";

                var shellInfo = new SHELLEXECUTEINFO();
                shellInfo.cbSize = Marshal.SizeOf(shellInfo);
                shellInfo.fMask = SEE_MASK_NOCLOSEPROCESS; // Дает нам hProcess для ожидания
                shellInfo.hwnd = IntPtr.Zero;
                shellInfo.lpVerb = "runas"; // <-- КЛЮЧЕВОЙ МОМЕНТ: запуск с повышением прав
                shellInfo.lpFile = "cmd.exe"; // Мы запускаем cmd, который выполнит нашу команду
                shellInfo.lpParameters = "/C " + command; // /C - выполнить команду и завершиться
                shellInfo.lpDirectory = dir;
                shellInfo.nShow = hidden ? SW_HIDE : SW_SHOWNORMAL;

                if (!ShellExecuteEx(ref shellInfo))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == ERROR_CANCELLED)
                    {
                        // Пользователь нажал "Нет" в диалоге UAC
                        output = "UAC elevation was cancelled by the user.";
                    }
                    else
                    {
                        output = $"ShellExecuteEx failed with error code: {errorCode}";
                    }
                    return false;
                }

                if (shellInfo.hProcess == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    processID = GetProcessId(shellInfo.hProcess);

                    // Ждем завершения процесса
                    WaitForSingleObject(shellInfo.hProcess, INFINITE);

                    // Читаем вывод из временного файла
                    // Даем небольшую задержку, чтобы ОС успела сбросить буферы в файл
                    System.Threading.Thread.Sleep(100);
                    output = File.ReadAllText(tempOutputFile, Encoding.UTF8);

                    return true;
                }
                finally
                {
                    // Обязательно закрываем дескриптор процесса
                    CloseHandle(shellInfo.hProcess);
                }
            }
            catch (Exception ex)
            {
                output = $"An exception occurred: {ex.Message}";
                return false;
            }
            finally
            {
                // Обязательно удаляем временный файл
                if (File.Exists(tempOutputFile))
                {
                    File.Delete(tempOutputFile);
                }
            }
        }

        // Старую версию StartWithOutput можно удалить или закомментировать,
        // так как новая ее полностью заменяет и улучшает.
        /*
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInheritHandle;
        }
        */

        #region Mutex

        /*
        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);
        [DllImport("kernel32.dll")]
        private static extern bool ReleaseMutex(IntPtr hMutex);

        public static IntPtr CreateMutex(bool initialOwner,
            string name)
        {
            // create IntPtrs for use with CreateMutex()
            IntPtr ipMutexAttr = new IntPtr(0);
            IntPtr ipHMutex = new IntPtr(0);
            ipHMutex = CreateMutex(ipMutexAttr, initialOwner, name);
            if (ipHMutex != IntPtr.Zero) {
                int iGLE = Marshal.GetLastWin32Error();
                if (iGLE == 183) {// Win32Calls.ERROR_ALREADY_EXISTS)
                    //allready exists
                }
            }
            return (ipHMutex);
        }
        */
        /*
        public static bool ReleaseMutex(IntPtr intPtr)
        {
            if (intPtr != IntPtr.Zero) {
                return (ReleaseMutex(intPtr));
            } else {
                return (false);
            }
        }
        */

        #endregion

       #region Admin Check P/Invoke

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        private enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation, // 20
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            MaxTokenInfoClass
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_ELEVATION
        {
            public uint TokenIsElevated;
        }

        private const uint TOKEN_QUERY = 0x0008;

        #endregion

        /// <summary>
        /// Проверяет, запущен ли текущий процесс с правами администратора (elevated).
        /// </summary>
        /// <returns>True, если процесс имеет права администратора, иначе false.</returns>
        public static bool IsRunningAsAdmin()
        {
            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                // Получаем токен доступа текущего процесса
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out tokenHandle))
                {
                    return false;
                }

                // Получаем информацию о правах (elevation)
                TOKEN_ELEVATION elevation = new TOKEN_ELEVATION();
                int elevationSize = Marshal.SizeOf(elevation);
                IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);

                try
                {
                    uint returnLength;
                    if (GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevation, elevationPtr, (uint)elevationSize, out returnLength))
                    {
                        elevation = (TOKEN_ELEVATION)Marshal.PtrToStructure(elevationPtr, typeof(TOKEN_ELEVATION));
                        // Если TokenIsElevated не равен 0, значит процесс запущен с повышенными правами.
                        return elevation.TokenIsElevated != 0;
                    }
                    else
                    {
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(elevationPtr);
                }
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
        }
    }
}
#endif