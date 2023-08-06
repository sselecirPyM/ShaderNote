using System;
using System.Collections.Generic;
using System.Threading;
using Vortice.Direct3D12;

namespace ShaderNoteD3D12;

internal class FrameAllocatorResource
{
    public ulong endFrame;
    public ID3D12CommandAllocator allocator;
    public FrameAllocatorResource(ulong endFrame, ID3D12CommandAllocator allocator)
    {
        this.endFrame = endFrame;
        this.allocator = allocator;
    }
}

internal sealed class CommandRefObject
{
    public ulong fenceValue;
    public ID3D12Object refObject;
}
internal sealed class CommandQueue : IDisposable
{
    const int c_frameCount = 3;

    internal ID3D12CommandQueue commandQueue;

    internal List<FrameAllocatorResource> commandAllocators = new List<FrameAllocatorResource>();

    internal List<CommandRefObject> commandListRef = new List<CommandRefObject>();

    internal uint executeIndex = 0;

    ID3D12GraphicsCommandList4 m_commandList;

    ID3D12Device device;

    CommandListType commandListType;

    internal ID3D12Fence fence;

    internal UInt64 currentFenceValue = 3;

    EventWaitHandle fenceEvent;

    public void Initialize(ID3D12Device device, CommandListType commandListType)
    {
        this.device = device;
        this.commandListType = commandListType;
        device.CreateCommandQueue(new CommandQueueDescription(commandListType), out commandQueue).CheckError();
        for (int i = 0; i < c_frameCount; i++)
        {
            device.CreateCommandAllocator(commandListType, out ID3D12CommandAllocator commandAllocator).CheckError();
            commandAllocators.Add(new FrameAllocatorResource(0, commandAllocator));
        }
        device.CreateFence(c_frameCount, FenceFlags.None, out fence).CheckError();
        fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        currentFenceValue = c_frameCount;
        currentFenceValue++;
    }

    public SharpGen.Runtime.Result Signal(ulong value) => commandQueue.Signal(fence, value);

    public SharpGen.Runtime.Result Wait(ID3D12Fence fence, ulong value) => commandQueue.Wait(fence, value);

    public void ExecuteCommandList(ID3D12CommandList commandList) => commandQueue.ExecuteCommandList(commandList);

    public ID3D12CommandAllocator GetCommandAllocator() => commandAllocators[(int)executeIndex].allocator;

    public void Wait()
    {
        // 在队列中安排信号命令。
        Signal(currentFenceValue);

        // 等待跨越围栏。
        fence.SetEventOnCompletion(currentFenceValue, fenceEvent);
        fenceEvent.WaitOne();
        currentFenceValue++;
        ReleaseRefs();
    }

    /// <summary>
    ///  Gpu side wait.
    /// </summary>
    public void WaitFor(CommandQueue other)
    {
        commandQueue.Wait(other.fence, other.currentFenceValue - 1);
    }

    public void NextExecuteIndex()
    {
        Signal(currentFenceValue);
        executeIndex = (executeIndex < (c_frameCount - 1)) ? (executeIndex + 1) : 0;

        // 检查下一帧是否准备好启动。
        if (fence.CompletedValue < currentFenceValue - c_frameCount + 1)
        {
            fence.SetEventOnCompletion(currentFenceValue - c_frameCount + 1, fenceEvent);
            fenceEvent.WaitOne();
        }
        commandAllocators[(int)executeIndex].allocator.Reset();
        currentFenceValue++;
        ReleaseRefs();
    }

    void ReleaseRefs()
    {
        commandListRef.RemoveAll(e =>
        {
            if(e.fenceValue <= fence.CompletedValue)
            {
                e.refObject.Release();
                return true;
            }
            return false;
        });

    }

    public void CommandRef(ID3D12Object obj)
    {
        obj.AddRef();
        commandListRef.Add(new CommandRefObject()
        {
            refObject = obj,
            fenceValue = currentFenceValue,
        });
    }

    internal ID3D12GraphicsCommandList4 GetCommandList()
    {
        if (m_commandList != null)
        {
            return m_commandList;
        }
        else
        {
            device.CreateCommandList(0, commandListType, GetCommandAllocator(), null, out m_commandList).CheckError();
            m_commandList.Close();
            return m_commandList;
        }
    }

    public void Dispose()
    {
        commandQueue?.Release();
        commandQueue = null;
        if (commandAllocators != null)
            foreach (var allocator in commandAllocators)
                allocator.allocator.Release();
        m_commandList?.Release();
        m_commandList = null;
        fence?.Release();
        fence = null;
        fenceEvent.Dispose();
        fenceEvent = null;
    }
}
