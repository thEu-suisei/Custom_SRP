//定义BRDF函数需要传入的参数BRDF
#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

//宏定义最小高光反射率
#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity(float metallic)
{
    //将漫反射反射率控制在[0,0.96]内
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

struct BRDF
{
    //物体表面漫反射颜色
    float3 diffuse;
    //物体表面高光颜色
    float3 specular;
    //物体表面粗糙度
    float3 roughness;
    //
    float perceptualRoughness;
    //菲涅尔项
    float fresnel;
};

BRDF GetBRDF(Surface surface,bool applyAlphaToDiffuse = false)
{
    BRDF brdf;

    //Reflectivity表示Specular反射率，oneMinusReflectivity表示Diffuse反射率
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    //diffuse等于物体表面不吸收的光能量color*（1-高光反射率）
    brdf.diffuse = surface.color * oneMinusReflectivity;
    //考虑Alpha Blend的材质，diffuse项Premultiplied Alpha
    if(applyAlphaToDiffuse)
    {
        brdf.diffuse *= surface.alpha;
    }
    
    //高光占比(specular)应该等于surface.color(物体不吸收的光能量，即用于反射的所有光能量)-brdf.diffuse（漫反射占比）
    //同时，高光占比越高，高光颜色越接近物体本身反射颜色，高光占比越低，高光颜色越接近白色，因此使用lerp
    brdf.specular = lerp(MIN_REFLECTIVITY,surface.color,surface.metallic);
    
    //先根据surface.smoothness计算出感知粗糙度，再将感知粗糙度转为实际粗糙度
    //函数的返回值就是(1-surface.smoothness)
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    //函数的返回的就是perceptualRoughness的平方
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

    //菲涅尔项
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
    
    return brdf;
}

//计算高光强度Specular Strength
float SpecularStrength(Surface surface,BRDF brdf,Light light)
{
    //SafeNormalize防止观察方向与物体表面法线完全反向时，其相加结果为0向量导致归一化时除以0
    float3 h = SafeNormalize(light.direction + surface.viewDirection);
    float nh2 = Square(saturate(dot(surface.normal,h)));
    float lh2 = Square(saturate(dot(light.direction,h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.0001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1,lh2) * normalization);
}

//计算反射出的总光能量比例（漫反射+高光）
float3 DirectBRDF(Surface surface,BRDF brdf,Light light)
{
    //观察角度接收到的高光能量 * 物体表面反射出的高光能量 + 各向均匀的漫反射能量
    return SpecularStrength(surface,brdf,light) * brdf.specular + brdf.diffuse;
}

//diffuse/specular:从全局照明获得的漫反射和镜面反射颜色
float3 IndirectBRDF(Surface surface,BRDF brdf , float3 diffuse , float3 specular)
{
    //菲涅尔强度
    float fresnelStrength = surface.fresnelStrength *Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
    float3 reflection = specular * lerp(brdf.specular,brdf.fresnel,fresnelStrength);
    //粗糙度会散射reflection
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return (diffuse*brdf.diffuse + reflection)*surface.occlusion;
}

#endif
