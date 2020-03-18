using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Threading.Tasks;

namespace IoTEdgeInstaller
{
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
