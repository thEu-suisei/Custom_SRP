//用来存放计算光照相关的方法
//HLSL编译保护机制
#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

//所有的include操作都放在LitPass.hlsl中


//计算物体表面接收到的光能量
float3 IncomingLight(Surface surface,Light light)
{
    return saturate(dot(surface.normal,light.direction)) * light.color;
}

//新增的GetLighting方法，传入surface和light，返回真正的光照计算结果，即物体表面最终反射出的RGB光能量
float3 GetLighting (Surface surface,Light light) {
    //物体表面接收到的光能量 * 物体表面Albedo（反射率）
    return IncomingLight(surface,light) * surface.color;
}

//GetLighting返回光照结果，这个GetLighting只传入一个surface
float3 GetLighting(Surface surface)
{
    float3 color = 0.0;

    for(int i =0;i<GetDirectionalLightCount();i++)
    {
        color+=GetLighting(surface,GetDirectionalLight(i));
    }

    return color;
    
    //光源从Light.hlsl的GetDirectionalLight获取
    // return GetLighting(surface,GetDirectionalLight());
}

#endif