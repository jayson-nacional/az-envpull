using System.CommandLine;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;

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

    Console.WriteLine("Configuration extraction started.");
    Console.WriteLine($"App: {app}");
    Console.WriteLine($"Resource Group: {resourceGroup}");
    Console.WriteLine($"Subscription ID: {subscriptionId}");
    Console.WriteLine($"Extract Secrets: {extractSecrets}");

    var credential = new DefaultAzureCredential();
    var tokenRequestContext = new TokenRequestContext(new[]
            {
                "https://management.core.windows.net//.default"
            });

    try
    {
        AccessToken accessToken = await credential.GetTokenAsync(tokenRequestContext);
        Console.WriteLine("Successfully authenticated!");
    }
    catch (AuthenticationFailedException)
    {
        Console.WriteLine($"Authentication failed: run 'az login' and try again.");
    }
    catch (Exception)
    {
        Console.WriteLine($"An error occurred");
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
        Console.WriteLine($"{setting.Key}: {setting.Value}");
    }
    Console.WriteLine(new string('-', 60));
});

return await rootCommand.Parse(args).InvokeAsync();

