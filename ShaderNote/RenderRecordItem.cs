﻿using System.Collections.Generic;
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
internal class RenderRecordItem
{

    internal class RenderStates
    {
        public VariableSlot vertexShader1;
        public PrimitiveTopology primitiveTopology;

        public Dictionary<string, VariableSlot> SlotValue = new();

        public bool setInputLayout;
        public VariableSlot inputElementDescriptions;
    }

    public int offset;
    public int stride;
    public int[] strides;
    public int[] offsets;

    public RenderRecordItem PreviousRecord;

    public string caseName;
    public VariableSlot commonSlot;

    public void RunStateChain(NoteDevice noteDevice)
    {
        RunStateChain1(noteDevice, new RenderStates());
    }

    void RunStateChain1(NoteDevice noteDevice, RenderStates renderStates)
    {
        if (commonSlot != null && commonSlot.AsArgument && !string.IsNullOrEmpty(commonSlot.SlotName))
        {
            renderStates.SlotValue[commonSlot.SlotName] = commonSlot;
        }
        if (PreviousRecord != null)
        {
            PreviousRecord.RunStateChain1(noteDevice, renderStates);
        }
        if (commonSlot != null && commonSlot.AsArgument)
        {
            return;
        }
        SetState(noteDevice, renderStates);
    }

    void SetState(NoteDevice noteDevice, RenderStates renderStates)
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

}
