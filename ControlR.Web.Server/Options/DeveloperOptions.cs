namespace ControlR.Web.Server.Options;

public class DeveloperOptions
{
  public const string SectionKey = "DeveloperOptions";
  public GlobalRenderMode RenderMode { get; set; }
}
  
public enum GlobalRenderMode
{
  Auto,
  Server,
  WebAssembly,
}