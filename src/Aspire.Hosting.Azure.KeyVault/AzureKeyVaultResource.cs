// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Azure.Identity;
using Azure.Provisioning.KeyVault;
using Azure.Provisioning.Primitives;
using Azure.Security.KeyVault.Secrets;

namespace Aspire.Hosting.Azure;

/// <summary>
/// A resource that represents an Azure Key Vault.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="configureInfrastructure">Callback to configure the Azure resources.</param>
public class AzureKeyVaultResource(string name, Action<AzureResourceInfrastructure> configureInfrastructure)
    : AzureProvisioningResource(name, configureInfrastructure), IResourceWithConnectionString
{
    /// <summary>
    /// Gets the "vaultUri" output reference for the Azure Key Vault resource.
    /// </summary>
    public BicepOutputReference VaultUri => new("vaultUri", this);

    /// <summary>
    /// Gets the "name" output reference for the Azure Key Vault resource.
    /// </summary>
    public BicepOutputReference NameOutputReference => new("name", this);

    /// <summary>
    /// Gets the connection string template for the manifest for the Azure Key Vault resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{VaultUri}");

    /// <inheritdoc/>
    public override ProvisionableResource AddAsExistingResource(AzureResourceInfrastructure infra)
    {
        var store = KeyVaultService.FromExisting(this.GetBicepIdentifier());
        store.Name = NameOutputReference.AsProvisioningParameter(infra);
        infra.Add(store);
        return store;
    }
}

/// <summary>
/// Represents a reference to a secret in an Azure Key Vault resource.
/// </summary>
/// <param name="secretName">The name of the secret.</param>
/// <param name="azureKeyVaultResource">The Azure Key Vault resource.</param>
public sealed class AzureKeyVaultSecretReference(string secretName, AzureKeyVaultResource azureKeyVaultResource) : IKeyVaultSecretReference, IValueProvider, IManifestExpressionProvider
{
    /// <summary>
    /// Gets the name of the secret.
    /// </summary>
    public string SecretName => secretName;

    /// <summary>
    /// Gets the Azure Key Vault resource.
    /// </summary>
    public AzureKeyVaultResource KeyVaultResource => azureKeyVaultResource;

    string IManifestExpressionProvider.ValueExpression => $"{{{KeyVaultResource.Name}.secrets.{SecretName}}}";

    AzureBicepResource IKeyVaultSecretReference.KeyVaultResource => KeyVaultResource;

    async ValueTask<string?> IValueProvider.GetValueAsync(CancellationToken cancellationToken)
    {
        var vaultUri = await KeyVaultResource.VaultUri.GetValueAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(vaultUri))
        {
            throw new InvalidOperationException($"The vault URI for the Key Vault resource '{KeyVaultResource.Name}' is not available.");
        }

        // In run mode, we use a SecretClient to access the Key Vault secrets.
        var secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());

        var secret = await secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);

        return secret.Value.Value;
    }
}