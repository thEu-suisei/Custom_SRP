//用来定义光源属性
#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

//用CBuffer包裹构造方向光源的两个属性，cpu会每帧传递（修改）这两个属性到GPU的常量缓冲区，对于一次渲染过程这两个值恒定
CBUFFER_START(_CustomLight)
    float3 _DirectionalLightColor;
    float3 _DirectionalLightDirection;
CBUFFER_END

struct Light {
    //光源颜色
    float3 color;
    //光源方向：指向光源
    float3 direction;
};

//返回一个配置好的光源，初始化为Color白色，光线从上垂直向下投射（不明确坐标系，但由于教程中在世界空间下计算光照，因此这里多半指的是世界空间）
Light GetDirectionalLight () {
    Light light;
    light.color = _DirectionalLightColor;
    light.direction = _DirectionalLightDirection;
    return light;
}

#endif