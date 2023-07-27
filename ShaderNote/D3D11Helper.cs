using SharpGen.Runtime;
using Vortice.DXGI;

namespace ShaderNote;

internal static class D3D11Helper
{
    public static void ReleaseComPtr<T>(ref T comObject) where T : ComObject
    {
        comObject.Release();
        comObject = null;
    }

    public static Format GetResourceFormat(Format format)
    {
        switch (format)
        {
            case Format.D16_UNorm:
                return Format.R16_Typeless;
            case Format.D24_UNorm_S8_UInt:
                return Format.R24G8_Typeless;
            case Format.D32_Float:
                return Format.R32_Typeless;
            default:
                return format;
        }
    }
    public static Format GetSRVFormat(Format format)
    {
        switch (format)
        {
            case Format.R16_Typeless:
                return Format.R16_UNorm;
            case Format.R24G8_Typeless:
                return Format.R24_UNorm_X8_Typeless;
            case Format.R32_Typeless:
                return Format.R32_Float;
            default:
                return format;
        }
    }
}
