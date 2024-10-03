namespace ControlR.Libraries.Shared.Dtos.Interfaces;
public interface IEntityBaseDto : IHasSettablePrimaryKey, IHasSettableUid
{
}

public interface IReadOnlyEntityBaseDto : IHasPrimaryKey, IHasUid
{

}