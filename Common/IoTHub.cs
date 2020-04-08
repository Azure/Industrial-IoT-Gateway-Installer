using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public class EdgeAgent
    {
        public string Type { get; set; }

        public int ExitCode { get; set; }

        public string StatusDescription { get; set; }

        public DateTime LastStartTimeUtc { get; set; }

        public DateTime LastExitTimeUtc { get; set; }

        public string RuntimeStatus { get; set; }

        public string ImagePullPolicy { get; set; }

        public JObject Settings { get; set; }
    }

    public class EdgeHub
    {
        public string Type { get; set; }

        public string Status { get; set; }

        public string RestartPolicy { get; set; }

        public int ExitCode { get; set; }

        public string StatusDescription { get; set; }

        public DateTime LastStartTimeUtc { get; set; }

        public DateTime LastExitTimeUtc { get; set; }

        public int RestartCount { get; set; }

        public DateTime LastRestartTimeUtc { get; set; }

        public string RuntimeStatus { get; set; }

        public JObject Settings { get; set; }
    }

    public class SystemModules
    {
        public EdgeAgent EdgeAgent { get; set; }

        public EdgeHub EdgeHub { get; set; }
    }

    public class Module
    {
        public int ExitCode { get; set; }

        public string StatusDescription { get; set; }

        public DateTime LastStartTimeUtc { get; set; }

        public DateTime LastExitTimeUtc { get; set; }

        public int RestartCount { get; set; }

        public DateTime LastRestartTimeUtc { get; set; }

        public string RuntimeStatus { get; set; }

        public string Version { get; set; }

        public string Status { get; set; }

        public string RestartPolicy { get; set; }

        public string ImagePullPolicy { get; set; }

        public string Type { get; set; }

        public JObject Settings { get; set; }

        public JObject Env { get; set; }
    }

    public class Modules
    {
        public Module Twin { get; set; }

        public Module Publisher { get; set; }

        public Module Discovery { get; set; }
    }

    public class RootObject
    {
        public string SchemaVersion { get; set; }

        public Version Version { get; set; }

        public int LastDesiredVersion { get; set; }

        public JObject LastDesiredStatus { get; set; }

        public JObject Runtime { get; set; }

        public SystemModules SystemModules { get; set; }

        public Modules Modules { get; set; }
}

    public class AzureIoTHub
    {
        public AzureIoTHub(string connectionString)
        {
            registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            Name = connectionString.Substring(connectionString.IndexOf('=') + 1);
            Name = Name.Substring(0, Name.IndexOf("."));
        }

        private RegistryManager registryManager;

        public string Name { get; set; }

        public async Task<Device> GetDeviceAsync(string deviceId)
        {
            return await registryManager.GetDeviceAsync(deviceId);
        }

        public async Task<List<KeyValuePair<string, string>>> GetDeviceModulesStatusAsync(string deviceId)
        {
            List<KeyValuePair<string, string>> modulesStatus = new List<KeyValuePair<string, string>>();

            Device device = await registryManager.GetDeviceAsync(deviceId);
            if (device != null)
            {
                try
                {
                    foreach (Microsoft.Azure.Devices.Module module in await registryManager.GetModulesOnDeviceAsync(deviceId))
                    {
                        // the Edge Agent's reported properties have the runtime status of all modules in it
                        Twin twin = await registryManager.GetTwinAsync(deviceId, module.Id);
                        if ((twin != null) && (twin.ModuleId == "$edgeAgent"))
                        {
                            var reportedProperties = JsonConvert.DeserializeObject<RootObject>(twin.Properties.Reported.ToJson());

                            modulesStatus.Add(new KeyValuePair<string, string>("Edge Agent", reportedProperties.SystemModules.EdgeAgent.RuntimeStatus));
                            modulesStatus.Add(new KeyValuePair<string, string>("Edge Hub", reportedProperties.SystemModules.EdgeHub.RuntimeStatus));
                            modulesStatus.Add(new KeyValuePair<string, string>("OPC Twin", reportedProperties.Modules.Twin.RuntimeStatus));
                            modulesStatus.Add(new KeyValuePair<string, string>("OPC Publisher", reportedProperties.Modules.Publisher.RuntimeStatus));
                            modulesStatus.Add(new KeyValuePair<string, string>("Discovery", reportedProperties.Modules.Discovery.RuntimeStatus));
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // do nothing
                }
            }

            return modulesStatus;
        }

        public async Task<bool> CreateIoTEdgeDeviceAsync(string deviceId)
        {
            // create new device
            Device newDevice = await registryManager.AddDeviceAsync(new Device(deviceId));
            if (newDevice != null)
            {
                // make it an IoT Edge device
                newDevice.Capabilities.IotEdge = true;
                newDevice = await registryManager.UpdateDeviceAsync(newDevice, true);
                if (newDevice == null)
                {
                    // update failed
                    return false;
                }
                
                // enable layered deployment for Industrial IoT Edge devices
                Twin twin = await registryManager.GetTwinAsync(deviceId);
                if (twin != null)
                {
                    string patch = string.Empty;
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        patch = "\"tags\": { \"__type__\": \"iiotedge\", \"os\": \"Windows\" },";
                    }
                    if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        patch = "\"tags\": { \"__type__\": \"iiotedge\", \"os\": \"Linux\" },";
                    }
                    
                    string twinJSON = twin.ToJson();
                    twinJSON = twinJSON.Insert(twinJSON.IndexOf("\"version\""), patch);
                    return (await registryManager.UpdateTwinAsync(twin.DeviceId, twinJSON, twin.ETag) != null);
                }

                return (twin != null);
            }

            return (newDevice != null);
        }

        public async Task DeleteDeviceAsync(string deviceId)
        {
            await registryManager.RemoveDeviceAsync(deviceId);
        }
    }
}
