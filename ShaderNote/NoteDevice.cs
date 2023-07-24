using SharpGen.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Shader;
using Vortice.DXGI;

namespace ShaderNote;
using static D3D11;
using static D3D11Helper;

public class NoteDevice : IDisposable
{
    internal ID3D11Device5 device = null;
    internal ID3D11DeviceContext4 deviceContext = null;

    LRUCache<string, object> LRUCache;
    LRUCache<string, object> ShaderLRUCache;
    LRUCache<SamplerDescription, ID3D11SamplerState> SamplerLRUCache;

    FileSystemWatcher watcher;
    public NoteDevice(int LRUCapacity = 256, bool debug = true)
    {
        LRUCache = new(LRUCapacity, equalityComparer: StringComparer.InvariantCultureIgnoreCase);
        ShaderLRUCache = new(LRUCapacity, equalityComparer: StringComparer.InvariantCultureIgnoreCase);
        SamplerLRUCache = new(LRUCapacity);

        D3D11CreateDevice(null, DriverType.Hardware, debug ? DeviceCreationFlags.Debug : DeviceCreationFlags.None, null, out var _device, out var _deviceContext).CheckError();

        device = _device.QueryInterface<ID3D11Device5>();
        ReleaseComPtr(ref _device);
        deviceContext = _deviceContext.QueryInterface<ID3D11DeviceContext4>();
        ReleaseComPtr(ref _deviceContext);

        watcher = new FileSystemWatcher("./")
        {
            NotifyFilter = NotifyFilters.Attributes
                             | NotifyFilters.CreationTime
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.FileName
                             | NotifyFilters.LastAccess
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Security
                             | NotifyFilters.Size,

            Filter = "*.*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        watcher.Changed += Watcher_Changed;
        watcher.Deleted += Watcher_Changed;
        watcher.Renamed += Watcher_Changed;
        watcher.Created += Watcher_Changed;

        LRUCache.Deactivating += LRUCache_Deactivating;
        ShaderLRUCache.Deactivating += LRUCache_Deactivating;
        SamplerLRUCache.Deactivating += (_, e) => e.Value.Release();
    }

    private void LRUCache_Deactivating(object sender, LRUCacheEventArgs<string, object> e)
    {
        if (e.Value is ComObject com)
        {
            com.Release();
        }
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        LRUCache.InvalidCache(Path.GetFullPath(e.FullPath));
        ShaderLRUCache.InvalidCache(Path.GetFullPath(e.FullPath));
    }

    public RenderRecord GetRecord()
    {
        return new RenderRecord()
        {
            noteDevice = this,
        };
    }

    public void Dispose()
    {
        foreach (var obj in LRUCache.Values)
        {
            if (obj is ComObject com)
                com.Release();
        }
        foreach (var obj in ShaderLRUCache.Values)
        {
            if (obj is ComObject com)
                com.Release();
        }
        foreach (var obj in SamplerLRUCache.Values)
            obj.Release();

        deviceContext?.Release();
        device?.Release();

        deviceContext = null;
        device = null;
    }

    internal ID3D11SamplerState GetSampler(VariableSlot variableSlot)
    {
        SamplerDescription samplerDescription = (SamplerDescription)variableSlot.Value;
        return SamplerLRUCache.GetObject(samplerDescription, device.CreateSamplerState);
    }

    internal ID3D11ShaderResourceView GetImage(string file)
    {
        var path = Path.GetFullPath(file);

        return LRUCache.GetObject(path, (key) =>
        {
            var image = Image.Load<Rgba32>(file);
            if (!image.Frames[0].DangerousTryGetSinglePixelMemory(out var memory))
            {
                throw new Exception();
            }
            var texture = device.CreateTexture2D(memory.Span, Format.R8G8B8A8_UNorm, image.Width, image.Height);
            var srv = device.CreateShaderResourceView(texture);
            image.Dispose();
            texture.Release();

            return srv;
        }) as ID3D11ShaderResourceView;
    }

    internal ID3D11Buffer GetBuffer(VariableSlot variableSlot, BindFlags bindFlags)
    {
        if (variableSlot.Value != null)
        {
            var data = (byte[])variableSlot.Value;
            variableSlot.ShortCut ??= GetHashShortCut(data);
            return LRUCache.GetObject(variableSlot.ShortCut, (key) =>
            {
                return device.CreateBuffer(data, bindFlags, sizeInBytes: (data.Length + 64) / 64 * 64);
            }) as ID3D11Buffer;
        }
        else if (variableSlot.File != null)
        {
            var sourcePath = Path.GetFullPath(variableSlot.File);
            return LRUCache.GetObject(sourcePath, (key) =>
            {
                return device.CreateBuffer(File.ReadAllBytes(sourcePath), bindFlags);
            }) as ID3D11Buffer;
        }
        return null;
    }


    internal byte[] GetVertexShaderByteCode(VariableSlot variableSlot)
    {
        if (variableSlot.Value != null && variableSlot.Value is string source)
        {
            variableSlot.ShortCut ??= GetHashShortCut(source);

            return ShaderLRUCache.GetObject(variableSlot.ShortCut, (key) =>
            {
                return CompileShader(source, variableSlot.EntryPoint, null, "vs_5_0");
            }) as byte[];
        }

        var sourcePath = Path.GetFullPath(variableSlot.File);
        return ShaderLRUCache.GetObject(sourcePath, (key) =>
        {
            var source = File.ReadAllText(sourcePath);
            return CompileShader(source, variableSlot.EntryPoint, sourcePath, "vs_5_0");
        }) as byte[];
    }

    internal ID3D11VertexShader GetVertexShader(VariableSlot variableSlot)
    {
        if (variableSlot.Value != null && variableSlot.Value is string source)
        {
            variableSlot.ShortCut ??= GetHashShortCut(source);

            return LRUCache.GetObject(variableSlot.ShortCut, (key) =>
            {
                return device.CreateVertexShader(GetVertexShaderByteCode(variableSlot));
            }) as ID3D11VertexShader;
        }

        var sourcePath = Path.GetFullPath(variableSlot.File);
        return LRUCache.GetObject(sourcePath, (key) =>
        {
            return device.CreateVertexShader(GetVertexShaderByteCode(variableSlot));
        }) as ID3D11VertexShader;
    }

    internal ID3D11PixelShader GetPixelShader(VariableSlot variableSlot)
    {
        if (variableSlot.Value != null && variableSlot.Value is string source)
        {
            variableSlot.ShortCut ??= GetHashShortCut(source);

            return ShaderLRUCache.GetObject(variableSlot.ShortCut, (key) =>
            {
                return device.CreatePixelShader(CompileShader(source, variableSlot.EntryPoint, null, "ps_5_0"));
            }) as ID3D11PixelShader;
        }

        var sourcePath = Path.GetFullPath(variableSlot.File);
        return ShaderLRUCache.GetObject(sourcePath, (key) =>
        {
            var source = File.ReadAllText(sourcePath);
            return device.CreatePixelShader(CompileShader(source, variableSlot.EntryPoint, sourcePath, "ps_5_0"));
        }) as ID3D11PixelShader;
    }

    internal ID3D11InputLayout GetInputLayout(VariableSlot descs, VariableSlot vertexShader)
    {
        if (descs != null)
        {
            var inputElementDescriptions = (InputElementDescription[])descs.Value;
            descs.ShortCut ??= ObjectShortCut(inputElementDescriptions);

            return ShaderLRUCache.GetObject(descs.ShortCut, key =>
            device.CreateInputLayout(inputElementDescriptions, GetVertexShaderByteCode(vertexShader))) as ID3D11InputLayout;

        }
        else
        {
            string vertexShaderKey = vertexShader.ShortCut ?? vertexShader.File;
            return ShaderLRUCache.GetObject("layout_" + vertexShaderKey, key =>
            {
                using var reflection = Compiler.Reflect<ID3D11ShaderReflection>(GetVertexShaderByteCode(vertexShader));

                int count1 = 0;
                foreach (var item in reflection.InputParameters)
                    if (item.SystemValueType == SystemValueType.Undefined)
                        count1++;
                var inputElementDescriptions = new InputElementDescription[count1];
                int count = 0;

                foreach (var item in reflection.InputParameters)
                {
                    if (item.SystemValueType == SystemValueType.Undefined)
                    {
                        Format format = Format.Unknown;
                        if (item.ComponentType == RegisterComponentType.Float32)
                        {
                            if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                                format = Format.R32G32B32A32_Float;
                            else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                                format = Format.R32G32B32_Float;
                            else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                                format = Format.R32G32_Float;
                            else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                                format = Format.R32_Float;
                        }
                        inputElementDescriptions[count] = new InputElementDescription(item.SemanticName, item.SemanticIndex, format, count);
                        count++;
                    }
                }
                return device.CreateInputLayout(inputElementDescriptions, GetVertexShaderByteCode(vertexShader));
            }) as ID3D11InputLayout;
        }
    }

    string ObjectShortCut<T>(T[] source)
    {
        Span<int> hashCodes = stackalloc int[source.Length];
        for (int i = 0; i < source.Length; i++)
            hashCodes[i] = source[i].GetHashCode();

        Span<byte> buffer = stackalloc byte[32];
        SHA256.TryHashData(MemoryMarshal.AsBytes(hashCodes), buffer, out int bytesWritten);
        return new Guid(buffer.Slice(0, 16)).ToString();
    }

    string GetHashShortCut(string source)
    {
        Span<byte> buffer = stackalloc byte[32];
        SHA256.TryHashData(Encoding.UTF8.GetBytes(source), buffer, out int bytesWritten);
        return new Guid(buffer.Slice(0, 16)).ToString();
    }

    string GetHashShortCut<T>(T[] source) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[32];
        SHA256.TryHashData(MemoryMarshal.AsBytes(source.AsSpan()), buffer, out int bytesWritten);
        return new Guid(buffer.Slice(0, 16)).ToString();
    }

    static byte[] CompileShader(string shaderSource, string entryPoint, string sourceName, string profile)
    {
        var hr = Compiler.Compile(shaderSource, entryPoint, sourceName, profile, out var bVertexShader, out var errorBlob);
        string err = errorBlob?.AsString();
        errorBlob?.Release();
        if (hr.Failure)
            throw new Exception(err);
        byte[] result = bVertexShader.AsBytes();
        bVertexShader.Release();
        return result;

    }
}
