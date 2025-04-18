using System;

namespace ShaderNoteD3D12;

public class VariableSlot
{
    internal Action<NoteDevice, RenderStates> Call { get; set; }
    internal Action<NoteDevice, RenderStates> BeforeRenderCall { get; set; }
}
