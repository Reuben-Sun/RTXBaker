# RTXBaker

### 功能

Raytrace生成Cubemap，积分得到SH

![效果演示](image/效果演示.gif)

![烘焙面板](image/烘焙面板.jpg)

### 运行环境

- Unity 2021.3.6f1c1
- DX12 RTX2070s [Unity raytrace 硬件要求](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@13.1/manual/Ray-Tracing-Getting-Started.html#HardwareRequirements)
- Unity SRP

### 安装方法

打开Unity Package Manager，输入

```
https://github.com/Reuben-Sun/RTXBaker.git#upm
```

### 开发日志

[开发日志](https://github.com/Reuben-Sun/RTXBaker/blob/main/%E5%BC%80%E5%8F%91%E6%97%A5%E5%BF%97.md#%E5%BC%80%E5%8F%91%E6%97%A5%E5%BF%97)

### 开发指南

#### 提交方法

1. 在`master`分支进行开发测试
   - 提交前请保证无编译错误，Runtime不崩溃，且无严重影响性能的功能
   - 请修改`package.json`的版本号
2. 提交`upm`分支，具体指令

```
git subtree split --prefix=RTXBaker/Packages/com.reuben.rtx-baker --branch upm
git tag 1.0.x upm
git push origin upm --tags
```

### 参考

[RTIOW in Unity](https://github.com/zhing2006/GPU-Ray-Tracing-in-One-Weekend-by-Unity-2019.3#gpu-ray-tracing-in-one-weekend-by-unity-20193)

[RTIRL in Unity](https://github.com/zhing2006/GPU-Ray-Tracing-in-Rest-of-Your-Life-by-Unity-2019.3)

[UE4 IBL](https://cdn2-unrealengine-1251447533.file.myqcloud.com/Resources/files/2013SiggraphPresentationsNotes-26915738.pdf)

[3阶SH模拟Irradiance Map](https://zhuanlan.zhihu.com/p/476612991)

[CubeMap To SH](https://github.com/Crocs512/GAMES202-HW/blob/1b15139b633b39124670dd5cbe79ecb4124470c4/homework2/prt/src/prt.cpp)

[GAMES 202](https://games-cn.org/games202/)

[HDRP Sample](https://github.com/Unity-Technologies/HDRPRayTracingScenes)
