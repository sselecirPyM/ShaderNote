using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using ShaderResourceViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension;

namespace ShaderNoteD3D12;

internal class DrawIndexedInstances
{
    public int indexCountPerInstance;
    public int instanceCount;
    public int startIndexLocation;
    public int baseVertexLocation;
    public int startInstanceLocation;
}

internal class RenderStates
{
    public VariableSlot vertexShader1;
    public VariableSlot pixelShader1;
    public PrimitiveTopology primitiveTopology;
    public BlendDescription blendDescription = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);
    public DepthStencilDescription depthStencilDescription = DepthStencilDescription.Default;

    public Format[] formats;
    public Format depthFormat;

    public Dictionary<string, VariableSlot> SlotValue = new();

    public VariableSlot inputElementDescriptions;
    public InputElementDescription[] currentInputElements;

    public Dictionary<string, VariableSlot> vertexBuffers = new();
    public Dictionary<int, GpuDescriptorHandle> CBV = new();
    public Dictionary<int, GpuDescriptorHandle> SRV = new();
    public Dictionary<int, SamplerDescription> sampler = new();
    public Dictionary<RenderRecordItem, Texture2D> RenderTexture = new();
    public List<RenderResult> trash = new();
    public RootParameter1[] currentRootDescriptor = new RootParameter1[0];

    public bool pipelineChange;
    public bool vertexBufferChanged;

    public void Dispose()
    {
        foreach (RenderResult v in trash)
        {
            v.Dispose();
        }
    }
}
internal class ResultWrap
{
    public RenderResult renderResult;
    public int channel;
}

internal enum RenderAction
{
    None = 0,
    DrawIndexedInstances,
    DepthStencil,
    BlendState,
    VertexShader,
    PixelShader,
    PrimitiveTopology,
    InputLayout,
    Sampler,
    VertexBuffer,
    IndexBuffer,
    ConstantBuffer,
    RenderImage,
    Image
}
internal class RenderRecordItem
{
    public int offset;
    public int stride;
    public int[] strides;
    public int[] offsets;
    public string bindSlot;

    public RenderRecordItem PreviousRecord;

    public RenderAction renderAction;
    public VariableSlot commonSlot;

    internal void SetState(NoteDevice noteDevice, RenderStates renderStates)
    {
        var commandList = noteDevice.commandList;

        var commonSlot = this.commonSlot;

        if (commonSlot != null && commonSlot.SlotName != null && renderStates.SlotValue.TryGetValue(commonSlot.SlotName, out var replaceSlot))
        {
            commonSlot = replaceSlot;
        }

        switch (renderAction)
        {
            case RenderAction.DrawIndexedInstances:
                noteDevice.SetPipelineState(renderStates);

                var d = (DrawIndexedInstances)commonSlot.Value;
                commandList.DrawIndexedInstanced(d.indexCountPerInstance, d.instanceCount, d.startIndexLocation, d.baseVertexLocation, d.startInstanceLocation);
                renderStates.pipelineChange = false;
                break;
            case RenderAction.DepthStencil:
                renderStates.depthStencilDescription = (DepthStencilDescription)commonSlot.Value;
                renderStates.pipelineChange = true;
                break;
            case RenderAction.BlendState:
                renderStates.blendDescription = (BlendDescription)commonSlot.Value;
                break;
            case RenderAction.VertexShader:
                renderStates.vertexShader1 = commonSlot;
                renderStates.pipelineChange = true;
                break;
            case RenderAction.PixelShader:
                renderStates.pixelShader1 = commonSlot;
                renderStates.pipelineChange = true;
                break;
            case RenderAction.PrimitiveTopology:
                renderStates.primitiveTopology = (PrimitiveTopology)commonSlot.Value;
                commandList.IASetPrimitiveTopology(renderStates.primitiveTopology);
                break;
            case RenderAction.InputLayout:
                renderStates.inputElementDescriptions = commonSlot;
                renderStates.pipelineChange = true;
                break;
            case RenderAction.Sampler:
                renderStates.sampler[offset] = (SamplerDescription)commonSlot.Value;
                renderStates.pipelineChange = true;
                break;
            case RenderAction.VertexBuffer:
                {
                    renderStates.vertexBuffers[bindSlot] = commonSlot;
                    renderStates.vertexBufferChanged = true;

                    //ulong addr = noteDevice.GetBuffer(commonSlot);
                    //commandList.IASetVertexBuffers(offset, new VertexBufferView(addr, ((byte[])commonSlot.Value).Length, stride));
                }
                break;
            case RenderAction.IndexBuffer:
                {
                    ulong addr = noteDevice.GetBuffer(commonSlot);
                    commandList.IASetIndexBuffer(new IndexBufferView(addr, ((byte[])commonSlot.Value).Length, (Format)commonSlot.Value1));
                }
                break;
            case RenderAction.ConstantBuffer:
                {
                    renderStates.CBV[offset] = noteDevice.GetCBV(commonSlot);
                }
                break;
            case RenderAction.RenderImage:
                {
                    var tex = renderStates.RenderTexture[this];
                    var gpuHandle = CreateSRV(noteDevice, tex.resource);
                    renderStates.SRV[offset] = gpuHandle;
                    tex.StateTrans(noteDevice.commandList, ResourceStates.GenericRead);
                }
                break;
            case RenderAction.Image:
                {
                    var texture = noteDevice.GetTexture(commonSlot);
                    noteDevice.commandQueue.CommandRef(texture);
                    renderStates.SRV[offset] = CreateSRV(noteDevice, texture);
                }
                break;
            default:
                throw new System.Exception();
        }
    }

    internal void BeforeRender(NoteDevice noteDevice, RenderStates renderStates)
    {
        switch (renderAction)
        {
            case RenderAction.RenderImage:
                var wrap = (ResultWrap)commonSlot.Value;
                var renderResult = wrap.renderResult;
                if (!renderResult.rendered)
                    renderStates.trash.Add(renderResult);
                renderResult.CheckRender();

                if (wrap.channel == -1 && renderResult.depthTexture != null)
                {
                    var tex = renderResult.depthTexture;
                    noteDevice.commandQueue.CommandRef(tex.resource);

                    renderStates.RenderTexture.Add(this, tex);
                }
                if (wrap.channel >= 0)
                {
                    var tex = renderResult.texture2Ds[wrap.channel];
                    noteDevice.commandQueue.CommandRef(tex.resource);

                    renderStates.RenderTexture.Add(this, tex);
                }

                break;
        }
    }

    internal GpuDescriptorHandle CreateSRV(NoteDevice noteDevice, ID3D12Resource tex)
    {
        var texDesc = tex.Description;
        var desc = new ShaderResourceViewDescription()
        {
            Format = D3D12Helper.GetSRVFormat(texDesc.Format),
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = new Texture2DShaderResourceView()
            {
                MipLevels = 1,
            },
            Shader4ComponentMapping = 5768
        };
        noteDevice.srv.GetTempHandle(out var cpuHandle, out var gpuHandle);
        noteDevice.device.CreateShaderResourceView(tex, desc, cpuHandle);
        return gpuHandle;
    }
}
