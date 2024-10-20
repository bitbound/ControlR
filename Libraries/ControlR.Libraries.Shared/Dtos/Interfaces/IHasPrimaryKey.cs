namespace ControlR.Libraries.Shared.Dtos.Interfaces;

public interface IHasPrimaryKey
{
  Guid Id { get; }
}

public interface IHasSettablePrimaryKey : IHasPrimaryKey
{
  new Guid Id { get; set; }
}