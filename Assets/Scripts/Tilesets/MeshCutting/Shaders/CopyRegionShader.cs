using UnityEngine;

namespace MeshCutting
{
    public class CopyRegionShader
    {
        private readonly ComputeShader _copyRegionShaderBilinear;
        private readonly ComputeShader _copyRegionShaderPoint;
        private readonly int _copyRegionShaderBilinearKernel;
        private readonly int _copyRegionShaderPointKernel;

        public CopyRegionShader()
        {
            _copyRegionShaderBilinear = Resources.Load<ComputeShader>("CopyRegionBilinear");
            _copyRegionShaderPoint = Resources.Load<ComputeShader>("CopyRegionPoint");
            _copyRegionShaderBilinearKernel = _copyRegionShaderBilinear.FindKernel("CopyRegionBilinear");
            _copyRegionShaderPointKernel = _copyRegionShaderPoint.FindKernel("CopyRegionPoint");
        }

        public void DispatchBilinear(
            Texture source,
            Vector4 sourceTextureTransform,
            float srcU,
            float srcV,
            float srcWidth,
            float srcHeight,
            Texture destination,
            int dstX,
            int dstY,
            int dstWidth,
            int dstHeight)
        {
            _copyRegionShaderBilinear.SetTexture(_copyRegionShaderBilinearKernel, "Source", source);
            _copyRegionShaderBilinear.SetVector("SourceTextureTransform", sourceTextureTransform);
            _copyRegionShaderBilinear.SetFloat("SrcU", srcU);
            _copyRegionShaderBilinear.SetFloat("SrcV", srcV);
            _copyRegionShaderBilinear.SetFloat("SrcWidth", srcWidth);
            _copyRegionShaderBilinear.SetFloat("SrcHeight", srcHeight);
            _copyRegionShaderBilinear.SetTexture(_copyRegionShaderBilinearKernel, "Destination", destination);
            _copyRegionShaderBilinear.SetInt("DstX", dstX);
            _copyRegionShaderBilinear.SetInt("DstY", dstY);
            _copyRegionShaderBilinear.SetInt("DstWidth", dstWidth);
            _copyRegionShaderBilinear.SetInt("DstHeight", dstHeight);
            
            _copyRegionShaderBilinear.Dispatch(
                _copyRegionShaderBilinearKernel,
                Mathf.CeilToInt(dstWidth / 8f), 
                Mathf.CeilToInt(dstHeight / 8f),  
                1);
        }
        
        public void DispatchPoint(
            Texture source,
            Vector4 sourceTextureTransform,
            float srcU,
            float srcV,
            float srcWidth,
            float srcHeight,
            Texture destination,
            int dstX,
            int dstY,
            int dstWidth,
            int dstHeight)
        {
            _copyRegionShaderPoint.SetTexture(_copyRegionShaderPointKernel, "Source", source);
            _copyRegionShaderPoint.SetVector("SourceTextureTransform", sourceTextureTransform);
            _copyRegionShaderPoint.SetFloat("SrcU", srcU);
            _copyRegionShaderPoint.SetFloat("SrcV", srcV);
            _copyRegionShaderPoint.SetFloat("SrcWidth", srcWidth);
            _copyRegionShaderPoint.SetFloat("SrcHeight", srcHeight);
            _copyRegionShaderPoint.SetTexture(_copyRegionShaderPointKernel, "Destination", destination);
            _copyRegionShaderPoint.SetInt("DstX", dstX);
            _copyRegionShaderPoint.SetInt("DstY", dstY);
            _copyRegionShaderPoint.SetInt("DstWidth", dstWidth);
            _copyRegionShaderPoint.SetInt("DstHeight", dstHeight);
            
            _copyRegionShaderPoint.Dispatch(
                _copyRegionShaderPointKernel,
                Mathf.CeilToInt(dstWidth / 8f), 
                Mathf.CeilToInt(dstHeight / 8f),  
                1);
        }
    }
}
