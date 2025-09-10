namespace ControlR.Libraries.Ipc;

public readonly struct CallbackToken : IEquatable<CallbackToken>
{
  public CallbackToken()
  {
    Id = Guid.NewGuid();
  }

  public Guid Id { get; }

  public static bool operator !=(CallbackToken left, CallbackToken right)
  {
    return !(left == right);
  }

  public static bool operator ==(CallbackToken left, CallbackToken right)
  {
    return left.Equals(right);
  }

  public bool Equals(CallbackToken other)
  {
    return Id == other.Id;
  }

  public override bool Equals(object? obj)
  {
    if (obj is not CallbackToken other)
    {
      return false;
    }
    return Equals(other);
  }
  public override int GetHashCode()
  {
    return Id.GetHashCode();
  }
}