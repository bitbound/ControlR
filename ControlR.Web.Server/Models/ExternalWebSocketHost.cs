﻿using ControlR.Web.Server.Interfaces;

namespace ControlR.Web.Server.Models;

public class ExternalWebSocketHost : ICoordinate
{
  public string? Label { get; set; }
  public double Latitude { get; set; }
  public double Longitude { get; set; }
  public Uri? Origin { get; set; }
}
