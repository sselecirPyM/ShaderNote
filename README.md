# ShaderNote

编写3D应用程序也许没那么困难，这个工具将帮助你快速实现你的想法。


## 入门

以下代码将图像绘制在目标上，然后保存结果。

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

## 在Polyglot Notebooks使用Shader Note

1. 安装.Net 8 SDK

2. 在Visual Studio Code中安装Polyglot Notebooks扩展

3. 为了正常显示html，在notebook中加入以下代码

```CSharp
using Microsoft.DotNet.Interactive.Formatting;
using ShaderNote;

Formatter.Register<RenderRecord>((w)=>
{
    var result = w.Render();
    string html = result.GetHtml();
    result.Dispose();
    return html;
},HtmlFormatter.MimeType);

```

4. 将RenderRecord类型置于单元格最后，不要加分号。

```CSharp

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
//用于显示
render
```

你可以在[examples](./examples/)文件夹下找到示例。