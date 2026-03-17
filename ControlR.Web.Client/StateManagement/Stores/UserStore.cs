namespace ControlR.Web.Client.StateManagement.Stores;

public interface IUserStore : IStoreBase<UserResponseDto>
{
}

public class UserStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserStore> logger) : StoreBase<UserResponseDto>(controlrApi, snackbar, logger), IUserStore
{
  protected override Guid GetItemId(UserResponseDto dto)
  {
    return dto.Id;
  }

  protected override async Task RefreshImpl()
  {
    var getResult = await ControlrApi.Users.GetAllUsers();
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }
    SetItems(getResult.Value);
  }
}