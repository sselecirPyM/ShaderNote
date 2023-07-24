using SharpGen.Runtime;

namespace ShaderNote;

internal static class D3D11Helper
{
    public static void ReleaseComPtr<T>(ref T comObject) where T : ComObject
    {
        comObject.Release();
        comObject = null;
    }
}
