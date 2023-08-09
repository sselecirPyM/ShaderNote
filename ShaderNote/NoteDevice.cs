using SharpGen.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
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

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        LRUCache.InvalidCache(Path.GetFullPath(e.FullPath));
        ShaderLRUCache.InvalidCache(Path.GetFullPath(e.FullPath));
    }

    private void LRUCache_Deactivating(object sender, LRUCacheEventArgs<string, object> e)
    {
        if (e.Value is ComObject com)
        {
            com.Release();
        }
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
            return LRUCache.GetObject(variableSlot.ShortCut, (key) =>
            {
                var data = (byte[])variableSlot.Value;
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

    internal byte[] GetShaderByteCode(VariableSlot variableSlot, string profile)
    {
        if (variableSlot.Value != null && variableSlot.Value is string source)
        {
            return ShaderLRUCache.GetObject(variableSlot.ShortCut, (key) =>
            {
                return CompileShader(source, variableSlot.EntryPoint, null, profile);
            }) as byte[];
        }

        var sourcePath = Path.GetFullPath(variableSlot.File);
        return ShaderLRUCache.GetObject(sourcePath, (key) =>
        {
            var source = File.ReadAllText(sourcePath);
            return CompileShader(source, variableSlot.EntryPoint, sourcePath, profile);
        }) as byte[];
    }
    internal byte[] GetVertexShaderByteCode(VariableSlot variableSlot)
    {
        return GetShaderByteCode(variableSlot, "vs_5_0");
    }
    internal byte[] GetPixelShaderByteCode(VariableSlot variableSlot)
    {
        return GetShaderByteCode(variableSlot, "ps_5_0");
    }

    internal ID3D11VertexShader GetVertexShader(VariableSlot variableSlot)
    {
        return LRUCache.GetObject(variableSlot.ShortCut, (key) =>
        {
            return device.CreateVertexShader(GetVertexShaderByteCode(variableSlot));
        }) as ID3D11VertexShader;
    }

    internal ID3D11PixelShader GetPixelShader(VariableSlot variableSlot)
    {
        return ShaderLRUCache.GetObject(variableSlot.ShortCut, (key) =>
        {
            return device.CreatePixelShader(GetPixelShaderByteCode(variableSlot));
        }) as ID3D11PixelShader;
    }

    internal ID3D11InputLayout GetInputLayout(VariableSlot descs, VariableSlot vertexShader)
    {
        if (descs != null)
        {
            var inputElementDescriptions = (InputElementDescription[])descs.Value;

            return ShaderLRUCache.GetObject(descs.ShortCut, key =>
            device.CreateInputLayout(inputElementDescriptions, GetVertexShaderByteCode(vertexShader))) as ID3D11InputLayout;
        }
        else
        {
            string vertexShaderKey = vertexShader.ShortCut ?? vertexShader.File;
            return ShaderLRUCache.GetObject("layout_" + vertexShaderKey, key =>
            {
                using var reflection = Compiler.Reflect<ID3D11ShaderReflection>(GetVertexShaderByteCode(vertexShader));

                var inputElementDescriptions = GetInputElementDescriptions(reflection);
                return device.CreateInputLayout(inputElementDescriptions, GetVertexShaderByteCode(vertexShader));
            }) as ID3D11InputLayout;
        }
    }
    static InputElementDescription[] GetInputElementDescriptions(ID3D11ShaderReflection reflection)
    {
        int count1 = 0;
        foreach (var item in reflection.InputParameters)
            if (item.SystemValueType == SystemValueType.Undefined)
                count1++;
        var descs = new InputElementDescription[count1];

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
                descs[count] = new InputElementDescription(item.SemanticName, item.SemanticIndex, format, count);
                count++;
            }
        }
        return descs;
    }


    internal ID3D11BlendState GetBlendState(VariableSlot variableSlot)
    {
        return ShaderLRUCache.GetObject(variableSlot.ShortCut, (key) =>
        {
            return device.CreateBlendState((BlendDescription)variableSlot.Value);
        }) as ID3D11BlendState;
    }

    internal ID3D11DepthStencilState GetDepthStencilState(VariableSlot variableSlot)
    {
        return ShaderLRUCache.GetObject(variableSlot.ShortCut, (key) =>
        {
            return device.CreateDepthStencilState((DepthStencilDescription)variableSlot.Value);
        }) as ID3D11DepthStencilState;
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
