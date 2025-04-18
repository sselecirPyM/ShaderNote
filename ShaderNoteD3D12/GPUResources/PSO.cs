using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Shader;

namespace ShaderNoteD3D12.GPUResources
{
    internal class CBVDescription
    {
        public Dictionary<string, int> positionMap = new Dictionary<string, int>();
        public int size;
    }
    public class PSO : IDisposable
    {
        internal Dictionary<int, CBVDescription> cbvDescriptions = new Dictionary<int, CBVDescription>();
        internal RootSignature rootSignature;
        internal ID3D12PipelineState pipelineState;


        internal RootSignature CreateRootSignature(NoteDevice noteDevice, params ID3D12ShaderReflection[] reflections)
        {
            rootSignature = new RootSignature();
            var constantBuffers = new Dictionary<string, ID3D12ShaderReflectionConstantBuffer>();

            var parameters = new List<RootParameter1>();
            var samplers = new List<StaticSamplerDescription>();
            var bindings = new List<InputBindingDescription>();
            void ProcessReflection(ID3D12ShaderReflection shaderReflection)
            {
                if (shaderReflection == null)
                    return;
                bindings.AddRange(shaderReflection.BoundResources);
                foreach (var constantBuffer in shaderReflection.ConstantBuffers)
                {
                    constantBuffers[constantBuffer.Description.Name] = constantBuffer;
                }
            }
            foreach (var reflection in reflections)
                ProcessReflection(reflection);

            HashSet<int> srvVisited = new HashSet<int>();
            HashSet<int> cbvVisited = new HashSet<int>();
            HashSet<int> uavVisited = new HashSet<int>();

            foreach (var res in bindings)
            {
                switch (res.Type)
                {
                    case ShaderInputType.Texture:
                    case ShaderInputType.Structured:
                        if (!srvVisited.Add(res.BindPoint))
                            continue;
                        rootSignature.srv[res.BindPoint] = parameters.Count;
                        parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                                        DescriptorRangeType.ShaderResourceView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                        break;
                    case ShaderInputType.ConstantBuffer:
                        if (!cbvVisited.Add(res.BindPoint))
                            continue;
                        rootSignature.cbv[res.BindPoint] = parameters.Count;
                        parameters.Add(new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                        if (constantBuffers.TryGetValue(res.Name, out var buffer))
                        {
                            var positionMap = new Dictionary<string, int>();
                            foreach (var item in buffer.Variables)
                            {
                                positionMap[item.Description.Name] = item.Description.StartOffset;
                            }

                            cbvDescriptions[res.BindPoint] = new CBVDescription()
                            {
                                positionMap = positionMap,
                                size = buffer.Description.Size,
                            };
                        }
                        break;
                    case ShaderInputType.Sampler:
                        samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                            0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                        //samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                        //        0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                        break;
                    case ShaderInputType.UnorderedAccessViewRWTyped:
                    case ShaderInputType.UnorderedAccessViewRWStructured:
                        if (!uavVisited.Add(res.BindPoint))
                            continue;
                        rootSignature.uav[res.BindPoint] = parameters.Count;
                        parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                                DescriptorRangeType.UnorderedAccessView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                        break;
                    default:
                        break;
                }
            }
            //var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.None, parameters.ToArray(), samplers.ToArray());
            var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.AllowInputAssemblerInputLayout, parameters.ToArray(), samplers.ToArray());
            rootSignature.description1 = rootSignatureDescription1;
            rootSignature.rootSignature = noteDevice.device.CreateRootSignature(rootSignatureDescription1);
            return rootSignature;
        }

        public void Dispose()
        {
            rootSignature?.Dispose();
            rootSignature = null;
            pipelineState?.Release();
            pipelineState = null;
            cbvDescriptions.Clear();
        }
    }
}
