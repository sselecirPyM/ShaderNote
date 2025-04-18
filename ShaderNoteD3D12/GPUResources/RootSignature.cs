using System;
using System.Collections.Generic;
using Vortice.Direct3D12;

namespace ShaderNoteD3D12.GPUResources
{
    internal class RootSignature : IDisposable
    {
        internal ID3D12RootSignature rootSignature;
        public string Name;

        internal RootSignatureDescription1 description1;
        internal Dictionary<int, int> cbv = new Dictionary<int, int>();
        internal Dictionary<int, int> srv = new Dictionary<int, int>();
        internal Dictionary<int, int> uav = new Dictionary<int, int>();


        public void Dispose()
        {
            rootSignature?.Release();
            rootSignature = null;
        }
    }
}
