namespace ControlR.ApiClient.Tests;

/// <summary>
///   Exercises the process-wide static <see cref="ControlrApiClientBuilder"/>.
/// </summary>
/// <remarks>
///   The builder holds one static client for the whole process, so this class must remain the only
///   one that touches it. That is enforced rather than merely requested: test parallelization is
///   disabled for this assembly in <c>AssemblyInfo.cs</c>. Keep it that way if another class ever
///   needs the builder, or move both into one serialized collection.
/// </remarks>
public sealed class ControlrApiClientBuilderTests : IDisposable
{
  private const string ServerUrl = "https://server.test/";

  public ControlrApiClientBuilderTests()
  {
    // Guarantee a clean slate regardless of which method ran last, since Initialize is
    // first-call-wins and would otherwise silently ignore a later test's configuration.
    ControlrApiClientBuilder.Dispose();
  }

  public void Dispose() => ControlrApiClientBuilder.Dispose();

  [Fact]
  public void GetAuthSession_AfterDispose_ThrowsInvalidOperation()
  {
    ControlrApiClientBuilder.Initialize(options => options.BaseUrl = new Uri(ServerUrl));
    Assert.NotNull(ControlrApiClientBuilder.GetAuthSession());

    ControlrApiClientBuilder.Dispose();

    // The factory that owned the session is gone, so a caller holding the previously returned
    // session has a dead object. The builder must refuse to hand out a new one rather than
    // resurrect a session that no longer owns a transport.
    Assert.Throws<InvalidOperationException>(() => ControlrApiClientBuilder.GetAuthSession());
  }

  [Fact]
  public void GetAuthSession_ReturnsSameSessionForRepeatedCalls()
  {
    ControlrApiClientBuilder.Initialize(options => options.BaseUrl = new Uri(ServerUrl));

    var first = ControlrApiClientBuilder.GetAuthSession();
    var second = ControlrApiClientBuilder.GetAuthSession();

    Assert.Same(first, second);
  }

  [Fact]
  public void GetAuthSession_WhenInitializedWithPersonalAccessToken_SeesConfiguredCredential()
  {
    ControlrApiClientBuilder.Initialize(options =>
    {
      options.BaseUrl = new Uri(ServerUrl);
      options.PersonalAccessToken = "configured-pat";
    });

    // The lazily created session must be built over the same auth state the target was configured
    // with, otherwise a bearer sign-in through the session would start from a blank state and the
    // configured PAT would be invisible to it.
    Assert.Equal("configured-pat", ControlrApiClientBuilder.GetAuthSession().PersonalAccessToken);
  }

  [Fact]
  public void GetAuthSession_WhenNotInitialized_ThrowsInvalidOperation()
  {
    Assert.Throws<InvalidOperationException>(() => ControlrApiClientBuilder.GetAuthSession());
  }
}
