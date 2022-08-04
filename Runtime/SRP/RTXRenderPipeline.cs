using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Reuben.RTXBaker.Runtime
{
    public class RTXRenderPipeline: RenderPipeline
    {
        private RTXRenderPipelineAsset _asset;
        private Camera mainCamera;
        private CommandBuffer cmd;
        private RayTracingAccelerationStructure _accelerationStructure;  //加速结构
        private int _frameIndex = 0;
        private ComputeBuffer PRNGStates;
        private Dictionary<int, RTHandle> renderTargetList = new Dictionary<int, RTHandle>();

        #region ID
        
        private static readonly int _renderTargetId = Shader.PropertyToID("_RenderTarget");
        private static readonly int _frameIndexShaderId = Shader.PropertyToID("_FrameIndex");
        private static readonly int _PRNGStatesShaderId = Shader.PropertyToID("_PRNGStates");
        private static readonly int _renderTargetSizeId = Shader.PropertyToID("_RenderTargetSize");
        private static class CameraShaderParams
        {
            public static readonly int _WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
            public static readonly int _InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
            public static readonly int _CameraFarDistance = Shader.PropertyToID("_CameraFarDistance");
        }
        private static readonly int _accelerationStructureShaderId = Shader.PropertyToID("_AccelerationStructure");
        
        #endregion
        
        //管线初始化
        public RTXRenderPipeline(RTXRenderPipelineAsset asset)
        {
            _asset = asset;
            _accelerationStructure = new RayTracingAccelerationStructure();
        }
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            //初始化变量
            mainCamera = cameras[0];
            cmd = new CommandBuffer {name = "RTX Camera"};
            SetupCamera();
            SetupAccelerationStructure();   //初始化加速结构
            SetupPRNGStates();
            //创建RT
            RTHandle renderTarget = SetupRT(mainCamera);
            
            Vector4 renderTargetSize = new Vector4(mainCamera.pixelWidth, mainCamera.pixelHeight, 1.0f / mainCamera.pixelWidth, 1.0f / mainCamera.pixelHeight);
            
            try
            {
                if (_frameIndex < 1000)
                {
                    using (new ProfilingSample(cmd, "RayTracing"))
                    {
                        //RTX
                        cmd.SetRayTracingShaderPass(_asset.shader, "RayTracing");
                        cmd.SetRayTracingAccelerationStructure(_asset.shader, _accelerationStructureShaderId, _accelerationStructure);
                        cmd.SetRayTracingIntParam(_asset.shader, _frameIndexShaderId, _frameIndex);
                        cmd.SetRayTracingBufferParam(_asset.shader, _PRNGStatesShaderId, PRNGStates);
                        cmd.SetRayTracingTextureParam(_asset.shader, _renderTargetId, renderTarget);
                        cmd.SetRayTracingVectorParam(_asset.shader, _renderTargetSizeId, renderTargetSize);
                        cmd.DispatchRays(_asset.shader, "RTXShader", (uint) renderTarget.rt.width, (uint) renderTarget.rt.height, 1, mainCamera);
                    }
                    context.ExecuteCommandBuffer(cmd);
                    if (mainCamera.cameraType == CameraType.Game)
                    {
                        _frameIndex++;
                    }
                }
                
                using (new ProfilingSample(cmd, "FinalBilt"))
                {
                    //屏幕绘制
                    cmd.Blit(renderTarget, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                    
            }
            //提交命令
            context.Submit();
        }

        public void SetupCamera()
        {
            Shader.SetGlobalVector(CameraShaderParams._WorldSpaceCameraPos, mainCamera.transform.position);
            var projMatrix = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
            var viewMatrix = mainCamera.worldToCameraMatrix;
            var viewProjMatrix = projMatrix * viewMatrix;
            var invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
            Shader.SetGlobalMatrix(CameraShaderParams._InvCameraViewProj, invViewProjMatrix);
            Shader.SetGlobalFloat(CameraShaderParams._CameraFarDistance, mainCamera.farClipPlane);
        }

        public void SetupAccelerationStructure()
        {
            if (SceneManager.Instance == null || !SceneManager.Instance.isDirty) return;

            _accelerationStructure.Dispose();
            _accelerationStructure = new RayTracingAccelerationStructure();

            SceneManager.Instance.FillAccelerationStructure(ref _accelerationStructure);

            _accelerationStructure.Build();

            SceneManager.Instance.isDirty = false;
        }

        public void SetupPRNGStates()
        {
            PRNGStates = new ComputeBuffer(mainCamera.pixelWidth * mainCamera.pixelHeight, 4 * 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            var _mt19937 = new MersenneTwister.MT.mt19937ar_cok_opt_t();
            _mt19937.init_genrand((uint)System.DateTime.Now.Ticks);
            var data = new uint[mainCamera.pixelWidth * mainCamera.pixelHeight * 4];
            for (var i = 0; i < mainCamera.pixelWidth * mainCamera.pixelHeight * 4; ++i)
                data[i] = _mt19937.genrand_int32();
            PRNGStates.SetData(data);
        }
        public RTHandle SetupRT(Camera camera)
        {
            int id = camera.GetInstanceID();
            if (renderTargetList.TryGetValue(id, out var renderTarget))
            {
                return renderTarget;
            }
            else
            {
                //创建RT
                renderTarget = RTHandles.Alloc(
                    camera.pixelWidth,
                    camera.pixelHeight,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R32G32B32A32_SFloat,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    false,
                    false,
                    false,
                    1,
                    0f,
                    MSAASamples.None,
                    false,
                    false,
                    RenderTextureMemoryless.None,
                    $"OutputTarget_{camera.name}");
                renderTargetList.Add(id, renderTarget);
                return renderTarget;
            }
        }
    }
}