using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using ShaderResourceViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension;

namespace ShaderNoteD3D12;

internal class RenderStates
{
    public ShaderInfo vertexShader1;
    public ShaderInfo pixelShader1;
    public PrimitiveTopology primitiveTopology;
    public BlendDescription blendDescription = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);
    public DepthStencilDescription depthStencilDescription = DepthStencilDescription.Default;

    public Format[] formats;
    public Format depthFormat;

    public Dictionary<string, VariableSlot> SlotValue = new();

    public InputElementDescription[] inputElementDescriptions;
    public InputElementDescription[] currentInputElements;

    public Dictionary<string, Action<int>> vertexBuffers = new();
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

internal class ShaderInfo
{
    public string source;
    public string file;
    public string sourcePath;
    public string entryPoint;
}
internal class RenderRecordItem
{
    public string bindSlot;

    public RenderRecordItem PreviousRecord;

    public VariableSlot commonSlot;

    internal void SetState(NoteDevice noteDevice, RenderStates renderStates)
    {
        var commandList = noteDevice.commandList;

        var commonSlot = this.commonSlot;

        if (commonSlot != null && commonSlot.SlotName != null && renderStates.SlotValue.TryGetValue(commonSlot.SlotName, out var replaceSlot))
        {
            commonSlot = replaceSlot;
        }

        commonSlot.Call(noteDevice, renderStates);
    }

    internal void BeforeRender(NoteDevice noteDevice, RenderStates renderStates)
    {
        commonSlot.BeforeRenderCall?.Invoke(noteDevice, renderStates);
    }

    internal static GpuDescriptorHandle CreateSRV(NoteDevice noteDevice, ID3D12Resource tex)
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
