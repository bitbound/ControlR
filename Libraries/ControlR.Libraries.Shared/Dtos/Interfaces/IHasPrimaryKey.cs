namespace ControlR.Libraries.Shared.Dtos.Interfaces;

public interface IHasPrimaryKey
{
  int Id { get; }
}

public interface IHasSettablePrimaryKey : IHasPrimaryKey
{
  new int Id { get; set; }
}