// TODO: This can be removed after next .NET 10 release.
// See https://github.com/dotnet/aspnetcore/issues/64009
export function onRuntimeConfigLoaded(config) {
    config.disableNoCacheFetch = true;
}