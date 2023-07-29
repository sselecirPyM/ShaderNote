using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShaderNote;

public static class RenderHelper
{
    public static void GIF(Func<int, RenderRecord> render, string fileName, int width, int height, int frameEnd, int channel = 0, int startFrame = 0, int frameDelay = 20)
    {
        Image<Rgba32> image = new Image<Rgba32>(width, height);
        var meta = image.Frames.RootFrame.Metadata.GetGifMetadata();
        meta.FrameDelay = frameDelay;
        var meta1 = image.Metadata.GetGifMetadata();
        meta1.RepeatCount = 0;

        for (int i = startFrame; i < frameEnd; i++)
        {
            var result = render(i).WithSize(width, height).Render();
            byte[] imageData = result.GetData(channel, out _, out _, out _);
            image.Frames.AddFrame(MemoryMarshal.Cast<byte, Rgba32>(imageData));
            if (i == startFrame)
            {
                image.Frames.RemoveFrame(0);
            }

            result.Dispose();
        }
        image.SaveAsGif(fileName);
        image.Dispose();
    }

    public static void Mp4(Func<int, RenderRecord> render, string fileName, int width, int height, int frameEnd, int channel = 0, int startFrame = 0, float frameRate = 30)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo()
        {
            ArgumentList =
            {
                "-y",
                "-r",frameRate.ToString(),
                "-i","pipe:0",
                "-s",$"{width}x{height}",
                "-pix_fmt","yuv420p",
                fileName
            },
            FileName = "ffmpeg",
            RedirectStandardInput = true,
        };
        var process = Process.Start(processStartInfo);
        for (int i = startFrame; i < frameEnd; i++)
        {
            var result = render(i).WithSize(width, height).Render();
            byte[] imageData = result.GetData(channel, out _, out _, out _);
            result.Dispose();
            Image<Rgba32> image = Image.WrapMemory<Rgba32>(imageData, width, height);
            image.SaveAsBmp(process.StandardInput.BaseStream);
            image.Dispose();
        }
        process.StandardInput.BaseStream.Dispose();
    }
}
