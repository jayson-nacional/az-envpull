# Overview  
This is a simple dotnet cli utility that enables to pull environment variables from Azure App Servicesincluding the values stored on Key Vaults.  

## Prerequisites  
This relies on DefaultAzureCredential. Preferrably when running on IDE ensure that it is currently logged in using an account that has access to the Azure Resources. If plainly using terminal, ensure that Azure CLI is installed and run ```az login``` before running ```az-envpull```.  

[Reference: Installing Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)  

## Installation  
RUN ```dotnet tool install --global Nash.AzEnvPull --version [preferred-version]```  

## Usage
RUN ```az-envpull --app [app service name] --rg-name [resource group name] --subscription-id [Azure subscription id]```   

By default, all key vault references are extracted with the actual value, if you do not wish to do so, add ```--extract-secrets``` as an additional parameter with ```false``` as the argument.
