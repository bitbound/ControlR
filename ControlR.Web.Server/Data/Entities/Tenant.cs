﻿using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class Tenant : EntityBase
{
  public List<Device>? Devices { get; set; }
  public string? Name { get; set; }
  public List<Tag>? Tags { get; set; }
  public List<AppUser>? Users { get; set; }
}
