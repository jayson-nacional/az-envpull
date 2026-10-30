using System.CommandLine;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.Security.KeyVault.Secrets;

var appOption = new Option<string>("--app")
{
    Description = "The Azure App Service name.",
    Required = true
};

var resourceGroupOption = new Option<string>("--rg-name")
{
    Description = "The Azure resource group name.",
    Required = true
};

var subscriptionOption = new Option<string>("--subscription-id")
{
    Description = "The Azure subscription ID.",
    Required = true
};

var extractSecretsOption = new Option<bool>("--extract-secrets")
{
    Description = "Whether to extract Key Vault secrets. (true or false)",
    DefaultValueFactory = _ => true
};

var rootCommand = new RootCommand(
    "Extract configuration from an Azure App Service.")
{
    appOption,
    resourceGroupOption,
    subscriptionOption,
    extractSecretsOption
};

// Validate required values and report all missing options together.
rootCommand.Validators.Add(result =>
{
    var missing = new List<string>();

    if (string.IsNullOrWhiteSpace(result.GetValue(appOption)))
        missing.Add("--app");

    if (string.IsNullOrWhiteSpace(result.GetValue(resourceGroupOption)))
        missing.Add("--rg-name");

    if (string.IsNullOrWhiteSpace(result.GetValue(subscriptionOption)))
        missing.Add("--subscription-id");

    if (missing.Count > 0)
    {
        result.AddError(
            $"Missing required parameter(s): {string.Join(", ", missing)}.\n" +
            "Please provide all required parameters. Use --help for usage.");
    }
});

// Execute only when parsing and validation succeed.
rootCommand.SetAction(async (parseResult) =>
{
    var app = parseResult.GetValue(appOption)!;
    var resourceGroup = parseResult.GetValue(resourceGroupOption)!;
    var subscriptionId = parseResult.GetValue(subscriptionOption)!;
    var extractSecrets = parseResult.GetValue(extractSecretsOption);

    var credential = new DefaultAzureCredential();
    var tokenRequestContext = new TokenRequestContext(new[]
            {
                "https://management.core.windows.net//.default"
            });

    try
    {
        AccessToken accessToken = await credential.GetTokenAsync(tokenRequestContext);
        Console.WriteLine("Successfully authenticated!");
        Console.WriteLine("Configuration extraction started...");
        Console.WriteLine();
    }
    catch (AuthenticationFailedException)
    {
        Console.WriteLine($"Authentication failed: run 'az login' and try again.");
        return;
    }
    catch (Exception)
    {
        Console.WriteLine($"An error occurred");
        return;
    }

    var armClient = new ArmClient(credential);
    var resourceId = WebSiteResource.CreateResourceIdentifier(
                subscriptionId,
                resourceGroup,
                app
            );

    WebSiteResource webApp = armClient.GetWebSiteResource(resourceId);
    Response<AppServiceConfigurationDictionary> response =
        await webApp.GetApplicationSettingsAsync();

    var settings = response.Value.Properties;
    Console.WriteLine($"Application Settings: {app}");
    Console.WriteLine(new string('-', 60));

    if (settings is null || settings.Count == 0)
    {
        Console.WriteLine("No application settings found.");
        return;
    }

    foreach (var setting in settings.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
    {
        if (extractSecrets && setting.Value.StartsWith("@Microsoft.KeyVault("))
        {
            string secretName = string.Empty;
            Uri? vaultUri = null;

            if (setting.Value.Contains("SecretUri="))
            {
                if (!KeyVaultReferenceParser.TryParseUriFormat(
                            setting.Value,
                            out vaultUri,
                            out secretName))

                {
                    Console.WriteLine($"Variable {setting.Key} cannot be processed");
                }
            }
            else if (setting.Value.Contains("VaultName="))
            {
                if (!KeyVaultReferenceParser.TryParseKeyValueFormat(
                            setting.Value,
                            out string vaultName,
                            out secretName))
                {
                    Console.WriteLine($"Variable {setting.Key} cannot be processed");
                }
                else
                {
                    vaultUri = new Uri($"https://{vaultName}.vault.azure.net");
                }
            }

            var client = new SecretClient(vaultUri, credential);
            KeyVaultSecret secret =
                (await client.GetSecretAsync(secretName)).Value;

            Console.WriteLine($"\"{setting.Key}\": \"{secret.Value}\",");
        }
        else
        {
            Console.WriteLine($"\"{setting.Key}\": \"{setting.Value}\",");
        }
    }

    Console.WriteLine(new string('-', 60));
});

return await rootCommand.Parse(args).InvokeAsync();
