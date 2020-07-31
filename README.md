
# Azure Industrial IoT Gateway Installer
This installer installs Azure IoT Edge on a local computer for both Windows 10 (supporting both a desktop app or a console app)
as well as Linux (currently Ubuntu is supported only) in a simple, step-by-step manner. It installs all prerequisits and default modules (Edge Hub and Edge Agent). When used with the Azure Industrial IoT cloud platform or with IoT Central, it also automatically installs the Azure Industrial IoT Edge Modules Discovery, OPC Twin and OPC Publisher (see https://github.com/Azure/Industrial-IoT/tree/master/docs/modules) needed for interoperating with OPC UA adapters and OPC UA-enabled PLCs integrated into industrial machinery and systems. This is done via IoT Edge's automatic deployment feature, see here: https://docs.microsoft.com/en-us/azure/iot-edge/how-to-deploy-at-scale.

Releases including a 1-click installer for Windows can be found in the Releases folder.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
