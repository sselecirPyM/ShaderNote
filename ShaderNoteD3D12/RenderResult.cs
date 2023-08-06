using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.DXGI;

namespace ShaderNoteD3D12;

public class RenderResult : IDisposable
{
    internal NoteDevice noteDevice;
    internal RenderRecord renderRecord;
    internal bool rendered;

    internal Texture2D[] texture2Ds;
    internal Texture2D depthTexture;


    public void Save(string path, int index = 0)
    {
        CheckRender();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var desc = texture2Ds[index].resource.Description;

        SaveImage(desc.Format, path, GetTextureData(texture2Ds[index], noteDevice), (int)desc.Width, desc.Height);
    }

    public void SaveDepth(string path)
    {
        CheckRender();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var desc = depthTexture.resource.Description;

        SaveImage(desc.Format, path, GetTextureData(depthTexture, noteDevice), (int)desc.Width, desc.Height);
    }


    public byte[] GetData(int index, out int width, out int height, out Format format)
    {
        CheckRender();

        var desc = texture2Ds[index].resource.Description;
        width = (int)desc.Width;
        height = desc.Height;
        format = desc.Format;
        return GetTextureData(texture2Ds[index], noteDevice);
    }

    internal void CheckRender()
    {
        if (rendered)
            return;
        rendered = true;
        renderRecord.RenderTo(out texture2Ds, out depthTexture);
    }

    bool disposed = false;
    public void Dispose()
    {
        if (disposed)
            return;
        if (texture2Ds != null)
            foreach (var tex in texture2Ds)
                tex?.Dispose();
        texture2Ds = null;
        depthTexture?.Dispose();
        depthTexture = null;

        disposed = true;

    }
    ~RenderResult()
    {
        Dispose();
    }

    static byte[] GetTextureData(Texture2D source, NoteDevice noteDevice)
    {
        var desc = source.resource.Description;
        int bytePerPixel = (int)D3D12Helper.BitsPerPixel(desc.Format) / 8;
        noteDevice.Begin();
        byte[] data = new byte[(int)desc.Width * desc.Height * bytePerPixel];
        int offset = noteDevice.ReadBack(source);
        noteDevice.Execute();
        noteDevice.readBackBuffer.GetData<byte>(offset, desc.Height, ((int)desc.Width * bytePerPixel + 255) & ~255, (int)desc.Width * bytePerPixel, data);

        return data;
    }

    static void SaveImage(Format format, string path, byte[] data, int width, int height)
    {
        switch (format)
        {
            case Format.R16_Typeless:
                {
                    var image = Image.WrapMemory<L16>(data, width, height);
                    image.Save(path);
                    image.Dispose();
                }
                break;
            case Format.R32_Typeless:
                {
                    var data2 = MemoryMarshal.Cast<byte, float>(data);
                    byte[] data3 = new byte[width * height];
                    for (int i = 0; i < data2.Length; i++)
                    {
                        data3[i] = (byte)Math.Clamp(data2[i] * 255, 0.0f, 255.0f);
                    }
                    var image = Image.WrapMemory<L8>(data3, width, height);
                    image.Save(path);
                    image.Dispose();
                }
                break;
            case Format.R24G8_Typeless:
                {
                    byte[] data3 = new byte[width * height];
                    for (int i = 0; i < data3.Length; i++)
                    {
                        data3[i] = data[i * 4 + 2];
                    }
                    var image = Image.WrapMemory<L8>(data3, width, height);
                    image.Save(path);
                    image.Dispose();
                }
                break;
            default:
                {
                    var image = Image.WrapMemory<Rgba32>(data, width, height);
                    image.Save(path);
                    image.Dispose();
                }
                break;
        }
    }

    public string GetHtml()
    {
        CheckRender();
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < texture2Ds.Length; i++)
        {
            string temp = Guid.NewGuid().ToString();
            Save($".cache/images/{temp}.png", i);
            sb.Append($"<img class=\"result-preview\" style=\"padding:10px\" src=\".cache/images/{temp}.png\">");
        }
        if (depthTexture != null)
        {
            string temp = Guid.NewGuid().ToString();
            SaveDepth($".cache/images/{temp}.png");
            sb.Append($"<img class=\"result-preview\" style=\"padding:10px\" src=\".cache/images/{temp}.png\">");
        }
        return sb.ToString();
    }
}
