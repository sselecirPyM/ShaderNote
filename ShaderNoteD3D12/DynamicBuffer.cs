using System;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace ShaderNoteD3D12;


public class DynamicBuffer : IDisposable
{
    public void CreateReadBackBuffer(ID3D12Device device, int size)
    {
        this.buffer?.Release();
        this.buffer = device.CreateCommittedResource(
             new HeapProperties(HeapType.Readback),
             HeapFlags.None,
             ResourceDescription.Buffer(size), ResourceStates.CopyDest);
        this.size = size;
        this.buffer.Name = "buffer readback";
    }

    public void CreateUploadBuffer(ID3D12Device device, int size)
    {
        this.buffer?.Release();
        this.buffer = device.CreateCommittedResource(
             new HeapProperties(HeapType.Upload),
             HeapFlags.None,
             ResourceDescription.Buffer(size), ResourceStates.GenericRead);
        this.size = size;
        this.buffer.Name = "buffer upload";
    }

    unsafe public static void memcpy<T>(Span<T> t2, void* p1, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        new Span<T>(p1, size / d1).CopyTo(t2);
    }
    unsafe public static void memcpy<T>(void* p1, ReadOnlySpan<T> t2, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        t2.CopyTo(new Span<T>(p1, size / d1));
    }

    unsafe public void GetData<T>(int offset, int height, int RowPitch, int targetRowPitch, Span<T> bitmapData) where T : unmanaged
    {
        int size = Marshal.SizeOf(typeof(T));
        void* ptr = null;
        int imageSize = RowPitch * height;
        buffer.Map(0, new Vortice.Direct3D12.Range(offset, imageSize + offset), &ptr);
        ptr = (byte*)ptr + offset;
        for (int i = 0; i < height; i++)
        {
            memcpy(bitmapData.Slice(targetRowPitch * i / size, targetRowPitch / size), (byte*)ptr + RowPitch * i, targetRowPitch);
        }
        buffer.Unmap(0);
    }

    unsafe public int UploadData(ReadOnlySpan<byte> data)
    {
        int offset = GetOffsetAndMove(data.Length);

        void* ptr = null;
        buffer.Map(0, &ptr);
        ptr = (byte*)ptr + offset;

        memcpy(ptr, data, data.Length);
        buffer.Unmap(0);

        return offset;
    }

    public void Dispose()
    {
        buffer?.Release();
        buffer = null;
    }
    public ID3D12Resource buffer;

    internal int GetOffsetAndMove(int size)
    {
        if (((currentPosition + size + 511) & ~511) > this.size)
        {
            currentPosition = 0;
        }
        int result = currentPosition;
        currentPosition = ((currentPosition + size + 511) & ~511) % this.size;
        return result;
    }

    public int size;
    public int currentPosition;
}
