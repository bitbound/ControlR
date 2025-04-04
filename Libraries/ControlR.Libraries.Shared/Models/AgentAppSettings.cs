using System.Text.Json.Nodes;

namespace ControlR.Libraries.Shared.Models;

public class AgentAppSettings
{
  public AgentAppOptions? AppOptions { get; set; }
  public JsonNode? Serilog { get; set; }
}