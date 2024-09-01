using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//https://www.cnblogs.com/uwatech/p/15419757.html
public class MyComputeShaderTest : MonoBehaviour
{
    public ComputeShader mComputeShader;
    public Material material;

    ComputeBuffer mParticleDataBuffer;
    const int mParticleCount = 20000;

    private int kernelIndex;


    struct ParticleData
    {
        public Vector3 pos;//float3
        public Color color;//float4
    }
    
    void Start()
    {
        kernelIndex = mComputeShader.FindKernel("UpdateParticle");
        
        //Compute Buffer Part:
        //创建ComputeBuffer，用来给computeShader的数据初始化并传递给computeShader中的buffer。
        //count：表示buffer的元素数量
        //stride：表示元素所占用的空间，字节
        //struct中7个float，4*7=28个字节
        mParticleDataBuffer = new ComputeBuffer(mParticleCount,28);
        ParticleData[] particleDatas = new ParticleData[mParticleCount];
        //用SetData来填充buffer数据。
        mParticleDataBuffer.SetData(particleDatas);
        
        // //Compute Texture Part:
        // RenderTexture mRenderTexture = new RenderTexture(256, 256, 16);
        // //enableRandomWrite表示是否允许无序写入，在多线程时开启
        // mRenderTexture.enableRandomWrite=true;
        // //创建一张纹理
        // mRenderTexture.Create();
        // //将材质的纹理设置为会被computeShader处理的纹理。
        // material.mainTexture = mRenderTexture;
        // //设置mComputeShader要写入Tex
        // mComputeShader.SetTexture(kernelIndex,"Result",mRenderTexture);
        
    }

    // Update is called once per frame
    void Update()
    {
        
        //将Buffer传递给ComputeShader
        //name:对应computeShader中的属性
        mComputeShader.SetBuffer(kernelIndex,"ParticleBuffer",mParticleDataBuffer);
        
        mComputeShader.SetFloat("Time",Time.time);
        
        //Execute:
        //分成gx*gy*gz个线程组，来执行ComputeShader
        mComputeShader.Dispatch(kernelIndex, mParticleCount/1000, 1, 1);
        
        //给shader也传递buffer的数据
        material.SetBuffer("_particleDataBuffer",mParticleDataBuffer);
        
    }

    //该方法里我们可以自定义绘制几何。
    private void OnRenderObject()
    {
        material.SetPass(0);
        //我们可以用该方法绘制几何，第一个参数是拓扑结构，第二个参数数顶点数。
        Graphics.DrawProceduralNow(MeshTopology.Points,mParticleCount);
    }

    private void OnDestroy()
    {
        mParticleDataBuffer.Release();
        mParticleDataBuffer = null;
    }
}
