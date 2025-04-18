using Vortice.Direct3D12;

namespace ShaderNoteD3D12.Commanding
{
    internal static class ResouceExt
    {
        internal static GpuDescriptorHandle CreateSRV(this NoteDevice noteDevice, ID3D12Resource tex)
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
}
