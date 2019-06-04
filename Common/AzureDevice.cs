using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace IoTEdgeInstaller
{
    public class AzureIoT
    {
        public delegate void ShowProgress(double percentProgress, bool isAbsolute);
        public delegate void ShowError(string error);
        public delegate Collection<string> RunPSCommand(string command);

        private static object hubListLock = new object();

        public static List<AzureIoTHub> GetIotHubList(
            ShowProgress progressCallback,
            ShowError errorCallback,
            RunPSCommand PSCallback)
        {
            List<AzureIoTHub> hubList = new List<AzureIoTHub>();

            try
            {
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
                                                connectionString = connectionString.Substring(connectionString.IndexOf(":"));
                                                connectionString = connectionString.Substring(connectionString.IndexOf("\"") + 1);
                                                connectionString = connectionString.Substring(0, connectionString.IndexOf("\""));

                                                lock (hubListLock)
                                                {
                                                    hubList.Add(new AzureIoTHub(hubName + " (" + subscriptionName + ")", connectionString, subscriptionName));
                                                }
                                            }
                                        }
                                    }
                                }));
                            }
                        }
                    }
                 
                    Task.WhenAll(tasks).Wait();
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
        public delegate void ShowProgress(double percentProgress, bool isAbsolute);
        public delegate void ShowError(string error);
        public delegate Collection<string> RunPSCommand(string command);

        public AzureIoTHub(string name, string connectionString, string subscriptionName)
        {
            Name = name;
            SubscriptionName = subscriptionName;
        }

        public string Name { get; set; }

        public string SubscriptionName { get; set; }

        public AzureDeviceEntity GetDevice(RunPSCommand PSCallback, string deviceId)
        {
            //az iot hub device-identity show --device-id myEdgeDevice --hub-name {hub_name}
            Collection<string> results = PSCallback?.Invoke($"az iot hub device-identity show --device-id {deviceId} --hub-name {Name}");
            if (results != null && results.Count != 0)
            {
                var deviceEntity = new AzureDeviceEntity();
                
                //TODO
                //deviceEntity.Id = device.Id
                
                //if (device.Capabilities != null)
                //{
                //    deviceEntity.IotEdge = device.Capabilities.IotEdge;
                //}

                ////az iot hub device-identity show-connection-string --device-id myEdgeDevice --hub-name {hub_name}
                //if (device.Authentication != null &&
                //    device.Authentication.SymmetricKey != null)
                //{
                //    deviceEntity.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                //}

                //if (deviceEntity.IotEdge)
                //{
                //    var moduleList = new List<AzureModuleEntity>();
                //    //az iot hub module-identity list --device-id myEdgeDevice --hub-name {hub_name}
                //    if (modules != null)
                //    {
                //        foreach (var m in modules)
                //        {
                //            var entity = new AzureModuleEntity()
                //            {
                //                Id = m.Id,
                //                DeviceId = m.DeviceId,
                //            };
                //            moduleList.Add(entity);
                //        }
                //    }
                //    deviceEntity.Modules = moduleList;
                //}

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
            return (results != null && results.Count != 0);
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
