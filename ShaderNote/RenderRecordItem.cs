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

    public bool setInputLayout;
    public VariableSlot inputElementDescriptions;

    public Dictionary<int, ID3D11ShaderResourceView> TemporaryTexture = new();
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

internal class RenderRecordItem
{
    public int offset;
    public int stride;
    public int[] strides;
    public int[] offsets;

    public RenderRecordItem PreviousRecord;

    public string caseName;
    public VariableSlot commonSlot;

    internal List<RenderRecordItem> GetList()
    {
        var renderRecordItems = new List<RenderRecordItem>();
        var current = this;
        while (current != null)
        {
            renderRecordItems.Add(current);
            current = current.PreviousRecord;
        }
        renderRecordItems.Reverse();
        return renderRecordItems;
    }

    internal void SetState(NoteDevice noteDevice, RenderStates renderStates)
    {
        var deviceContext = noteDevice.deviceContext;

        var commonSlot = this.commonSlot;

        if (commonSlot != null && commonSlot.SlotName != null && renderStates.SlotValue.TryGetValue(commonSlot.SlotName, out var replaceSlot))
        {
            commonSlot = replaceSlot;
        }

        switch (caseName)
        {
            case "DrawIndexedInstances":
                if (!renderStates.setInputLayout)
                {
                    deviceContext.IASetInputLayout(noteDevice.GetInputLayout(renderStates.inputElementDescriptions, renderStates.vertexShader1));
                    renderStates.setInputLayout = true;
                }
                if (renderStates.primitiveTopology == PrimitiveTopology.Undefined)
                {
                    renderStates.primitiveTopology = PrimitiveTopology.TriangleList;
                    deviceContext.IASetPrimitiveTopology(renderStates.primitiveTopology);
                }

                var d = (DrawIndexedInstances)commonSlot.Value;
                deviceContext.DrawIndexedInstanced(d.indexCountPerInstance, d.instanceCount, d.startIndexLocation, d.baseVertexLocation, d.startInstanceLocation);
                break;
            case "DepthStencil":
                deviceContext.OMSetDepthStencilState(noteDevice.GetDepthStencilState(commonSlot));
                break;
            case "BlendState":
                deviceContext.OMSetBlendState(noteDevice.GetBlendState(commonSlot));
                break;
            case "VertexShader":
                deviceContext.VSSetShader(noteDevice.GetVertexShader(commonSlot));
                renderStates.vertexShader1 = commonSlot;
                break;
            case "PixelShader":
                deviceContext.PSSetShader(noteDevice.GetPixelShader(commonSlot));
                break;
            case "PrimitiveTopology":
                renderStates.primitiveTopology = (PrimitiveTopology)commonSlot.Value;
                deviceContext.IASetPrimitiveTopology(renderStates.primitiveTopology);
                break;
            case "InputLayout":
                renderStates.setInputLayout = false;
                renderStates.inputElementDescriptions = commonSlot;
                break;
            case "Sampler":
                deviceContext.PSSetSampler(offset, noteDevice.GetSampler(commonSlot));
                break;
            case "VertexBuffer":
                deviceContext.IASetVertexBuffer(offset, noteDevice.GetBuffer(commonSlot, BindFlags.VertexBuffer), stride, 0);
                break;
            case "IndexBuffer":
                deviceContext.IASetIndexBuffer(noteDevice.GetBuffer(commonSlot, BindFlags.IndexBuffer), (Format)commonSlot.Value1, offset);
                break;
            case "ConstantBuffer":
                var constnatBuffer = noteDevice.GetBuffer(commonSlot, BindFlags.ConstantBuffer);
                deviceContext.VSSetConstantBuffer(offset, constnatBuffer);
                deviceContext.PSSetConstantBuffer(offset, constnatBuffer);
                break;
            case "RenderImage":
                if (renderStates.TemporaryTexture.TryGetValue(offset, out var texture))
                {
                    deviceContext.PSSetShaderResource(offset, texture);
                }
                break;
            case "Image":
                deviceContext.PSSetShaderResource(offset, noteDevice.GetImage(commonSlot.File));
                break;
            case "":
            case null:
                break;
            default:
                throw new System.Exception();
        }
    }

    internal void BeforeRender(NoteDevice noteDevice, RenderStates renderStates)
    {
        switch (caseName)
        {
            case "RenderImage":
                var wrap = (ResultWrap)commonSlot.Value;
                var renderResult = wrap.renderResult;
                if (!renderResult.rendered)
                    renderStates.trash.Add(renderResult);
                renderResult.CheckRender();

                if (wrap.channel == -1 && renderResult.depthTexture != null)
                {
                    var tex = renderResult.depthTexture;
                    var texDesc = tex.Description;
                    var desc = new ShaderResourceViewDescription()
                    {
                        Format = D3D11Helper.GetSRVFormat(texDesc.Format),
                        ViewDimension = ShaderResourceViewDimension.Texture2D,
                        Texture2D = new Texture2DShaderResourceView()
                        {
                            MipLevels = 1,
                        }
                    };
                    var srv = noteDevice.device.CreateShaderResourceView(tex, desc);
                    renderStates.TemporaryTexture.Add(offset, srv);
                }
                if (wrap.channel >= 0)
                {
                    var tex = renderResult.texture2Ds[wrap.channel];
                    var texDesc = tex.Description;
                    var desc = new ShaderResourceViewDescription()
                    {
                        Format = D3D11Helper.GetSRVFormat(texDesc.Format),
                        ViewDimension = ShaderResourceViewDimension.Texture2D,
                        Texture2D = new Texture2DShaderResourceView()
                        {
                            MipLevels = 1,
                        }
                    };
                    var srv = noteDevice.device.CreateShaderResourceView(tex, desc);
                    renderStates.TemporaryTexture.Add(offset, srv);
                }

                break;
        }
    }

}
