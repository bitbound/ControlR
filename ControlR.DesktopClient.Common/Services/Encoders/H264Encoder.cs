using System.Buffers;
using System.Diagnostics;
using System.Threading.Channels;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Helpers;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

public class H264Encoder : IStreamEncoder
{
    private Process? _ffmpegProcess;
    private Stream? _inputStream;
    private readonly Channel<byte[]> _packetChannel = Channel.CreateUnbounded<byte[]>();
    private bool _disposed;
    private Task? _readTask;
    private CancellationTokenSource? _cts;

    public CaptureEncoderType Type => CaptureEncoderType.H264;

    public void Start(int width, int height, int quality)
    {
        if (_ffmpegProcess != null)
        {
            throw new InvalidOperationException("Encoder already started.");
        }

        _cts = new CancellationTokenSource();

        // Adjust CRF (quality) - lower is better. 0-51.
        // Map 0-100 quality to 51-0 CRF roughly.
        // quality 100 -> crf 0 (lossless)
        // quality 0 -> crf 51
        var crf = (int)((100 - quality) * 0.51);

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-f rawvideo -pix_fmt bgra -s {width}x{height} -r 30 -i pipe:0 -c:v libx264 -preset ultrafast -tune zerolatency -crf {crf} -f h264 pipe:1",
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
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
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
}
