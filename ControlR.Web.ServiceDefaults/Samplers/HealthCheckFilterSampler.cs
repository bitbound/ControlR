using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ControlR.Web.ServiceDefaults.Samplers;

public class HealthCheckFilterSampler : Sampler
{
    private readonly Sampler _baseSampler = new AlwaysOnSampler();

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.ParentContext.IsValid() &&
            Activity.Current?.GetTagItem("http.target") is string requestPath &&
            requestPath.StartsWith("/health"))
        {
            return new SamplingResult(SamplingDecision.Drop);
        }

        return _baseSampler.ShouldSample(samplingParameters);
    }
}