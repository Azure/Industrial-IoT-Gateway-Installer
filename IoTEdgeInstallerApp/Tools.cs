using System;
using System.IO;

namespace IoTEdgeInstaller
{
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
