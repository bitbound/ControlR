namespace ControlR.Viewer.Platforms.Android;

public class InstallPackagesPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
    {
        get
        {
            return
            [
                (global::Android.Manifest.Permission.RequestInstallPackages, true),
                (global::Android.Manifest.Permission.InstallPackages, true)
            ];
        }
    }
}