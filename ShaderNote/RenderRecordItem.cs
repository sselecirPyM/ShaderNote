using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ShaderNote;

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
    public PrimitiveTopology primitiveTopology;

    public Dictionary<string, VariableSlot> SlotValue = new();

    public bool inputLayoutChanged;
    public bool vertexBufferChanged;
    public VariableSlot inputElementDescriptions;
    public InputElementDescription[] currentInputElements;

    public Dictionary<string, VariableSlot> vertexBuffers = new();

    public Dictionary<RenderRecordItem, ID3D11ShaderResourceView> TemporaryTexture = new();
    public List<RenderResult> trash = new();

    public void Dispose()
    {
        foreach (var value in TemporaryTexture)
        {
            value.Value.Release();
        }
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
        var deviceContext = noteDevice.deviceContext;

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
                deviceContext.DrawIndexedInstanced(d.indexCountPerInstance, d.instanceCount, d.startIndexLocation, d.baseVertexLocation, d.startInstanceLocation);
                break;
            case RenderAction.DepthStencil:
                deviceContext.OMSetDepthStencilState(noteDevice.GetDepthStencilState(commonSlot));
                break;
            case RenderAction.BlendState:
                deviceContext.OMSetBlendState(noteDevice.GetBlendState(commonSlot));
                break;
            case RenderAction.VertexShader:
                deviceContext.VSSetShader(noteDevice.GetVertexShader(commonSlot));
                renderStates.vertexShader1 = commonSlot;
                renderStates.inputLayoutChanged = true;
                break;
            case RenderAction.PixelShader:
                deviceContext.PSSetShader(noteDevice.GetPixelShader(commonSlot));
                break;
            case RenderAction.PrimitiveTopology:
                renderStates.primitiveTopology = (PrimitiveTopology)commonSlot.Value;
                deviceContext.IASetPrimitiveTopology(renderStates.primitiveTopology);
                break;
            case RenderAction.InputLayout:
                renderStates.inputElementDescriptions = commonSlot;
                renderStates.inputLayoutChanged = true;
                break;
            case RenderAction.Sampler:
                deviceContext.PSSetSampler(offset, noteDevice.GetSampler(commonSlot));
                break;
            case RenderAction.VertexBuffer:
                renderStates.vertexBuffers[bindSlot] = commonSlot;
                renderStates.vertexBufferChanged = true;
                break;
            case RenderAction.IndexBuffer:
                deviceContext.IASetIndexBuffer(noteDevice.GetBuffer(commonSlot, BindFlags.IndexBuffer), (Format)commonSlot.Value1, offset);
                break;
            case RenderAction.ConstantBuffer:
                var constnatBuffer = noteDevice.GetBuffer(commonSlot, BindFlags.ConstantBuffer);
                deviceContext.VSSetConstantBuffer(offset, constnatBuffer);
                deviceContext.PSSetConstantBuffer(offset, constnatBuffer);
                break;
            case RenderAction.RenderImage:
                if (renderStates.TemporaryTexture.TryGetValue(this, out var texture))
                {
                    deviceContext.PSSetShaderResource(offset, texture);
                }
                break;
            case RenderAction.Image:
                deviceContext.PSSetShaderResource(offset, noteDevice.GetImage(commonSlot.File));
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
                    var srv = CreateSRV(noteDevice, tex);
                    renderStates.TemporaryTexture.Add(this, srv);
                }
                if (wrap.channel >= 0)
                {
                    var tex = renderResult.texture2Ds[wrap.channel];
                    var srv = CreateSRV(noteDevice, tex);
                    renderStates.TemporaryTexture.Add(this, srv);
                }

                break;
        }
    }

    internal ID3D11ShaderResourceView CreateSRV(NoteDevice noteDevice, ID3D11Texture2D tex)
    {
        var desc = new ShaderResourceViewDescription()
        {
            Format = D3D11Helper.GetSRVFormat(tex.Description.Format),
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = new Texture2DShaderResourceView()
            {
                MipLevels = 1,
            }
        };
        return noteDevice.device.CreateShaderResourceView(tex, desc);
    }

}
