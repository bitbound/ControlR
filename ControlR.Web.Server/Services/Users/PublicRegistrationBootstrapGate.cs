namespace ControlR.Web.Server.Services.Users;

/// <summary>
/// Application-wide gate that serializes first-user self-registration
/// to prevent racing admin promotions on an empty server.
/// </summary>
public interface IPublicRegistrationBootstrapGate
{
  Task<IDisposable> AcquireAsync(CancellationToken cancellationToken);
}

public class PublicRegistrationBootstrapGate : IPublicRegistrationBootstrapGate
{
  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
  {
    return await _semaphore.AcquireLockAsync(cancellationToken);
  }
}
