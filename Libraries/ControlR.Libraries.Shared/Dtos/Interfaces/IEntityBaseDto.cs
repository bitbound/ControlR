namespace ControlR.Libraries.Shared.Dtos.Interfaces;
public interface IEntityBaseDto : IHasSettablePrimaryKey
{
}

public interface IReadOnlyEntityBaseDto : IHasPrimaryKey
{

}