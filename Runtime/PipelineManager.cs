using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PipelineManager : MonoBehaviour
{
    public RenderPipelineAsset renderPipelineAsset;
    
    private RenderPipelineAsset _oldRenderPipelineAsset;
    
    public IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();

        _oldRenderPipelineAsset = GraphicsSettings.renderPipelineAsset;
        GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
    }

    public void OnDestroy()
    {
        GraphicsSettings.renderPipelineAsset = _oldRenderPipelineAsset;
    }
}
