namespace ShaderNoteD3D12;

public class VariableSlot
{
    public object Value { get; init; }
    public string ShortCut { get; set; }
    public string File { get; init; }
    public string SlotName { get; init; }

    public object Value1 { get; init; }
    public string EntryPoint { get; set; }

    public bool AsArgument { get; set; }
}
