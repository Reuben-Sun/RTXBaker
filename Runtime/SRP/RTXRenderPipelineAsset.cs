using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Reuben.RTXBaker.Runtime
{
    [CreateAssetMenu(menuName = "Rendering/CreateRTXRenderPipeline")]
    public class RTXRenderPipelineAsset : RenderPipelineAsset
    {
        public RayTracingShader shader;
        protected override RenderPipeline CreatePipeline()
        {
            return new RTXRenderPipeline(this);
        }
    }

}
