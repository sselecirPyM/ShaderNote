using SharpGen.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.Direct3D12.Shader;
using Vortice.Dxc;
using Vortice.DXGI;

namespace ShaderNoteD3D12;

public class NoteDevice : IDisposable
{
    internal ID3D12Device5 device;
    internal ID3D12GraphicsCommandList4 commandList;
    internal CommandQueue commandQueue;

    internal DescriptorHeapX srv;
    internal DescriptorHeapX rtv;
    internal DescriptorHeapX dsv;
    //internal DescriptorHeapX sampler;

    internal DynamicBuffer uploadBuffer;
    internal DynamicBuffer readBackBuffer;

    FileSystemWatcher watcher;
    LRUCache<string, object> LRUCache;
    public NoteDevice()
    {
#if DEBUG
        if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
            pDx12Debug.EnableDebugLayer();
#endif
        var factory = DXGI.CreateDXGIFactory1<IDXGIFactory6>();
        var adapter = factory.EnumAdapterByGpuPreference<IDXGIAdapter>(0, GpuPreference.HighPerformance);
        factory.Release();

        device = D3D12.D3D12CreateDevice<ID3D12Device5>(adapter, FeatureLevel.Level_11_0);
        adapter.Release();
        commandQueue = new CommandQueue();
        commandQueue.Initialize(device, CommandListType.Direct);
        commandList = commandQueue.GetCommandList();

        srv = new DescriptorHeapX();
        srv.Initialize(device, new DescriptorHeapDescription()
        {
            DescriptorCount = 4096,
            Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            Flags = DescriptorHeapFlags.ShaderVisible
        });

        rtv = new DescriptorHeapX();
        rtv.Initialize(device, new DescriptorHeapDescription()
        {
            DescriptorCount = 256,
            Type = DescriptorHeapType.RenderTargetView
        });
        dsv = new DescriptorHeapX();
        dsv.Initialize(device, new DescriptorHeapDescription()
        {
            DescriptorCount = 64,
            Type = DescriptorHeapType.DepthStencilView
        });
        //sampler.Initialize(device, new DescriptorHeapDescription()
        //{
        //    DescriptorCount = 128,
        //    Type = DescriptorHeapType.Sampler
        //});

        readBackBuffer = new DynamicBuffer();
        readBackBuffer.CreateReadBackBuffer(device, 1048576 * 64);
        uploadBuffer = new DynamicBuffer();
        uploadBuffer.CreateUploadBuffer(device, 1048576 * 64);

        InitCaches();
    }

    private void InitCaches()
    {
        LRUCache = new(512, equalityComparer: StringComparer.InvariantCultureIgnoreCase);
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
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        LRUCache.InvalidCache(Path.GetFullPath(e.FullPath));
        //ShaderLRUCache.InvalidCache(Path.GetFullPath(e.FullPath));
    }

    private void LRUCache_Deactivating(object sender, LRUCacheEventArgs<string, object> e)
    {
        if (e.Value is ComObject com)
        {
            com.Release();
        }
    }

    internal void Begin()
    {
        commandQueue.GetCommandList();
        commandList.Reset(commandQueue.GetCommandAllocator());
        commandList.SetDescriptorHeaps(srv.heap);
    }

    internal void Execute()
    {
        commandList.Close();
        commandQueue.ExecuteCommandList(commandList);
        commandQueue.NextExecuteIndex();
        commandQueue.Wait();
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
        if (device == null)
            return;

        srv?.Dispose();
        rtv?.Dispose();
        dsv?.Dispose();

        commandQueue.Dispose();
        device.Dispose();

        device = null;
    }

    internal ulong Upload(ReadOnlySpan<byte> data)
    {
        return (ulong)uploadBuffer.UploadData(data) + uploadBuffer.buffer.GPUVirtualAddress;
    }

    internal int ReadBack(Texture2D source)
    {
        var desc = source.resource.Description;
        source.StateTrans(commandList, ResourceStates.CopySource);
        int RowPitch = ((int)desc.Width * 4 + 255) & ~255;
        int offset = readBackBuffer.GetOffsetAndMove(RowPitch * desc.Height);
        PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
        footPrint.Footprint.Width = (int)desc.Width;
        footPrint.Footprint.Height = desc.Height;
        footPrint.Footprint.Depth = 1;
        footPrint.Footprint.RowPitch = RowPitch;
        footPrint.Footprint.Format = (desc.Format == Format.R24G8_Typeless) ? Format.R32_Typeless : desc.Format;
        footPrint.Offset = (ulong)offset;

        TextureCopyLocation Dst = new TextureCopyLocation(readBackBuffer.buffer, footPrint);
        TextureCopyLocation Src = new TextureCopyLocation(source.resource, 0);
        commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);

        return offset;
    }

    internal ulong GetBuffer(VariableSlot variableSlot)
    {
        byte[] bytes = (byte[])variableSlot.Value;
        return Upload(bytes);
    }

    internal GpuDescriptorHandle GetCBV(VariableSlot variableSlot)
    {
        byte[] bytes = (byte[])variableSlot.Value;
        var addr = Upload(bytes);
        srv.GetTempHandle(out var cpuHandle, out var gpuHandle);
        device.CreateConstantBufferView(new ConstantBufferViewDescription(addr, (bytes.Length + 255) & ~255), cpuHandle);
        return gpuHandle;
    }

    internal static byte[] GetShader(VariableSlot variableSlot, DxcShaderStage shaderStage, out ID3D12ShaderReflection reflection)
    {
        if (variableSlot == null)
        {
            reflection = null;
            return null;
        }
        using var result = DxcCompiler.Compile(shaderStage, File.ReadAllText(variableSlot.File), variableSlot.EntryPoint, fileName: variableSlot.File);
        reflection = DxcCompiler.Utils.CreateReflection<ID3D12ShaderReflection>(result.GetOutput(DxcOutKind.Reflection));
        return result.GetObjectBytecodeArray();
    }

    internal void BindResources(RenderStates renderStates)
    {
        for (int i = 0; i < renderStates.currentRootDescriptor.Length; i++)
        {
            RootParameter1 a = renderStates.currentRootDescriptor[i];
            var range = a.DescriptorTable.Ranges[0];
            if (range.RangeType == DescriptorRangeType.ConstantBufferView)
            {
                if (renderStates.CBV.TryGetValue(range.BaseShaderRegister, out var handle))
                {
                    commandList.SetGraphicsRootDescriptorTable(i, handle);
                }
            }
            else if (range.RangeType == DescriptorRangeType.ShaderResourceView)
            {
                if (renderStates.SRV.TryGetValue(range.BaseShaderRegister, out var handle))
                {
                    commandList.SetGraphicsRootDescriptorTable(i, handle);
                }
            }
        }
    }

    internal ID3D12PipelineState GetPipelineState(RenderStates renderStates)
    {
        var vs = GetShader(renderStates.vertexShader1, DxcShaderStage.Vertex, out var vsReflection);
        var ps = GetShader(renderStates.pixelShader1, DxcShaderStage.Pixel, out var psReflection);
        using var _1 = vsReflection;
        using var _2 = psReflection;

        var rootSignatureDescription = GetRootSignatureDescription(renderStates, psReflection ?? vsReflection);
        var rootSignature = device.CreateRootSignature(rootSignatureDescription);
        commandList.SetGraphicsRootSignature(rootSignature);
        renderStates.currentRootDescriptor = rootSignatureDescription.Parameters;

        var pipelineState = device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
        {
            VertexShader = vs,
            PixelShader = ps,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RasterizerState = new RasterizerDescription()
            {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                FrontCounterClockwise = false,
            },
            InputLayout = new InputLayoutDescription()
            {
                Elements = GetInputElementDescriptions(vsReflection)
            },
            RootSignature = rootSignature,
            RenderTargetFormats = renderStates.formats,
            BlendState = renderStates.blendDescription,
            DepthStencilState = renderStates.depthStencilDescription,
            DepthStencilFormat = renderStates.depthFormat,
        });

        commandQueue.CommandRef(rootSignature);
        commandQueue.CommandRef(pipelineState);
        rootSignature.Release();
        pipelineState.Release();
        return pipelineState;
    }

    static RootSignatureDescription1 GetRootSignatureDescription(RenderStates renderStates, ID3D12ShaderReflection reflection)
    {
        var parameters = new List<RootParameter1>();
        var samplers = new List<StaticSamplerDescription>();

        foreach (var res in reflection.BoundResources)
        {
            if (res.Type == ShaderInputType.Texture)
            {
                parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                    DescriptorRangeType.ShaderResourceView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
            }
            else if (res.Type == ShaderInputType.ConstantBuffer)
            {
                parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                    DescriptorRangeType.ConstantBufferView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
            }
            else if (res.Type == ShaderInputType.Sampler)
            {
                if (renderStates.sampler.TryGetValue(res.BindPoint, out var desc))
                {
                    samplers.Add(new StaticSamplerDescription(desc.Filter, desc.AddressU, desc.AddressV, desc.AddressW, desc.MipLODBias,
                        desc.MaxAnisotropy, desc.ComparisonFunction, StaticBorderColor.TransparentBlack, desc.MinLOD, desc.MaxLOD, res.BindPoint, 0));
                }
                else
                {
                    samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                        0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                }
            }
        }

        return new RootSignatureDescription1()
        {
            Parameters = parameters.ToArray(),
            StaticSamplers = samplers.ToArray(),
            Flags = RootSignatureFlags.AllowInputAssemblerInputLayout,
        };
    }

    static InputElementDescription[] GetInputElementDescriptions(ID3D12ShaderReflection reflection)
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

    internal ID3D12Resource GetTexture(VariableSlot variableSlot)
    {
        return GetTexture(variableSlot.File);
    }
    internal ID3D12Resource GetTexture(string file)
    {
        var path = Path.GetFullPath(file);

        return LRUCache.GetObject(path, (key) =>
        {
            var image = Image.Load<Rgba32>(file);
            if (!image.Frames[0].DangerousTryGetSinglePixelMemory(out var memory))
            {
                throw new Exception();
            }
            var texture = device.CreateCommittedResource(HeapType.Default, ResourceDescription.Texture2D(Format.R8G8B8A8_UNorm, (uint)image.Width, (uint)image.Height), ResourceStates.CopyDest);
            int offset = uploadBuffer.UploadData(MemoryMarshal.AsBytes(memory.Span));

            var desc = texture.Description;

            PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
            footPrint.Footprint.Width = (int)desc.Width;
            footPrint.Footprint.Height = desc.Height;
            footPrint.Footprint.Depth = 1;
            footPrint.Footprint.RowPitch = ((int)desc.Width * 4 + 255) & ~255;
            footPrint.Footprint.Format = desc.Format;
            footPrint.Offset = (ulong)offset;

            TextureCopyLocation Dst = new TextureCopyLocation(texture, 0);
            TextureCopyLocation Src = new TextureCopyLocation(uploadBuffer.buffer, footPrint);

            commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
            commandList.ResourceBarrier(ResourceBarrier.BarrierTransition(texture, ResourceStates.CopyDest, ResourceStates.GenericRead));

            return texture;
        }) as ID3D12Resource;
    }
}
