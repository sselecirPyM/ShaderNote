using ShaderNote;
using System;
using System.Numerics;

namespace ShaderNoteTest;

internal class Program
{
    static void Main(string[] args)
    {
        NoteDevice noteDevice = new NoteDevice();

        var record1 = noteDevice.GetRecord();
        var record2 = record1
            .WithVertexShader("Shaders/VertexShader.hlsl", name: "vs")
            .WithPixelShader("Shaders/PixelShader.hlsl", name: "ps")
            //.WithInputLayout(new Vortice.Direct3D11.InputElementDescription[] { new Vortice.Direct3D11.InputElementDescription("POSITION", 0, Vortice.DXGI.Format.R32G32B32_Float, 0) })
            //.WithPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList)
            .WithConstantBuffer(0, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), name: "cb0")
            .WithVertexBuffer(0, 12, data: new Vector3[]
            {
                new(0,0,0),
                new(1,0,0),
                new(0,1,0),
                new(1,1,0),
            }, name: "vb0")
            .WithIndexBuffer(data: new ushort[] { 0, 2, 1, 1, 2, 3 })
            .WithImage(0, "sample.png")
            .WithSampler(0)
            .WithDrawIndexed(6);

        //var record3 = record2.WithPixelShader("Shaders/PixelShader.hlsl")
        //     .WithDrawIndexed(6);
        //var result = record3.Render();
        //result.Save("test.png");

        //for (int i = 0; i < 15; i++)
        //{
        //    record2.WithPixelShader("Shaders/PixelShader.hlsl")
        //         .WithDrawIndexed(6)
        //         .Save("test1.png");
        //}
        //GC.Collect(0, GCCollectionMode.Forced, true, true);
        //GC.Collect(1, GCCollectionMode.Forced, true, true);
        //GC.Collect(2, GCCollectionMode.Forced, true, true);

        record2.WithPixelShader("Shaders/PixelShader.hlsl", name: "ps", argument: true)
             .Save("test1.png");

        record2.WithPixelShader("Shaders/PixelShader2.hlsl", name: "ps", argument: true)
             .Save("test2.png");


        record1
           .WithVertexShader("Shaders/VertexShader.hlsl", name: "vs")
           .WithPixelShader("Shaders/PixelShader.hlsl", name: "ps")
           .WithInputLayout(new Vortice.Direct3D11.InputElementDescription[] { new Vortice.Direct3D11.InputElementDescription("POSITION", 0, Vortice.DXGI.Format.R32G32B32_Float, 0) })
           //.WithPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList)
           .WithConstantBuffer(0, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), name: "cb0")
           .WithVertexBuffer(0, 12, data: new Vector3[]
           {
                new(0,0,0),
                new(1,0,0),
                new(0,1,0),
                new(1,1,0),
           }, name: "vb0")
           .WithIndexBuffer(data: new ushort[] { 0, 2, 1, 1, 2, 3 })
           .WithImage(0, "sample.png")
           .WithSampler(0)
           .WithDrawIndexed(6)
           .Save("test8.png");

        record2.WithPixelShader("Shaders/PixelShader2.hlsl", name: "ps", argument: true)
            .WithConstantBuffer(0, new Vector4(1.0f, 1.0f, 0.0f, 1.0f), name: "cb0", argument: true)
            .WithVertexBuffer(data: new Vector3[]
            {
                new(-1,0,0),
                new(1,0,0),
                new(0,1,0),
                new(1,1,0),
            }, name: "vb0", argument: true)
            .WithMRT(Vortice.DXGI.Format.R8G8B8A8_UNorm, Vortice.DXGI.Format.R8G8B8A8_UNorm)
            .Save("test10.png", 0);

        string shader3 =
"""
Texture2D<float4> texture0 : register(t0);
SamplerState sampler0 : register(s0);

struct PSIn
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};
float4 main(PSIn input) : SV_TARGET
{
    //return texture0.Sample(sampler0, input.texcoord);
    return float4(0.5,input.texcoord, 1);
}
""";

        var recordX = record2.WithPixelShader(source: shader3)
             .WithDrawIndexed(6);
        recordX
             .Save("test3.png");

        //recordX
        //     .Save("test4.png");

        //result.Dispose();


        var mypipe = record1
            .WithVertexShader("Shaders/VertexShader2.hlsl", name: "vs")
            .WithPixelShader("Shaders/PixelShader.hlsl", name: "ps")
            .WithConstantBuffer(0, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), name: "cb0")
            .WithVertexBuffer(0, 12, data: new Vector3[]
            {
                new(0,0,0.8f),
                new(1,0,0.8f),
                new(0,1,0.8f),
                new(1,1,0.8f),
            }, name: "vb0")
            .WithVertexBuffer(1, 12, data: new Vector3[]
            {
                new(0,0,1),
                new(0,0,1),
                new(0,0,1),
                new(0,0,1),
            }, name: "vb1")
            .WithVertexBuffer(2, 8, data: new Vector2[]
            {
                new(0,0),
                new(1,0),
                new(0,1),
                new(1,1),
            }, name: "vb2")
            .WithIndexBuffer(data: new ushort[] { 0, 2, 1, 1, 2, 3 })
            .WithPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList)
            .WithImage(0, "sample.png")
            .WithSampler(0)
            .WithDepth()
             .WithDrawIndexed(6);
        //Console.WriteLine(mypipe.Render().GetHtml());
        mypipe.Save("test9.png");
        //RenderRecord.Combine(record2, mypipe);

        noteDevice.Dispose();
    }
}