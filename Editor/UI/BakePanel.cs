﻿using System;
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
        public int RenderSize = 256;
        public bool SaveCubemap = true;

        #endregion
        
        #region Attribute

        private List<ProbeInfo> _probeInfos = new List<ProbeInfo>();
        private Camera _mainCamera;
        private RayTracingAccelerationStructure _accelerationStructure = new RayTracingAccelerationStructure();  //加速结构
        private ComputeBuffer PRNGStates;
        private Dictionary<int, RTHandle> renderTargetList = new Dictionary<int, RTHandle>();
        private int _frameIndex = 0;
        private Dictionary<int, CubemapInfo> cubemapInfo = new Dictionary<int, CubemapInfo>();
        #endregion

        #region Const

        //镜头朝向，0为前向量，1为上向量
        //左手系，大拇指为 y，食指方向为 x，中指方向为 z
        Vector3[,] _faceDirs = new Vector3[6, 3] {
            {new Vector3( 1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)},    //x轴正方形
            {new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1)},   //x轴负方向
            {new Vector3( 0, 1, 0), new Vector3(0, 0,-1), new Vector3(0, 0, 1)},    //y轴正方向
            {new Vector3( 0,-1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, 1)},    //y轴负方向
            {new Vector3( 0, 0, 1), new Vector3(0, 1, 0), new Vector3(-1, 0, 0)},   //z轴正方向
            {new Vector3( 0, 0,-1), new Vector3(0, 1, 0), new Vector3(1, 0, 0)},    //z轴负方向
        };

        private static readonly int _faceDirTime = 3;   //方向数
        
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
            
            cubemapInfo.Clear();

            for (int faceId = 0; faceId < _faceDirs.Length/_faceDirTime; faceId++)
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
                    SaveRT(renderTarget, faceId, SaveCubemap);
                }
                finally
                {
                
                }
            }
        }

        [Button("积分")]
        void GetIrradianceMap()
        {
            for (int i = 0; i < RenderSize; i++)
            {
                Debug.Log(SampleCubemap(0, i, RenderSize/2));
                Debug.Log(GetAreaSize(i, RenderSize/2));
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
            GetIrradianceMap();
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

        public void SaveRT(RenderTexture rt, int faceId, bool _saveCubemap)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            
            //保存cubemap
            if (_saveCubemap)   
            {
                byte[] bytes = tex.EncodeToTGA();
                File.WriteAllBytes($"Assets/RT{faceId}.tga", bytes);
                RenderTexture.active = prev;
                AssetDatabase.Refresh();
                //贴图格式设置
                TextureImporter importer = AssetImporter.GetAtPath($"Assets/RT{faceId}.tga") as TextureImporter;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.sRGBTexture = false;
                importer.SetPlatformTextureSettings("Android", RenderSize/2, TextureImporterFormat.ASTC_8x8);
                importer.SetPlatformTextureSettings("iPhone", RenderSize/2, TextureImporterFormat.ASTC_8x8);
                importer.SetPlatformTextureSettings("Standalone", RenderSize, TextureImporterFormat.BC7);
                importer.SaveAndReimport();
            }
            
            Color[] tempColor = tex.GetPixels();
            if (!cubemapInfo.ContainsKey(faceId))
            {
                cubemapInfo.Add(faceId, new CubemapInfo{colors = tempColor});
            }
            
            
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

        Vector3 GetNormalDir(int faceId, int sampleX, int sampleY)
        {
            float u = 2f * ((sampleX + 0.5f) / RenderSize) - 1;
            float v = 2f * ((sampleY + 0.5f) / RenderSize) - 1;
            Vector3 normalDir = _faceDirs[faceId, 0] + _faceDirs[faceId, 1] * v + _faceDirs[faceId, 2] * u;
            return Vector3.Normalize(normalDir);
        }

        Color SampleCubemap(int faceId, int sampleX, int sampleY)
        {
            if (cubemapInfo.ContainsKey(faceId))
            {
                return cubemapInfo[faceId].colors[sampleY * RenderSize + sampleX];
            }
            return Color.black;
        }

        float GetPreAreaSize(float x, float y)
        {
            return (float)Math.Atan2(x * y, Math.Sqrt(x * x + y * y + 1.0));
        }
        //求像素在单位球面的投影面积，公式来自 GAMES202
        float GetAreaSize(int sampleX, int sampleY)     
        {
            float u = 2f * ((sampleX + 0.5f) / RenderSize) - 1;
            float v = 2f * ((sampleY + 0.5f) / RenderSize) - 1;
            float prePixelSize = 1f / RenderSize;
            float x0 = u - prePixelSize;
            float y0 = v - prePixelSize;
            float x1 = u + prePixelSize;
            float y1 = v + prePixelSize;
            float angle = GetPreAreaSize(x0, y0) - GetPreAreaSize(x0, y1) - GetPreAreaSize(x1, y0) + GetPreAreaSize(x1, y1);
            return angle;
        }
        #endregion
    }
}