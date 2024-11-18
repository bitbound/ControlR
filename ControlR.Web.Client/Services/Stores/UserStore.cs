namespace ControlR.Web.Client.Services.Stores;

public interface IUserStore : IStoreBase<UserResponseDto>
{
}

public class UserStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserStore> logger) : StoreBase<UserResponseDto>(controlrApi, snackbar, logger), IUserStore
{
  protected override async Task RefreshImpl()
  {
    Cache.Clear();
    var getResult = await ControlrApi.GetAllUsers();
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    foreach (var user in getResult.Value)
    {
      Cache.AddOrUpdate(user.Id, user, (_, _) => user);
    }
  }
}