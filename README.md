# Unity-APNG
 一个在Unity中播放APNG动画的插件

 ## 使用
 添加APNGPlayer脚本至GameObject对象下即可
 
 ## APNGPlayer参数说明
 * imagePath：APNG图片加载路径
 * imageSource：APNG图片来源
 * textureMode：Texture使用模式
 * materials：指定APNG图像所需要赋值的Material
 * rawImages：指定APNG图像所需要赋值的RawImage
 * runOnStart：是否脚本启动即加载
 * autoPlay：是否自动播放，为true则加载完成后立即开始播放，为false则需手动调用Play()才开始播放
 * playSpeed：播放速度倍率，值越大动画播放速度越快，默认1.0
 * maxLoopCount：动画最大循环次数，0表示无限制
 * isReady[ReadOnly]：判断是否已初始化完成
 * onReady[event]：加载完成事件通知
 * onError[event]：加载出错事件通知
 * onChanged[event]：动画帧变化事件通知
 
 ## APNGPlayer函数说明
 * Run()：开始加载
 * Clear()：清除掉已加载的数据，只有isReady为true时可调用
 * Play()：开始播放，只有isReady为true时可调用
 * Stop()：停止播放，只有isReady为true时可调用
 * Pause()：暂停播放，只有isReady为true时可调用
 * Restart()：重新开始播放
 
 ## Thirdparty
 [APNG.NET]https://github.com/xupefei/APNG.NET.git