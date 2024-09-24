using Riok.Mapperly.Abstractions;

namespace ControlR.Web.Server.Mappers;

[Mapper]
public partial class DeviceDtoToDeviceMapper : IMapper<DeviceDto, Device>
{
  public partial Device Map(DeviceDto from);
  public partial void Map(DeviceDto from, Device to);

}

[Mapper]
public partial class DeviceToDeviceDtoMapper : IMapper<Device, DeviceDto>
{
  public partial DeviceDto Map(Device from);
  public partial void Map(Device from, DeviceDto to);
}


[Mapper]
public partial class DeviceFromAgentDtoToDeviceMapper : IMapper<DeviceFromAgentDto, Device>
{
  public partial Device Map(DeviceFromAgentDto from);
  public partial void Map(DeviceFromAgentDto from, Device to);
}

[Mapper]
public partial class DeviceToDeviceFromAgentDtoMapper : IMapper<Device, DeviceFromAgentDto>
{
  public partial DeviceFromAgentDto Map(Device from);
  public partial void Map(Device from, DeviceFromAgentDto to);
}

[Mapper]
public partial class DeviceDtoToDeviceFromAgentDtoMapper : IMapper<DeviceDto, DeviceFromAgentDto>
{
  public partial DeviceFromAgentDto Map(DeviceDto from);
  public partial void Map(DeviceDto from, DeviceFromAgentDto to);
}

[Mapper]
public partial class DeviceFromAgentDtoToDeviceDtoMapper : IMapper<DeviceFromAgentDto, DeviceDto>
{
  public partial DeviceDto Map(DeviceFromAgentDto from);
  public partial void Map(DeviceFromAgentDto from, DeviceDto to);
}
