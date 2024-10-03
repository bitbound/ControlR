namespace ControlR.Libraries.Shared.Dtos.Interfaces;
public interface IHasUid
{
  Guid Uid { get; }
}

public interface IHasSettableUid : IHasUid
{
  new Guid Uid { get; set; }
}
