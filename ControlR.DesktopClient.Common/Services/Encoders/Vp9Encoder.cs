using System.Diagnostics;
using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Enums;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

// TODO: Implementation stub.
public class Vp9Encoder : IStreamEncoder
{
    private const uint DefaultFrameRate = 30;
    private readonly Channel<byte[]> _packetChannel = Channel.CreateUnbounded<byte[]>();

    private CancellationTokenSource? _cts;
    private bool _disposed;
    private Process? _ffmpegProcess;
    private Stream? _inputStream;
    private Task? _readTask;

    public CaptureEncoderType Type => CaptureEncoderType.Vpx;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        
        try
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                _ffmpegProcess.Kill();
            }
        }
        catch { }
        
        _ffmpegProcess?.Dispose();
        _cts?.Dispose();
    }
    public void EncodeFrame(SKBitmap frame, bool forceKeyFrame = false)
    {
        if (_inputStream == null || _disposed) return;

        try
        {
            var pixels = frame.GetPixels();
            var length = frame.RowBytes * frame.Height;
            
            unsafe
            {
                using var unmanagedStream = new UnmanagedMemoryStream((byte*)pixels, length);
                unmanagedStream.CopyTo(_inputStream);
            }
            _inputStream.Flush();
        }
        catch
        {
            // Ignore write errors (process might have died)
        }
    }
    public byte[]? GetNextPacket()
    {
        if (_packetChannel.Reader.TryRead(out var packet))
        {
            return packet;
        }
        return null;
    }
    public void Start(int width, int height, int quality)
    {
        if (_ffmpegProcess != null)
        {
            throw new InvalidOperationException("Encoder already started.");
        }

        _cts = new CancellationTokenSource();

        var ffmpegArgs = $"-y -use_wallclock_as_timestamps 1 -f rawvideo -pixel_format bgra " +
                    $"-video_size {width}x{height} -i pipe:0 " +
                    $"-c:v libvpx-vp9 -deadline realtime -cpu-used 8 -row-mt 1 " +
                    $"-vf \"format=yuv420p\" -g 60 -vsync vfr -f webm pipe:1";

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = ffmpegArgs,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _ffmpegProcess = Process.Start(startInfo);
        if (_ffmpegProcess == null)
        {
            throw new InvalidOperationException("Failed to start ffmpeg.");
        }

        _inputStream = _ffmpegProcess.StandardInput.BaseStream;
        
        // Consume stderr
        _ = Task.Run(async () =>
        {
            try
            {
                using var reader = _ffmpegProcess.StandardError;
                while (await reader.ReadLineAsync(_cts.Token) != null)
                {
                    // Consume
                }
            }
            catch { }
        });

        // Read stdout
        _readTask = Task.Run(async () =>
        {
            try
            {
                using var stream = _ffmpegProcess.StandardOutput.BaseStream;
                var buffer = new byte[4096 * 4]; // 16KB buffer
                while (!_cts.Token.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, _cts.Token);
                    if (bytesRead == 0) break;

                    var packetData = new byte[bytesRead];
                    Array.Copy(buffer, packetData, bytesRead);
                    
                    await _packetChannel.Writer.WriteAsync(packetData, _cts.Token);
                }
            }
            catch { }
            finally
            {
                _packetChannel.Writer.TryComplete();
            }
        });
    }
}
