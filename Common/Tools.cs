using System;
using System.Diagnostics;
using System.IO;

namespace IoTEdgeInstaller
{
    public static class ShellHelper
    {
        public static int Bash(this string cmd)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{cmd.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
    }

    public class Tools
    {
        public static bool CreateDriveMappingDirectory()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (Directory.Exists("C:\\IoTEdgeMapping"))
                {
                    return true;
                }
                else
                {
                    return (Directory.CreateDirectory("C:\\IoTEdgeMapping") != null);
                }
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                if (Directory.Exists("/IoTEdgeMapping"))
                {
                    return true;
                }
                else
                {
                    return (Directory.CreateDirectory("/IoTEdgeMapping") != null);
                }
            }

            return false;
        }
    }
}
