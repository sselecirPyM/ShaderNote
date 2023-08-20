using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace ShaderNoteD3D12;

public record RenderRecord
{
    RenderRecordItem recordItem;

    internal NoteDevice noteDevice;

    int width = 256;
    int height = 256;

    Format[] outputFormats;
    Format depthStencilFormt;

    Vector4 ClearColor = Vector4.Zero;
    //Vector4 ClearColor = Vector4.One;
    //Vector4 ClearColor = new Vector4(0, 0, 1, 1);

    internal RenderRecord()
    {

    }


    public RenderRecord WithImage(int slot, string file = null, string name = null, bool argument = false)
    {
        string shortCut = null;
        if (file != null)
            shortCut ??= Path.GetFullPath(file);

        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.Image,
            commonSlot = new VariableSlot()
            {
                File = (file == null) ? null : shortCut,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            offset = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithImage(int slot, RenderResult renderResult, int channel = -1, string name = null, bool argument = false)
    {
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.RenderImage,
            commonSlot = new VariableSlot()
            {
                Value = new ResultWrap()
                {
                    renderResult = renderResult,
                    channel = channel,
                },
                SlotName = name,
                AsArgument = argument,
            },
            offset = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithSampler(int slot, Filter filter = Filter.MinMagMipLinear, TextureAddressMode u = TextureAddressMode.Wrap,
        TextureAddressMode v = TextureAddressMode.Wrap, TextureAddressMode w = TextureAddressMode.Wrap,
        float mipLODBias = 0, int maxAnisotropy = 1, ComparisonFunction comparisonFunc = ComparisonFunction.Never,
        float minLOD = float.MinValue, float maxLOD = float.MaxValue, Vector4 borderColor = default, string name = null, bool argument = false)
    {
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.Sampler,
            commonSlot = new VariableSlot()
            {
                Value = new SamplerDescription(filter, u, v, w, mipLODBias, maxAnisotropy, comparisonFunc, new Vortice.Mathematics.Color4(borderColor), minLOD, maxLOD),
                SlotName = name,
                AsArgument = argument,
            },
            offset = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithVertexBuffer<T>(string slot = "POSITION0", int stride = 0, T[] data = null, string name = null, bool argument = false) where T : unmanaged
    {
        byte[] bytes = MemoryMarshal.AsBytes(data.AsSpan()).ToArray();

        string shortCut = null;
        if (bytes != null)
            shortCut ??= GetHashShortCut(bytes);

        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.VertexBuffer,
            commonSlot = new VariableSlot()
            {
                Value = bytes,
                Value1 = stride,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            stride = stride,
            bindSlot = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithIndexBuffer(int byteWidth = 0, string file = null, object data = null, string name = null, bool argument = false)
    {
        byte[] data1 = null;
        if (data != null)
        {
            switch (data)
            {
                case byte[] bytes:
                    data1 = bytes.AsSpan().ToArray();
                    if (byteWidth == 0)
                        byteWidth = 1;
                    break;
                case ushort[] ushorts:
                    data1 = MemoryMarshal.AsBytes(ushorts.AsSpan()).ToArray();
                    if (byteWidth == 0)
                        byteWidth = 2;
                    break;
                case int[] ints:
                    data1 = MemoryMarshal.AsBytes(ints.AsSpan()).ToArray();
                    if (byteWidth == 0)
                        byteWidth = 4;
                    break;
            }
        }
        string shortCut = null;
        if (data1 != null)
            shortCut ??= GetHashShortCut(data1);
        if (file != null)
            shortCut ??= Path.GetFullPath(file);

        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.IndexBuffer,
            commonSlot = new VariableSlot()
            {
                File = (file == null) ? null : shortCut,
                Value = data1,
                Value1 = byteWidth switch
                {
                    1 => Format.R8_UInt,
                    2 => Format.R16_UInt,
                    4 => Format.R32_UInt,
                    _ => Format.Unknown
                },
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithConstantBuffer(int slot, string file, string name = null, bool argument = false)
    {
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.ConstantBuffer,
            commonSlot = new VariableSlot()
            {
                File = (file == null) ? null : Path.GetFullPath(file),
                ShortCut = (file == null) ? null : Path.GetFullPath(file),
                SlotName = name,
                AsArgument = argument,
            },
            offset = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithConstantBuffer<T>(int slot, T[] data, string name = null, bool argument = false) where T : unmanaged
    {
        byte[] bytes = MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
        string shortCut = GetHashShortCut(bytes);
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.ConstantBuffer,
            commonSlot = new VariableSlot()
            {
                Value = bytes,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            offset = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithConstantBuffer<T>(int slot, T data = default, string name = null, bool argument = false) where T : unmanaged
    {
        Span<T> array = stackalloc T[1] { data };
        byte[] bytes = MemoryMarshal.AsBytes(array).ToArray();
        string shortCut = GetHashShortCut(bytes);
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.ConstantBuffer,
            commonSlot = new VariableSlot()
            {
                Value = bytes,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            offset = slot,
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithInputLayout(InputElementDescription[] inputElementDescriptions, string name = null, bool argument = false)
    {
        string shortCut = (inputElementDescriptions == null) ? null : ObjectShortCut(inputElementDescriptions);
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.InputLayout,
            commonSlot = new VariableSlot()
            {
                Value = inputElementDescriptions,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithPrimitiveTopology(PrimitiveTopology primitiveTopology, string name = null, bool argument = false)
    {
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.PrimitiveTopology,
            commonSlot = new VariableSlot()
            {
                Value = primitiveTopology,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithVertexShader(string file = null, string source = null, string sourcePath = null, string entryPoint = "main", string name = null, bool argument = false)
    {
        string shortCut = null;
        if (file != null)
            shortCut = Path.GetFullPath(file);
        if (source != null)
            shortCut ??= GetHashShortCut(source);

        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.VertexShader,
            commonSlot = new VariableSlot()
            {
                File = (file == null) ? null : shortCut,
                Value = source,
                Value1 = sourcePath,
                EntryPoint = entryPoint,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithPixelShader(string file = null, string source = null, string sourcePath = null, string entryPoint = "main", string name = null, bool argument = false)
    {
        string shortCut = null;
        if (file != null)
            shortCut = Path.GetFullPath(file);
        if (source != null)
            shortCut ??= GetHashShortCut(source);

        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.PixelShader,
            commonSlot = new VariableSlot()
            {
                File = (file == null) ? null : shortCut,
                Value = source,
                Value1 = sourcePath,
                EntryPoint = entryPoint,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithDrawIndexed(int indexCountPerInstance, int instanceCount = 1, int startIndexLocation = 0, int baseVertexLocation = 0, int startInstanceLocation = 0, string name = null, bool argument = false)
    {
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.DrawIndexedInstances,
            commonSlot = new VariableSlot()
            {
                Value = new DrawIndexedInstances()
                {
                    baseVertexLocation = baseVertexLocation,
                    indexCountPerInstance = indexCountPerInstance,
                    instanceCount = instanceCount,
                    startIndexLocation = startIndexLocation,
                    startInstanceLocation = startInstanceLocation
                },
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithMRT(params Format[] formats)
    {
        return this with { outputFormats = formats.AsSpan().ToArray(), };
    }

    public RenderRecord WithDepth(Format format = Format.D24_UNorm_S8_UInt)
    {
        return this with { depthStencilFormt = format, };
    }

    public RenderRecord WithSize(int width, int height)
    {
        return this with { width = width, height = height };
    }

    public RenderRecord WithClearColor(Vector4 color)
    {
        return this with { ClearColor = color };
    }

    public RenderRecord WithBlendState(BlendDescription blendDescription, string name = null, bool argument = false)
    {
        string shortCut = "blend_state_" + blendDescription.GetHashCode().ToString();
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.BlendState,
            commonSlot = new VariableSlot()
            {
                Value = blendDescription,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderRecord WithDepthStencilState(DepthStencilDescription depthStencilDescription, string name = null, bool argument = false)
    {
        string shortCut = "depth_stencil_" + depthStencilDescription.GetHashCode().ToString();
        var recordItem = new RenderRecordItem()
        {
            renderAction = RenderAction.DepthStencil,
            commonSlot = new VariableSlot()
            {
                Value = depthStencilDescription,
                ShortCut = shortCut,
                SlotName = name,
                AsArgument = argument,
            },
            PreviousRecord = this.recordItem
        };
        return this with { recordItem = recordItem };
    }

    public RenderResult Render()
    {
        return new RenderResult()
        {
            noteDevice = noteDevice,
            renderRecord = this,
        };
    }

    internal void RenderTo(out Texture2D[] texture2Ds, out Texture2D depth)
    {
        var renderStates = new RenderStates();
        var renderRecordItems = GetRenderList();
        BeforeRenderList(renderRecordItems, renderStates);

        noteDevice.Begin();
        var commandList = noteDevice.commandList;

        Format[] outputFormats1 = outputFormats ?? new Format[1] { Format.R8G8B8A8_UNorm };
        renderStates.formats = outputFormats1;
        ID3D12Resource[] texture2Ds1 = new ID3D12Resource[outputFormats1.Length];
        CpuDescriptorHandle[] rtvs = new CpuDescriptorHandle[outputFormats1.Length];
        for (int i = 0; i < outputFormats1.Length; i++)
        {
            CreateRTV(outputFormats1[i], out texture2Ds1[i], out rtvs[i]);
            commandList.ClearRenderTargetView(rtvs[i], new Vortice.Mathematics.Color4(ClearColor));
        }
        CpuDescriptorHandle dsv = default;
        ID3D12Resource depth1 = null;
        if (depthStencilFormt != Format.Unknown)
        {
            renderStates.depthFormat = depthStencilFormt;
            CreateDepth(depthStencilFormt, out depth1, out dsv);
            commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
        }


        //commandList.ClearState(null);
        commandList.RSSetViewport(0, 0, width, height);
        commandList.RSSetScissorRect(new Vortice.RawRect(0, 0, width, height));
        if (dsv != default)
            commandList.OMSetRenderTargets(rtvs, dsv);
        else
            commandList.OMSetRenderTargets(rtvs);

        RenderList(renderRecordItems, renderStates);

        commandList.OMSetRenderTargets(null);
        texture2Ds = new Texture2D[texture2Ds1.Length];
        for (int i = 0; i < texture2Ds1.Length; i++)
            texture2Ds[i] = new Texture2D { resource = texture2Ds1[i], resourceState = ResourceStates.RenderTarget };
        depth = (depth1 == null) ? null : new Texture2D() { resource = depth1, resourceState = ResourceStates.DepthWrite };


        renderStates.Dispose();
        noteDevice.Execute();
    }

    void CreateRTV(Format format, out ID3D12Resource texture, out CpuDescriptorHandle srv)
    {
        texture = noteDevice.device.CreateCommittedResource(HeapType.Default, new ResourceDescription()
        {
            Width = (uint)width,
            Height = height,
            Dimension = ResourceDimension.Texture2D,
            Format = format,
            DepthOrArraySize = 1,
            Flags = ResourceFlags.AllowRenderTarget,
            MipLevels = 1,
            SampleDescription = SampleDescription.Default
        }, ResourceStates.RenderTarget);
        srv = noteDevice.rtv.GetTempCpuHandle();
        noteDevice.device.CreateRenderTargetView(texture, null, srv);
    }

    void CreateDepth(Format format, out ID3D12Resource texture, out CpuDescriptorHandle dsv)
    {
        texture = noteDevice.device.CreateCommittedResource(HeapType.Default, new ResourceDescription()
        {
            Width = (uint)width,
            Height = height,
            Dimension = ResourceDimension.Texture2D,
            Format = D3D12Helper.GetResourceFormat(format),
            DepthOrArraySize = 1,
            Flags = ResourceFlags.AllowDepthStencil,
            MipLevels = 1,
            SampleDescription = SampleDescription.Default
        }, ResourceStates.DepthWrite);
        dsv = noteDevice.dsv.GetTempCpuHandle();
        noteDevice.device.CreateDepthStencilView(texture, new DepthStencilViewDescription()
        {
            ViewDimension = DepthStencilViewDimension.Texture2D,
            Format = format,
        }, dsv);
    }

    internal List<RenderRecordItem> GetRenderList()
    {
        var renderRecordItems = new List<RenderRecordItem>();
        var current = recordItem;
        while (current != null)
        {
            renderRecordItems.Add(current);
            current = current.PreviousRecord;
        }
        renderRecordItems.Reverse();
        return renderRecordItems;
    }

    void BeforeRenderList(List<RenderRecordItem> renderRecordItems, RenderStates renderStates)
    {
        for (int i = 0; i < renderRecordItems.Count; i++)
        {
            var cs = renderRecordItems[i].commonSlot;
            if (cs != null && cs.AsArgument && !string.IsNullOrEmpty(cs.SlotName))
            {
                renderStates.SlotValue[cs.SlotName] = cs;
            }
        }
        for (int i = 0; i < renderRecordItems.Count; i++)
        {
            var renderRecord = renderRecordItems[i];
            renderRecord.BeforeRender(noteDevice, renderStates);
        }
    }

    void RenderList(List<RenderRecordItem> renderRecordItems, RenderStates renderStates)
    {
        for (int i = 0; i < renderRecordItems.Count; i++)
        {
            var renderRecord = renderRecordItems[i];
            var cs = renderRecordItems[i].commonSlot;
            if (cs != null && cs.AsArgument)
            {
                continue;
            }
            renderRecord.SetState(noteDevice, renderStates);
        }
    }

    public void Save(string path, int index = 0)
    {
        var result = Render();

        result.Save(path, index);
        result.Dispose();
    }

    static string ObjectShortCut<T>(T[] source)
    {
        Span<int> hashCodes = stackalloc int[source.Length];
        for (int i = 0; i < source.Length; i++)
            hashCodes[i] = source[i].GetHashCode();

        Span<byte> buffer = stackalloc byte[32];
        SHA256.TryHashData(MemoryMarshal.AsBytes(hashCodes), buffer, out int bytesWritten);
        return new Guid(buffer.Slice(0, 16)).ToString();
    }

    static string GetHashShortCut(string source)
    {
        Span<byte> buffer = stackalloc byte[32];
        SHA256.TryHashData(Encoding.UTF8.GetBytes(source), buffer, out int bytesWritten);
        return new Guid(buffer.Slice(0, 16)).ToString();
    }

    static string GetHashShortCut<T>(T[] source) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[32];
        SHA256.TryHashData(MemoryMarshal.AsBytes(source.AsSpan()), buffer, out int bytesWritten);
        return new Guid(buffer.Slice(0, 16)).ToString();
    }
}
