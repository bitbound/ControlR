namespace ControlR.Web.Client.Services.Stores;

public class UserStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserStore> logger) : StoreBase<UserResponseDto>(controlrApi, snackbar, logger)
{
  protected override async Task RefreshImpl()
  {
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