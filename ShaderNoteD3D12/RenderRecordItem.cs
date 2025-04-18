using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

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

    public InputElementDescription[] inputElementDescriptions;
    public InputElementDescription[] currentInputElements;

    public Dictionary<string, Action<int>> vertexBuffers = new();
    public Dictionary<int, SamplerDescription> sampler = new();
    public Dictionary<RenderRecordItem, Texture2D> RenderTexture = new();
    public List<RenderResult> trash = new();

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
    public RenderRecordItem PreviousRecord;

    public VariableSlot commonSlot;

    internal void SetState(NoteDevice noteDevice, RenderStates renderStates)
    {
        var commandList = noteDevice.commandList;

        var commonSlot = this.commonSlot;

        commonSlot.Call(noteDevice, renderStates);
    }

    internal void BeforeRender(NoteDevice noteDevice, RenderStates renderStates)
    {
        commonSlot.BeforeRenderCall?.Invoke(noteDevice, renderStates);
    }
}
