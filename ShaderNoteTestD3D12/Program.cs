using ShaderNoteD3D12;
using System.Numerics;
using ShaderNote.ModelLoaders;

namespace ShaderNoteTestD3D12;

using System.Numerics;
using Vortice.DXGI;
using Vortice.Direct3D12;
using System.IO;
using System.Linq;
using System;

//internal class Program
//{
//    static void Main(string[] args)
//    {
//        NoteDevice device = new NoteDevice();
//        var render = device.GetRecord()
//            .WithVertexShader("Shaders/VertexShader.hlsl", name: "vs")
//            .WithPixelShader("Shaders/PixelShader.hlsl", name: "ps")
//            .WithConstantBuffer(0, new Vector4(1, 0, 1, 1))
//            .WithVertexBuffer(0, 12, data: new Vector3[]
//            {
//                new(0,0,0),
//                new(1,0,0),
//                new(0,1,0),
//                new(1,1,0),
//            }, name: "vb0")
//            .WithIndexBuffer(data: new ushort[] { 0, 2, 1, 1, 2, 3 })
//            //.WithImage(0, "test1.png")
//            .WithSampler(0)
//            .WithDepth();
//        var result=
//        render
//            .WithDrawIndexed(6).Render();
//        result.GetHtml();
//        result.Dispose();
//        render.WithDrawIndexed(6).Save("test1.png");

//        var render2 = render.WithDrawIndexed(6);

//        var result2 = render2.Render();
//        result2.GetHtml();
//        result2.Dispose();

//        device.Dispose();
//    }
//}

public struct ShaderData
{
    public Matrix4x4 MVP;
    public Matrix4x4 Shadow;
    public Vector4 LightColor;
    public Vector3 LightDir;
}

internal class Program
{
    static void TestRender(RenderRecord renderRecord)
    {
        var result = renderRecord.Render();
        result.GetHtml();
        result.Dispose();
    }
    static void Main(string[] args)
    {
        NoteDevice noteDevice = new NoteDevice();
        var texturePath = "../../kizunaai/";
        var pmxModelPath = "../../kizunaai/kizunaai.pmx";
        PMXFormat pmx = PMXFormat.Load(pmxModelPath);

        var positions = pmx.Vertices.Select(u => u.Coordinate).ToArray();
        var normals = pmx.Vertices.Select(u => u.Normal).ToArray();
        var uvs = pmx.Vertices.Select(u => u.UvCoordinate).ToArray();
        var indices = pmx.TriangleIndexs;
        var materials = pmx.Materials;
        var textures = pmx.Textures;

        var lightDir = Vector3.Normalize(new Vector3(0.05f, 1, -0.3f));
        var shadowCenter = new Vector3(0, 10, 0);
        var shadowMatrix = Matrix4x4.Transpose(Matrix4x4.CreateLookAt(lightDir * 25 + shadowCenter, shadowCenter, Vector3.UnitY) *
            Matrix4x4.CreateOrthographic(16, 16, 0, 50));

        var vmat = Matrix4x4.CreateLookAt(new Vector3(0, 15, -10), new Vector3(0, 15, 0), Vector3.UnitY);
        var pmat = Matrix4x4.CreatePerspectiveFieldOfView(60.0f / 180.0f * MathF.PI, 1.0f, 1.0f, 100.0f);

        ShaderData constantBuffer = new ShaderData
        {
            MVP = Matrix4x4.Transpose(Matrix4x4.Multiply(vmat, pmat)),
            Shadow = shadowMatrix,
            LightColor = new Vector4(1, 1, 1, 1),
            LightDir = lightDir,
        };

        var shadowRender = noteDevice.GetRecord()
            .WithVertexShader("Shaders/vs_shadow.hlsl")
            .WithIndexBuffer(data: indices)
            .WithVertexBuffer(0, 12, data: positions)
            .WithConstantBuffer(0, shadowMatrix)
            .WithMRT()
            .WithDepth()
            .WithDrawIndexed(indices.Length);

        TestRender(shadowRender);
        //shadowRender

        var render = noteDevice.GetRecord()
.WithVertexShader("Shaders/vs_3d_03.hlsl")
.WithPixelShader("Shaders/ps_3d_03.hlsl")
.WithIndexBuffer(data: indices)
.WithVertexBuffer(0, 12, data: positions)
.WithVertexBuffer(1, 12, data: normals)
.WithVertexBuffer(2, 8, data: uvs)
.WithSampler(0)
.WithConstantBuffer(0, constantBuffer)
.WithBlendState(BlendDescription.AlphaBlend)
.WithImage(1, shadowRender.Render());

        foreach (var material in materials)
        {
            render = render
                .WithImage(0, file: Path.Combine(texturePath, textures[material.TextureIndex].TexturePath))
                .WithDrawIndexed(material.TriangeIndexNum, startIndexLocation: material.TriangeIndexStartNum);
        }
        render = render
            .WithMRT(Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm)
            .WithDepth();

        //View Result
        TestRender(render);
        //render


        RenderHelper.GIF((frame) =>
        {
            var lightDir = Vector3.Normalize(new Vector3(0.05f, 1 - frame * 0.2f, -0.3f));
            var shadowCenter = new Vector3(0, 10, 0);
            var shadowMatrix = Matrix4x4.Transpose(Matrix4x4.CreateLookAt(lightDir * 25 + shadowCenter, shadowCenter, Vector3.Cross(Vector3.Cross(lightDir, Vector3.UnitY), lightDir)) *
                Matrix4x4.CreateOrthographic(16, 16, 0, 50));

            var vmat = Matrix4x4.CreateLookAt(new Vector3(0, 15, -10), new Vector3(0, 15, 0), Vector3.UnitY);
            var pmat = Matrix4x4.CreatePerspectiveFieldOfView(60.0f / 180.0f * MathF.PI, 1.0f, 1.0f, 100.0f);

            ShaderData constantBuffer = new ShaderData
            {
                MVP = Matrix4x4.Transpose(Matrix4x4.Multiply(vmat, pmat)),
                Shadow = shadowMatrix,
                LightColor = new Vector4(1, 1, 1, 1),
                LightDir = lightDir,
            };

            var shadowRender = noteDevice.GetRecord()
            .WithVertexShader("Shaders/vs_shadow.hlsl")
            .WithIndexBuffer(data: indices)
            .WithVertexBuffer(0, 12, data: positions)
            .WithConstantBuffer(0, shadowMatrix)
            .WithMRT()
            .WithDepth()
            .WithDrawIndexed(indices.Length);

            var render = noteDevice.GetRecord()
            .WithVertexShader("Shaders/vs_3d_03.hlsl")
            .WithPixelShader("Shaders/ps_3d_03.hlsl")
            .WithIndexBuffer(data: indices)
            .WithVertexBuffer(0, 12, data: positions)
            .WithVertexBuffer(1, 12, data: normals)
            .WithVertexBuffer(2, 8, data: uvs)
            .WithSampler(0)
            .WithConstantBuffer(0, constantBuffer)
            .WithBlendState(BlendDescription.AlphaBlend)
            .WithImage(1, shadowRender.Render());

            foreach (var material in materials)
            {
                render = render
                    .WithImage(0, file: Path.Combine(texturePath, textures[material.TextureIndex].TexturePath))
                    .WithDrawIndexed(material.TriangeIndexNum, startIndexLocation: material.TriangeIndexStartNum);
            }
            render = render
                .WithMRT(Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm)
                .WithDepth();

            return render;
        }, ".cache/03.gif", 256, 256, 12, frameDelay: 20);
        //"<img src='.cache/03.gif'>".DisplayAs("text/html");
        noteDevice.Dispose();
    }
}