using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var processStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = @"C:\workspace\CodeModificationTests\minesweeper",
                FileName = @"C:\workspace\CodeModificationTests\minesweeper\minesweeper.exe",
                UseShellExecute = false
            };
            processStartInfo.EnvironmentVariables["COR_PROFILER"] = "{6489b8a0-59bb-402a-953b-72d770c7aa01}";
            processStartInfo.EnvironmentVariables["COR_ENABLE_PROFILING"] = "1";
            processStartInfo.EnvironmentVariables["COR_PROFILER_PATH"] = @"C:\workspace\CodeModificationTests\Assemblies\ClrProfiler.dll";
            Process.Start(processStartInfo).WaitForExit();
        }
    }
}
