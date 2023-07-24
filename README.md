# ShaderNote

用于快速验证着色器效果的工具。


入门

```CSharp
using ShaderNote;
using System.Numerics;
NoteDevice noteDevice = new NoteDevice();

var render = noteDevice.GetRecord()
    .WithVertexShader("Shaders/VertexShader.hlsl")
    .WithPixelShader("Shaders/PixelShader.hlsl")
    .WithIndexBuffer(data: new ushort[] { 0, 2, 1, 1, 2, 3 })
    .WithVertexBuffer(0, 12, data: new Vector3[]
    {
        new(0,0,0),
        new(1,0,0),
        new(0,1,0),
        new(1,1,0),
    })
    .WithVertexBuffer(1, 8, data: new Vector2[]
    {
        new(0,0),
        new(1,0),
        new(0,1),
        new(1,1),
    })
    .WithSampler(0)
    .WithImage(0, "sample.png")
    .WithDrawIndexed(6);
render.Save("test.png");
```