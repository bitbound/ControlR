namespace ControlR.Web.Server.Mappers;

public interface IMapper<TFrom, TTo>
{
  TTo Map(TFrom from);
  void Map(TFrom from, TTo to);
}