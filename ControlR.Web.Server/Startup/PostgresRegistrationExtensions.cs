using Azure.Core;
using Azure.Identity;
using ControlR.Web.Server.Data.Configuration;
using Npgsql;

namespace ControlR.Web.Server.Startup;

public static class PostgresRegistrationExtensions
{
  public static void AddControlrPostgresDb(
    this IHostApplicationBuilder builder,
    AppOptions appOptions)
  {
    var pgUser = builder.Configuration.GetValue<string>("POSTGRES_USER");
    var pgPass = builder.Configuration.GetValue<string>("POSTGRES_PASSWORD");
    var pgHost = builder.Configuration.GetValue<string>("POSTGRES_HOST");
    var pgDb = builder.Configuration.GetValue<string>("POSTGRES_DB");
    var pgPortRaw = builder.Configuration.GetValue<string>("POSTGRES_PORT");
    var useEntraId = builder.Configuration.GetValue<bool>("POSTGRES_USE_ENTRA_ID");
    var pgPort = 5432;

    ArgumentException.ThrowIfNullOrWhiteSpace(pgUser);
    ArgumentException.ThrowIfNullOrWhiteSpace(pgHost);

    if (!useEntraId)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(pgPass);
    }

    if (string.IsNullOrWhiteSpace(pgDb))
    {
      pgDb = "controlr";
    }

    if (int.TryParse(pgPortRaw, out var parsedPort))
    {
      pgPort = parsedPort;
    }

    if (Uri.TryCreate(pgHost, UriKind.Absolute, out var pgHostUri))
    {
      pgHost = pgHostUri.Host;
      if (pgHostUri.Port > 0)
      {
        pgPort = pgHostUri.Port;
      }
    }

    var pgBuilder = new NpgsqlConnectionStringBuilder
    {
      Database = pgDb,
      Username = pgUser,
      Host = pgHost,
      Port = pgPort
    };

    if (useEntraId)
    {
      // Azure Database for PostgreSQL requires SSL for Entra ID authentication.
      pgBuilder.SslMode = SslMode.Require;

      var credentialOptions = new DefaultAzureCredentialOptions();
      var credential = new DefaultAzureCredential(credentialOptions);

      var dataSourceBuilder = new NpgsqlDataSourceBuilder(pgBuilder.ConnectionString);
      dataSourceBuilder.UsePeriodicPasswordProvider(
        passwordProvider: async (_, cancellationToken) =>
        {
          var tokenContext = new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]);
          var accessToken = await credential.GetTokenAsync(tokenContext, cancellationToken);
          return accessToken.Token;
        },
        successRefreshInterval: TimeSpan.FromMinutes(55),
        failureRefreshInterval: TimeSpan.FromSeconds(10));

      builder.Services.AddSingleton(dataSourceBuilder.Build());
    }
    else
    {
      pgBuilder.Password = pgPass;
    }

    builder.Services.AddDbContextFactory<AppDb>((sp, options) =>
    {
      if (useEntraId)
      {
        var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
        options.UseNpgsql(dataSource);
      }
      else
      {
        options.UseNpgsql(pgBuilder.ConnectionString);
      }

      options.EnableDetailedErrors(appOptions.EnableDatabaseDetailedErrors);
      options.AddInterceptors(new ServiceAccountInvariantInterceptor());

      var accessor = sp.GetRequiredService<IHttpContextAccessor>();
      if (accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user)
      {
        options.UseUserClaims(user);
      }
    }, lifetime: ServiceLifetime.Transient);
  }
}