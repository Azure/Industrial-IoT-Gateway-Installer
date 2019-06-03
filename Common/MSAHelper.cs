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

    public class MSAHelper
    {
        public static SigninStates CurrentState { get; private set; } = SigninStates.SignedOut;

        public static List<string> Subscriptions = new List<string>();

        public delegate void ShowProgress(double percentProgress);
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
                        Process.Start(new ProcessStartInfo("sudo apt-get update")).WaitForExit();
                        Process.Start(new ProcessStartInfo("sudo apt --assume-yes install curl")).WaitForExit();
                        Process.Start(new ProcessStartInfo("curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"));
                    }
                    else
                    {
                        errorCallback?.Invoke(Strings.OSNotSupported);
                    }

                    return false;
                }

                progressCallback?.Invoke(5);
                
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

                progressCallback?.Invoke(10);
                
                // install iot extension, if required
                PSCallback?.Invoke("az extension add --name azure-cli-iot-ext");

                progressCallback?.Invoke(15);
                
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
