using ShaderNoteD3D12.Commanding;
using ShaderNoteD3D12.GPUResources;
using SharpGen.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
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
    internal CommandQueue copyCommandQueue;

    internal DescriptorHeapX srv;
    internal DescriptorHeapX rtv;
    internal DescriptorHeapX dsv;
    //internal DescriptorHeapX sampler;

    internal RingBuffer superRingBuffer = new RingBuffer();
    internal DynamicBuffer uploadBuffer;
    internal DynamicBuffer readBackBuffer;

    internal FastBufferAllocator fastBufferAllocator;
    internal FastBufferAllocator fastBufferAllocatorUAV;

    internal PSO currentPSO;

    Action delayBinding;

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
        copyCommandQueue = new CommandQueue();
        copyCommandQueue.Initialize(device, CommandListType.Copy);
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
        
        superRingBuffer.Initialize(this.device, 1048576 * 32, copyCommandQueue);
        fastBufferAllocator = new FastBufferAllocator(superRingBuffer, srv, ResourceFlags.None, commandQueue);
        fastBufferAllocatorUAV = new FastBufferAllocator(superRingBuffer, srv, ResourceFlags.AllowUnorderedAccess, commandQueue);

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
        superRingBuffer.FrameBegin();
        commandQueue.GetCommandList();
        commandList.Reset(commandQueue.GetCommandAllocator());
        commandList.SetDescriptorHeaps(srv.heap);
    }

    internal void Execute()
    {
        superRingBuffer.FrameEnd();
        fastBufferAllocator.FrameEnd();
        fastBufferAllocatorUAV.FrameEnd();
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

        superRingBuffer?.Dispose();
        fastBufferAllocator?.Dispose();
        fastBufferAllocatorUAV?.Dispose();

        uploadBuffer?.Dispose();
        readBackBuffer?.Dispose();

        currentPSO?.Dispose();

        srv?.Dispose();
        rtv?.Dispose();
        dsv?.Dispose();

        copyCommandQueue.Dispose();
        commandQueue.Dispose();
        device.Dispose();

        foreach(var obj in LRUCache.Values)
        {
            if(obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        device = null;
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

    internal static byte[] GetShader(ShaderInfo shaderInfo, DxcShaderStage shaderStage, out ID3D12ShaderReflection reflection)
    {
        if (shaderInfo == null)
        {
            reflection = null;
            return null;
        }
        using var result = DxcCompiler.Compile(shaderStage, File.ReadAllText(shaderInfo.file), shaderInfo.entryPoint, fileName: shaderInfo.sourcePath);
        reflection = DxcCompiler.Utils.CreateReflection<ID3D12ShaderReflection>(result.GetOutput(DxcOutKind.Reflection));
        return result.GetObjectBytecodeArray();
    }

    internal void BindResources(RenderStates renderStates)
    {
        if (renderStates.vertexBufferChanged && renderStates.currentInputElements != null)
        {
            for (int i = 0; i < renderStates.currentInputElements.Length; i++)
            {
                InputElementDescription element = renderStates.currentInputElements[i];
                int slot = i;
                if (element.Slot != -1)
                    slot = element.Slot;

                if (renderStates.vertexBuffers.TryGetValue(element.SemanticName + element.SemanticIndex, out var variable))
                {
                    variable.Invoke(slot);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Vertext Buffer '{0}' not found.", element.SemanticName + element.SemanticIndex));
                }
            }
            renderStates.vertexBufferChanged = false;
        }
        delayBinding?.Invoke();
        delayBinding = null;
    }

    public void SetGraphicsResources(Action<GraphicsCommandProxy> setResources)
    {
        delayBinding += () =>
        {
            var graphicsResourceProxy = new GraphicsCommandProxy();
            graphicsResourceProxy.noteDevice = this;
            graphicsResourceProxy.cbvs = currentPSO.rootSignature.cbv;
            graphicsResourceProxy.srvs = currentPSO.rootSignature.srv;
            graphicsResourceProxy.uavs = currentPSO.rootSignature.uav;
            setResources(graphicsResourceProxy);
        };
    }

    internal void SetPipelineState(RenderStates renderStates)
    {
        if (renderStates.pipelineChange)
        {
            currentPSO?.Dispose();
            currentPSO = null;

            var vs = GetShader(renderStates.vertexShader1, DxcShaderStage.Vertex, out var vsReflection);
            var ps = GetShader(renderStates.pixelShader1, DxcShaderStage.Pixel, out var psReflection);
            using var _1 = vsReflection;
            using var _2 = psReflection;

            var inputElements = renderStates.inputElementDescriptions ?? GetInputElementDescriptions(vsReflection);
            renderStates.currentInputElements = inputElements;

            PSO pso = new PSO();
            currentPSO = pso;
            pso.CreateRootSignature(this, vsReflection, psReflection);

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
                    Elements = inputElements
                },
                RootSignature = pso.rootSignature.rootSignature,
                RenderTargetFormats = renderStates.formats,
                BlendState = renderStates.blendDescription,
                DepthStencilState = renderStates.depthStencilDescription,
                DepthStencilFormat = renderStates.depthFormat,
            });
            pso.pipelineState = pipelineState;

            commandList.SetGraphicsRootSignature(pso.rootSignature.rootSignature);
            commandList.SetPipelineState(pso.pipelineState);
            commandQueue.CommandRef(pso.rootSignature.rootSignature);
            commandQueue.CommandRef(pipelineState);
            renderStates.pipelineChange = false;
        }
        if (renderStates.primitiveTopology == PrimitiveTopology.Undefined)
        {
            renderStates.primitiveTopology = PrimitiveTopology.TriangleList;
            commandList.IASetPrimitiveTopology(renderStates.primitiveTopology);
        }
        BindResources(renderStates);
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
                    if (item.MinPrecision == MinPrecision.MinPrecisionFloat16)
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R16G16B16A16_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R16G16B16A16_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R16G16_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R16_Float;
                    }
                    else
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
                }
                else if (item.ComponentType == RegisterComponentType.UInt32)
                {
                    if (item.MinPrecision == MinPrecision.MinPrecisionUInt16)
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R16G16B16A16_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R16G16B16A16_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R16G16_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R16_UInt;
                    }
                    else
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R32G32B32A32_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R32G32B32_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R32G32_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R32_UInt;
                    }
                }
                descs[count] = new InputElementDescription(item.SemanticName, item.SemanticIndex, format, count);
                count++;
            }
        }
        return descs;
    }

    internal Texture2D GetTexture(string file)
    {
        var path = Path.GetFullPath(file);
        var resource = LRUCache.GetObject(path, (key) =>
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
        return new Texture2D()
        {
            resource = resource,
        };
    }
}
