using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ShaderNote;

public class RenderResult : IDisposable
{
    internal NoteDevice noteDevice;
    internal ID3D11Texture2D[] texture2Ds;
    internal ID3D11Texture2D depthTexture;

    internal RenderRecord renderRecord;

    internal bool rendered;


    public void Save(string path, int index = 0)
    {
        CheckRender();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var desc = texture2Ds[index].Description;

        SaveImage(desc.Format, path, GetTextureData(texture2Ds[index], noteDevice.device, noteDevice.deviceContext), desc.Width, desc.Height);
    }

    public void SaveDepth(string path)
    {
        CheckRender();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var desc = depthTexture.Description;

        SaveImage(desc.Format, path, GetTextureData(depthTexture, noteDevice.device, noteDevice.deviceContext), desc.Width, desc.Height);
    }

    public byte[] GetData(int index, out int width, out int height, out Format format)
    {
        CheckRender();

        var desc = texture2Ds[index].Description;
        width = desc.Width;
        height = desc.Height;
        format = desc.Format;
        return GetTextureData(texture2Ds[index], noteDevice.device, noteDevice.deviceContext);
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

    internal void CheckRender()
    {
        if (rendered)
            return;
        rendered = true;

        renderRecord.RenderTo(out texture2Ds, out depthTexture);
    }

    public void Dispose()
    {
        if (texture2Ds != null)
        {
            foreach (var texture2D in texture2Ds)
                texture2D?.Release();
            texture2Ds = null;
        }
        depthTexture?.Release();
        depthTexture = null;
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

    static byte[] GetTextureData(ID3D11Texture2D source, ID3D11Device device, ID3D11DeviceContext context)
    {
        var desc = source.Description;
        Texture2DDescription tex2dReadbackDesc = new Texture2DDescription(desc.Format, desc.Width, desc.Height, 1, 1, 0, ResourceUsage.Staging, CpuAccessFlags.Read);
        ID3D11Texture2D tex2dReadBack = device.CreateTexture2D(tex2dReadbackDesc);
        context.CopyResource(tex2dReadBack, source);

        Span<byte> mappedResource = context.Map<byte>(tex2dReadBack, 0, 0, MapMode.Read);

        byte[] data = mappedResource.ToArray();
        context.Unmap(tex2dReadBack, 0);
        tex2dReadBack.Dispose();

        return data;
    }



    ~RenderResult()
    {
        Dispose();
    }
}
