using System;
using Vortice.Direct3D12;

namespace ShaderNoteD3D12;

internal class Texture2D:IDisposable
{
    public ID3D12Resource resource;
    public ResourceStates resourceState;

    public void StateTrans(ID3D12GraphicsCommandList commandList, ResourceStates target)
    {
        if (resourceState != target)
        {
            commandList.ResourceBarrier(ResourceBarrier.BarrierTransition(resource, resourceState, target));
            resourceState = target;
        }
    }

    public void Dispose()
    {
        resource?.Release();
        resource = null;
    }
}
