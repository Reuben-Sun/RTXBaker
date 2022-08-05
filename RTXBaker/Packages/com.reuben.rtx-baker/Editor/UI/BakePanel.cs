using System;
using System.Collections.Generic;
using System.IO;
using Reuben.RTXBaker.Runtime;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Reuben.RTXBaker.Editor
{
    public class BakePanel
    {
        #region Properties

        public RayTracingShader _RaytraceShader;
        public int AATime = 10;
        public int RenderSize = 2048;

        #endregion
        
        #region Attribute

        private List<ProbeInfo> _probeInfos = new List<ProbeInfo>();
        private Camera _mainCamera;
        private RayTracingAccelerationStructure _accelerationStructure = new RayTracingAccelerationStructure();  //加速结构
        private ComputeBuffer PRNGStates;
        private Dictionary<int, RTHandle> renderTargetList = new Dictionary<int, RTHandle>();
        private int _frameIndex = 0;
        
        #endregion

        #region Const

        //镜头朝向，左为前向量，右为上向量
        Vector3[,] _faceDirs = new Vector3[6, 2] {
            {new Vector3( 1, 0, 0), new Vector3(0, 1, 0)},
            {new Vector3(-1, 0, 0), new Vector3(0, 1, 0)},
            {new Vector3( 0, 1, 0), new Vector3(0, 0,-1)},
            {new Vector3( 0,-1, 0), new Vector3(0, 0, 1)},
            {new Vector3( 0, 0, 1), new Vector3(0, 1, 0)},
            {new Vector3( 0, 0,-1), new Vector3(0, 1, 0)},
        };
        
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
        
        #region Debug Panel

        [Title("Debug Panel")]
        [Button("初始化光追Shader")]
        void GetRayTraceShader()
        {
            _RaytraceShader = AssetDatabase.LoadAssetAtPath<RayTracingShader>("Packages/com.reuben.rtx-baker/Runtime/Shader/RTXShader.raytrace");
        }
        
        [Button("收集LightProbe")]
        void GetLightProbeItem()
        {
            _probeInfos.Clear();
            var probes = GameObject.FindObjectsOfType<LightProbeItem>();
            foreach (var probe in probes)
            {
                var info = new ProbeInfo
                {
                    Position = probe.transform.position
                };
                _probeInfos.Add(info);
            }
        }
        
        [Button("构建加速结构")]
        void GetAccelerationStructure()
        {
            var sceneRenderers = GameObject.FindObjectsOfType<Renderer>();
            Debug.Log(sceneRenderers.Length);
            
            bool[] subMeshFlagArray = new bool[sceneRenderers.Length];
            bool[] subMeshCutoffArray = new bool[sceneRenderers.Length];
            for (int i = 0; i < sceneRenderers.Length; i++)
            {
                subMeshFlagArray[i] = true;
                subMeshCutoffArray[i] = false;
            }
            
            foreach (var sceneRenderer in sceneRenderers)
            {
                _accelerationStructure.AddInstance(sceneRenderer, subMeshFlagArray, subMeshCutoffArray);
            }
            _accelerationStructure.Build();
        }
        
        [Button("获得渲染相机")]
        void GetMainCamera()
        {
            var cameras = Camera.allCameras;
            if (cameras.Length == 0)
            {
                GameObject cam = new GameObject("Main Camera");
                Camera _c = cam.AddComponent<Camera>();
                _c.tag = "MainCamera";
                _c.aspect = 1;
                _c.fieldOfView = 90f;
                _mainCamera = _c;
            }
            else
            {
                foreach (var camera in cameras)
                {
                    if (camera.cameraType == CameraType.Game)
                    {
                        _mainCamera = camera;
                        _mainCamera.tag = "MainCamera";
                        _mainCamera.aspect = 1;
                        _mainCamera.fieldOfView = 90f;
                        return;
                    }
                }
            }
        }
        

        [Button("绘制")]
        void GetRenderTarget()
        {
            CommandBuffer cmd = new CommandBuffer {name = "RTX Camera"};
            SetupPRNGStates(_mainCamera);
            RTHandle renderTarget = SetupRT(_mainCamera);
            Vector4 renderTargetSize = new Vector4(RenderSize, RenderSize, 1.0f / RenderSize, 1.0f / RenderSize);

            for (int faceId = 0; faceId < _faceDirs.Length/2; faceId++)
            {
                SetCameraPosition(_mainCamera, faceId);
                _frameIndex = 0;
                SetupCamera(_mainCamera);
                cmd.ClearRenderTarget(true, true, Color.clear);
                try
                {
                    for (int i = 0; i < AATime; i++)
                    {
                        //RTX
                        cmd.SetRayTracingShaderPass(_RaytraceShader, "RayTracing");
                        cmd.SetRayTracingAccelerationStructure(_RaytraceShader, _accelerationStructureShaderId, _accelerationStructure);
                        cmd.SetRayTracingIntParam(_RaytraceShader, _frameIndexShaderId, _frameIndex);
                        cmd.SetRayTracingBufferParam(_RaytraceShader, _PRNGStatesShaderId, PRNGStates);
                        cmd.SetRayTracingTextureParam(_RaytraceShader, _renderTargetId, renderTarget);
                        cmd.SetRayTracingVectorParam(_RaytraceShader, _renderTargetSizeId, renderTargetSize);
                        cmd.DispatchRays(_RaytraceShader, "RTXShader", (uint) renderTarget.rt.width, (uint) renderTarget.rt.height, 1, _mainCamera);
                        Graphics.ExecuteCommandBuffer(cmd);
                        _frameIndex++;
                    }
                    SaveRT(renderTarget, $"RT{faceId}");
                }
                finally
                {
                
                }
            }
        }

        #endregion

        #region Bake Panel

        [Title("Bake Panel")]
        [Button("Bake")]
        void Bake()
        {
            GetRayTraceShader();
            GetLightProbeItem();
            GetAccelerationStructure();
            GetMainCamera();
            GetRenderTarget();
        }

        #endregion
        
        #region Utils

        private void SetupCamera(Camera camera)
        {
            Shader.SetGlobalVector(CameraShaderParams._WorldSpaceCameraPos, camera.transform.position);
            var projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            var viewMatrix = camera.worldToCameraMatrix;
            var viewProjMatrix = projMatrix * viewMatrix;
            var invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
            Shader.SetGlobalMatrix(CameraShaderParams._InvCameraViewProj, invViewProjMatrix);
            Shader.SetGlobalFloat(CameraShaderParams._CameraFarDistance, camera.farClipPlane);
        }
        
        private RTHandle SetupRT(Camera camera)
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
                    RenderSize,
                    RenderSize,
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
        private void SetupPRNGStates(Camera camera)
        {
            PRNGStates = new ComputeBuffer(RenderSize * RenderSize, 4 * 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            var _mt19937 = new MersenneTwister.MT.mt19937ar_cok_opt_t();
            _mt19937.init_genrand((uint)System.DateTime.Now.Ticks);
            var data = new uint[RenderSize * RenderSize * 4];
            for (var i = 0; i < RenderSize * RenderSize * 4; ++i)
                data[i] = _mt19937.genrand_int32();
            PRNGStates.SetData(data);
        }

        public void SaveRT(RenderTexture rt, string fileName)
        {
            if (fileName == "")
            {
                Debug.LogError("fail to save rt: fileName is empty");
                return;
            }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            byte[] bytes = tex.EncodeToTGA();
            File.WriteAllBytes($"Assets/{fileName}.tga", bytes);
            AssetDatabase.Refresh();
            //贴图格式设置
            TextureImporter importer = AssetImporter.GetAtPath($"Assets/{fileName}.tga") as TextureImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = false;
            importer.SetPlatformTextureSettings("Android", 1024, TextureImporterFormat.ASTC_8x8);
            importer.SetPlatformTextureSettings("iPhone", 1024, TextureImporterFormat.ASTC_8x8);
            importer.SetPlatformTextureSettings("Standalone", 2048, TextureImporterFormat.BC7);
            importer.SaveAndReimport();
        }
        void SetCameraPosition(Camera camera, int faceId)
        {
            if (camera == null || _probeInfos.Count == 0)
            {
                return;
            }
            
            camera.transform.position = _probeInfos[0].Position;
            camera.transform.rotation = Quaternion.LookRotation(_faceDirs[faceId, 0], _faceDirs[faceId, 1]);
        }
        #endregion
    }
}