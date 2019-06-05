using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace IoTEdgeInstaller
{
    public enum SigninStates
    {
        SignedIn,
        SignedOut
    }

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

    public class MSAHelper
    {
        public static SigninStates CurrentState { get; private set; } = SigninStates.SignedOut;

        public static List<string> Subscriptions = new List<string>();

        public delegate void ShowProgress(double percentProgress, bool isAbsolute);
        public delegate void ShowError(string error);
        public delegate Collection<string> RunPSCommand(string command);

        public static bool SignIn(
            ShowProgress progressCallback,
            ShowError errorCallback,
            RunPSCommand PSCallback)
        {
            if (CurrentState == SigninStates.SignedIn)
            {
                return true;
            }

            try
            {
                Collection<string> results = PSCallback?.Invoke("az");
                if (results == null || results.Count == 0)
                {
                    errorCallback?.Invoke(Strings.AzureCLI);
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        Process.Start(new ProcessStartInfo("https://aka.ms/installazurecliwindows"));
                    }
                    else if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        "sudo apt-get update".Bash();
                        "sudo apt --assume-yes install curl".Bash();
                        "curl -sL -N https://aka.ms/InstallAzureCLIDeb | sudo bash".Bash();
                    }
                    else
                    {
                        errorCallback?.Invoke(Strings.OSNotSupported);
                    }

                    return false;
                }

                progressCallback?.Invoke(5, true);
                
                results = PSCallback?.Invoke("az login");
                if (results == null || results.Count == 0)
                {
                    errorCallback?.Invoke(Strings.LoginFailedAlertMessage);
                    return false;
                }

                // enumerate subscriptions
                Subscriptions.Clear();
                for (int i = 0; i < results.Count; i++)
                {
                    string json = results[i].ToString();
                    if (json.Contains("\"name\""))
                    {
                        json = json.Substring(json.IndexOf(":"));
                        json = json.Substring(json.IndexOf("\"") + 1);
                        json = json.Substring(0, json.IndexOf("\""));
                        if (!json.Contains("@"))
                        { 
                            Subscriptions.Add(json);
                        }
                    }
                }

                progressCallback?.Invoke(10, true);
                
                // install iot extension, if required
                PSCallback?.Invoke("az extension add --name azure-cli-iot-ext");

                progressCallback?.Invoke(15, true);
                
                CurrentState = SigninStates.SignedIn;

                return true;
            }
            catch (Exception ex)
            {
                errorCallback?.Invoke(Strings.LoginFailedAlertMessage + ": " + ex.Message);
                return false;
            }
        }
    }
}
