using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Reuben.RTXBaker.Runtime
{
    public class RTXRenderPipeline: RenderPipeline
    {
        private RTXRenderPipelineAsset _asset;
        public Camera mainCamera;
        public CommandBuffer cmd;
        public RayTracingAccelerationStructure _accelerationStructure;  //加速结构

        #region ID
        
        private static int renderTargetId = Shader.PropertyToID("RenderTarget");
        private static class CameraShaderParams
        {
            public static readonly int _WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
            public static readonly int _InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
            public static readonly int _CameraFarDistance = Shader.PropertyToID("_CameraFarDistance");
        }
        private readonly int accelerationStructureShaderId = Shader.PropertyToID("_AccelerationStructure");
        
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
            //创建RT
            RTHandle renderTarget = RTHandles.Alloc(
                mainCamera.pixelWidth,
                mainCamera.pixelHeight,
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
                $"OutputTarget_{mainCamera.name}");

            try
            {
                using (new ProfilingSample(cmd, "RayTracing"))
                {
                    //RTX
                    cmd.SetRayTracingShaderPass(_asset.shader, "RayTracing");
                    cmd.SetRayTracingAccelerationStructure(_asset.shader, accelerationStructureShaderId, _accelerationStructure);
                    cmd.SetRayTracingTextureParam(_asset.shader, renderTargetId, renderTarget);
                    cmd.DispatchRays(_asset.shader, "RTXShader", (uint) renderTarget.rt.width, (uint) renderTarget.rt.height, 1, mainCamera);
                }
                context.ExecuteCommandBuffer(cmd);
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
    }
}