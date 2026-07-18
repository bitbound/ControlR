using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace ControlR.Web.Server.Startup;

public static class DataProtectionRegistrationExtensions
{
  public static void AddControlrDataProtection(this IHostApplicationBuilder hostBuilder)
  {
    hostBuilder.Services.Configure<KeyProtectionOptions>(
      hostBuilder.Configuration.GetSection(KeyProtectionOptions.SectionKey));

    var keyProtectionOptions = hostBuilder.Configuration
      .GetSection(KeyProtectionOptions.SectionKey)
      .Get<KeyProtectionOptions>() ?? new KeyProtectionOptions();

    var dataProtectionBuilder = hostBuilder.Services
      .AddDataProtection()
      .PersistKeysToDbContext<AppDb>();

    if (!keyProtectionOptions.EncryptKeys)
    {
      dataProtectionBuilder.UnprotectKeysWithAnyCertificate();
      Console.WriteLine("Data Protection keys will NOT be encrypted at rest. " +
        "Set KeyProtectionOptions:EncryptKeys to true and configure a certificate for production environments.");
      return;
    }

    if (!string.IsNullOrWhiteSpace(keyProtectionOptions.CertificateContentsBase64))
    {
      var certBytes = Convert.FromBase64String(keyProtectionOptions.CertificateContentsBase64);
      var certificate = X509CertificateLoader.LoadPkcs12(certBytes, keyProtectionOptions.CertificatePassword);
      dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
      Console.WriteLine($"Data Protection keys will be encrypted using certificate from {nameof(KeyProtectionOptions.CertificateContentsBase64)}.");
    }
    else
    {
      if (string.IsNullOrWhiteSpace(keyProtectionOptions.CertificatePath))
      {
        throw new InvalidOperationException(
          "KeyProtectionOptions:EncryptKeys is true, but KeyProtectionOptions:CertificatePath is not configured. " +
          "Provide a valid path to a PFX certificate file.");
      }

      if (!File.Exists(keyProtectionOptions.CertificatePath))
      {
        throw new InvalidOperationException(
          $"KeyProtectionOptions:EncryptKeys is true, but the certificate file does not exist: " +
          $"{keyProtectionOptions.CertificatePath}");
      }

      var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        keyProtectionOptions.CertificatePath,
        keyProtectionOptions.CertificatePassword);

      dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
      Console.WriteLine("Data Protection keys will be encrypted using certificate from file.");
    }
  }
}