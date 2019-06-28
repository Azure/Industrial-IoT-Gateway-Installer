using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace IoTEdgeInstaller
{
    public class AzureIoT
    {
        public delegate void ShowProgress(double percentProgress, bool isAbsolute);
        public delegate void ShowError(string error);
        public delegate Collection<string> RunPSCommand(string command);

        private static object hubListLock = new object();

        public static string DeploymentManifestNameWindows = "IoTEdgeInstaller.iiotedgedeploymentmanifestwindows.json";
        public static string DeploymentManifestNameLinux = "IoTEdgeInstaller.iiotedgedeploymentmanifestlinux.json";

        public static bool LoadDeploymentManifest()
        {
            string manifestName = string.Empty;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                manifestName = DeploymentManifestNameWindows;
            }
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                manifestName = DeploymentManifestNameLinux;
            }

            Stream istrm = typeof(AzureIoT).Assembly.GetManifestResourceStream(manifestName);
            if (istrm != null)
            {
                // store in app directory
                FileStream ostrm = File.Create(Directory.GetCurrentDirectory() + "/" + manifestName);
                istrm.CopyTo(ostrm);

                ostrm.Flush();
                ostrm.Dispose();
                istrm.Dispose();
                return true;
            }

            return false;
        }

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

        public static List<AzureIoTHub> GetIotHubList(
            ShowProgress progressCallback,
            ShowError errorCallback,
            RunPSCommand PSCallback)
        {
            List<AzureIoTHub> hubList = new List<AzureIoTHub>();
            
            try
            {
                if (MSAHelper.Subscriptions.Count == 0)
                {
                    // no subscritions means no IoT Hubs
                    return hubList;
                }

                double progressPerSubscription = 85.0f / MSAHelper.Subscriptions.Count;
                for (int k = 0; k < MSAHelper.Subscriptions.Count; k++)
                {
                    List<Task> tasks = new List<Task>();
                    string subscriptionName = MSAHelper.Subscriptions[k];

                    PSCallback?.Invoke("az account set --subscription '" + subscriptionName + "'");

                    Collection<string> hubListResults = PSCallback?.Invoke("az iot hub list");
                    if (hubListResults != null && hubListResults.Count != 0)
                    {
                        for (int i = 0; i < hubListResults.Count; i++)
                        {
                            string hubName = hubListResults[i];
                            if (hubName.Contains("\"name\""))
                            {
                                hubName = hubName.Substring(hubName.IndexOf(":"));
                                hubName = hubName.Substring(hubName.IndexOf("\"") + 1);
                                hubName = hubName.Substring(0, hubName.IndexOf("\""));

                                // filter
                                if (hubName == "$fallback" || hubName == "S1" || hubName == "F1" || hubName == "B1")
                                {
                                    continue;
                                }

                                tasks.Add(Task.Run(() =>
                                {
                                    Collection<string> results2 = PSCallback("az iot hub show-connection-string --name '" + hubName + "'");
                                    if (results2 != null && results2.Count != 0)
                                    {
                                        for (int j = 0; j < results2.Count; j++)
                                        {
                                            string connectionString = results2[j];
                                            if (connectionString.Contains("\"connectionString\""))
                                            {
                                                // we have access
                                                lock (hubListLock)
                                                {
                                                    hubList.Add(new AzureIoTHub(hubName, subscriptionName));
                                                }
                                            }
                                        }
                                    }
                                }));
                            }
                        }
                    }
                 
                    Task.WhenAll(tasks).Wait();
                    tasks.Clear();
                    progressCallback?.Invoke(progressPerSubscription, false);
                }
            }
            catch (Exception ex)
            {
                errorCallback?.Invoke(ex.Message);
            }

            return hubList;
        }
    }

    public class AzureIoTHub
    {
        public delegate Collection<string> RunPSCommand(string command);

        public AzureIoTHub(string name, string subscriptionName)
        {
            Name = name;
            SubscriptionName = subscriptionName;
            DisplayName = Name + " (" + SubscriptionName + ")";
        }

        public string Name { get; set; }

        public string SubscriptionName { get; set; }

        public string DisplayName { get; set; }

        public AzureDeviceEntity GetDevice(RunPSCommand PSCallback, string deviceId)
        {
            Collection<string> results = PSCallback?.Invoke($"az iot hub device-identity show --device-id {deviceId} --hub-name {Name}");
            if (results != null && results.Count != 0)
            {
                var deviceEntity = new AzureDeviceEntity();
                deviceEntity.Id = deviceId;

                for (int i = 0; i < results.Count; i++)
                {
                    string result = results[i];
                    if (result.Contains("\"iotEdge\": true"))
                    {
                        deviceEntity.IotEdge = true;
                        continue;
                    }

                    if (result.Contains("primaryKey"))
                    {
                        result = result.Substring(result.IndexOf(":"));
                        result = result.Substring(result.IndexOf("\"") + 1);
                        result = result.Substring(0, result.IndexOf("\""));
                        deviceEntity.PrimaryKey = result;
                    }
                }

                if (deviceEntity.IotEdge)
                {
                    deviceEntity.Modules = new List<AzureModuleEntity>();

                    Collection<string> results2 = PSCallback?.Invoke($"az iot hub module-identity list --device-id {deviceId} --hub-name {Name}");
                    if (results2 != null && results2.Count != 0)
                    {
                        for (int i = 0; i < results2.Count; i++)
                        {
                            string result = results2[i];
                            if (result.Contains("moduleId"))
                            {
                                var entity = new AzureModuleEntity();
                                result = result.Substring(result.IndexOf(":"));
                                result = result.Substring(result.IndexOf("\"") + 1);
                                result = result.Substring(0, result.IndexOf("\""));
                                entity.Id = result;
                                entity.DeviceId = deviceEntity.Id;
                                deviceEntity.Modules.Add(entity);
                            }
                        }
                    }
                }

                return deviceEntity;
            }

            return null;
        }

        public bool CreateIoTEdgeDevice(RunPSCommand PSCallback, string deviceId)
        {
            Collection<string> results = PSCallback?.Invoke($"az iot hub device-identity create --device-id {deviceId} --hub-name {Name} --edge-enabled");
            return (results != null && results.Count != 0);
        }

        public bool DeleteDevice(RunPSCommand PSCallback, string deviceId)
        {
            Collection<string> results = PSCallback?.Invoke($"az iot hub device-identity delete --device-id {deviceId} --hub-name {Name}");
            return (results != null);
        }
    }

    public class AzureModuleEntity : IComparable<AzureModuleEntity>
    {
        public AzureModuleEntity(){}

        public string Id { get; set; }

        public string DeviceId { get; set; }

        public int CompareTo(AzureModuleEntity other)
        {
            return string.Compare(this.DeviceId + this.Id, other.DeviceId + other.Id, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class NicsEntity : IComparable<NicsEntity>
    {
        public NicsEntity(){}

        public string Name { get; set; }

        public string Description { get; set; }
       
        public int CompareTo(NicsEntity other)
        {
            return string.Compare(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class AzureDeviceEntity : IComparable<AzureDeviceEntity>
    {
        public AzureDeviceEntity()
        {
            IotEdge = false;
        }

        public string Id { get; set; }

        public string PrimaryKey { get; set; }

        public bool IotEdge { get; set; }

        public IList<AzureModuleEntity> Modules { get; set; }

        public string ComboId => Id + (IotEdge ? " (IoT Edge)" : "");

        public int CompareTo(AzureDeviceEntity other)
        {
            return string.Compare(this.Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
