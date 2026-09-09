// ControlrApiClientBuilderTests exercises the process-wide static ControlrApiClientBuilder, whose
// Initialize is first-call-wins. A second test class touching the builder would race against it, and
// the result would look like flakiness rather than a collision. Parallelization is disabled for the
// whole assembly because a comment on one class cannot stop a future class from being scheduled
// alongside it. The suite runs in a few seconds, so the lost concurrency is cheap.
[assembly: Xunit.v3.ParallelizationAttribute(Mode = Xunit.Sdk.ParallelMode.None)]
