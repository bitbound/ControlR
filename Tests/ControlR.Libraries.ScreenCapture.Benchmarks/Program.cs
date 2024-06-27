using ControlR.Libraries.ScreenCapture.Benchmarks;


//var config = new DebugInProcessConfig();
//var summary = BenchmarkRunner.Run<CaptureTests>(config);
//Console.WriteLine($"{summary}");

var test = new CaptureTests();
//test.DoCaptures();
//test.DoEncoding();
//test.DoCaptureEncodeAndDiff();
//test.DoDiffSizeComparison();
test.DoSaveToDesktop();
//await test.DoWinRtComparison();