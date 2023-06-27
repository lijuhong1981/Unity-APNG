using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using LibAPNG;

public class APNGPlayer : MonoBehaviour
{
    /// <summary>
    /// 图片来源
    /// </summary>
    public enum ImageSource
    {
        FromStreamingAssets,
        FromFile,
        FromHttp,
    }

    /// <summary>
    /// 加载状态
    /// </summary>
    public enum LoadState
    {
        UNLOADED,//未加载
        LOADING,//加载中
        PROCESSING,//处理中
        READY,//准备完成
        ERROR,//错误
    }

    /// <summary>
    /// 播放状态
    /// </summary>
    public enum PlayState
    {
        STOPED,//停止
        PLAYING,//播放
        PAUSED,//暂停
    }

    class APNGFrame
    {
        //当前帧索引
        public int index;
        public Frame frame;
        //当前帧图像数据
        public Color32[] pixels;
        //当前帧持续时间
        public float duration;
        //指定下一帧绘制之前对缓冲区的操作
        public DisposeOps disposeOp;
        //指定绘制当前帧之前对缓冲区的操作
        public BlendOps blendOp;
        //当前帧像素宽
        public uint width;
        //当前帧像素高
        public uint height;
        //当前帧x方向像素偏移
        public uint xOffset;
        //当前帧y方向像素偏移
        public uint yOffset;

        public APNGFrame clone()
        {
            var result = new APNGFrame();
            result.index = this.index;
            result.frame = this.frame;
            result.pixels = this.pixels;
            result.duration = this.duration;
            result.disposeOp = this.disposeOp;
            result.blendOp = this.blendOp;
            result.width = this.width;
            result.height = this.height;
            result.xOffset = this.xOffset;
            result.yOffset = this.yOffset;
            return result;
        }
    }

    class ImagePixels
    {
        private uint mWidth;
        public uint width
        {
            get => mWidth;
        }
        private uint mHeight;
        public uint height
        {
            get => mHeight;
        }
        private Color32[] mPixels;
        public Color32[] pixels
        {
            get => mPixels;
        }
        public ImagePixels(uint width, uint height)
        {
            mWidth = width;
            mHeight = height;
            Clear();
        }

        /// <summary>
        /// 清除所有像素点
        /// </summary>
        public void Clear()
        {
            mPixels = new Color32[mWidth * mHeight];
        }

        /// <summary>
        /// 清除一个矩形区域
        /// </summary>
        /// <param name="x">水平方向开始像素，从左到右顺序</param>
        /// <param name="y">垂直方向开始像素，从下到上顺序</param>
        /// <param name="width">像素宽</param>
        /// <param name="height">像素高</param>
        public void ClearRect(uint x, uint y, uint width, uint height)
        {
            //var startIndex = y * mWidth + x;
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var index = (y + j) * mWidth + x + i;
                    var color = mPixels[index];
                    color.r = 0;
                    color.g = 0;
                    color.b = 0;
                    color.a = 0;
                }
            }
        }

        /// <summary>
        /// 放入像素点数据
        /// </summary>
        /// <param name="pixels">像素点数据</param>
        /// <param name="x">水平方向开始像素，从左到右顺序</param>
        /// <param name="y">垂直方向开始像素，从下到上顺序</param>
        /// <param name="width">像素宽</param>
        /// <param name="height">像素高</param>
        public void SetPixels(Color32[] pixels, uint x, uint y, uint width, uint height)
        {
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var index = (y + j) * mWidth + x + i;
                    mPixels[index] = pixels[j * width + i];
                }
            }
        }

        /// <summary>
        /// 放入像素点数据
        /// </summary>
        /// <param name="pixels"></param>
        public void SetPixels(Color32[] pixels)
        {
            SetPixels(pixels, 0, 0, mWidth, mHeight);
        }

        /// <summary>
        /// 获取像素点数据
        /// </summary>
        /// <param name="x">水平方向开始像素，从左到右顺序</param>
        /// <param name="y">垂直方向开始像素，从上到下顺序</param>
        /// <param name="width">像素宽</param>
        /// <param name="height">像素高</param>
        /// <returns>像素点数据</returns>
        public Color32[] GetPixels(uint x, uint y, uint width, uint height)
        {
            var result = new Color32[width * height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var index = (y + j) * mWidth + x + i;
                    result[j * width + i] = mPixels[index];
                }
            }
            return result;
        }

        /// <summary>
        /// 获取像素点数据
        /// </summary>
        /// <returns>像素点数据</returns>
        public Color32[] GetPixels()
        {
            return GetPixels(0, 0, mWidth, mHeight);
        }
    }

    [Tooltip("APNG图片加载路径")]
    public string imagePath;
    [Tooltip("APNG图片来源")]
    public ImageSource imageSource;
    [Tooltip("指定APNG图像所需要赋值的Material")]
    public List<Material> materials = new List<Material>();
    [Tooltip("指定APNG图像所需要赋值的RawImage")]
    public List<RawImage> rawImages = new List<RawImage>();
    [Tooltip("是否随脚本启动执行")]
    public bool runOnStart = true;
    [Tooltip("是否自动播放，为true则加载完成后立即开始播放，为false则需手动调用Play()才开始播放")]
    public bool autoPlay = true;
    [Tooltip("播放速度倍率")]
    [Min(0.1f)]
    public float playSpeed = 1.0f;

    private LoadState mLoadState = LoadState.UNLOADED;
    public bool isUnloaded
    {
        get { return mLoadState == LoadState.UNLOADED; }
    }
    public bool isLoading
    {
        get { return mLoadState == LoadState.LOADING; }
    }
    public bool isProcessing
    {
        get { return mLoadState == LoadState.PROCESSING; }
    }
    public bool isReady
    {
        get { return mLoadState == LoadState.READY; }
    }
    public bool isError
    {
        get { return mLoadState == LoadState.ERROR; }
    }

    private PlayState mPlayState = PlayState.STOPED;
    public bool isStoped
    {
        get { return mPlayState == PlayState.STOPED; }
    }
    public bool isPlaying
    {
        get { return mPlayState == PlayState.PLAYING; }
    }
    public bool isPaused
    {
        get { return mPlayState == PlayState.PAUSED; }
    }

    private APNG mApng;
    private List<APNGFrame> mFrames = new List<APNGFrame>();
    private uint mWidth;
    public uint imageWidth
    {
        get { return mWidth; }
    }
    private uint mHeight;
    public uint imageHeight
    {
        get { return mHeight; }
    }
    private APNGFrame mPrevFrame;
    private Texture2D mTexture;
    public Texture2D texture
    {
        get { return mTexture; }
    }
    private ImagePixels mImagePixels;
    private float mLastTime = 0.0f;
    private int mCurrentFrameIndex = -1;
    public int currentFrameIndex
    {
        get { return mCurrentFrameIndex; }
    }
    public int framesNumber
    {
        get { return mFrames.Count; }
    }

    public delegate void OnReady(APNGPlayer player);

    public delegate void OnError(APNGPlayer player, string error);

    public event OnReady onReady;
    public event OnError onError;

    // Start is called before the first frame update
    void Start()
    {
        if (runOnStart)
        {
            Run();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isReady && isPlaying)
        {
            checkNextFrame();
        }
    }

    /// <summary>
    /// 开始运行
    /// </summary>
    public void Run()
    {
        if (imagePath == null)
        {
            Debug.LogWarning("The url is null, can't call Run() method.");
            return;
        }
        if (isUnloaded || isError)
            StartCoroutine(load());
        else
            Debug.LogWarning("This player load state is " + mLoadState + ", can't call Run() method.");
    }

    //加载图片并处理
    private IEnumerator load()
    {
        mLoadState = LoadState.LOADING;
        Uri uri;
        if (imageSource == ImageSource.FromStreamingAssets)
            uri = new Uri(Path.Combine(Application.streamingAssetsPath, imagePath));
        else
            uri = new Uri(imagePath);
        using (UnityWebRequest www = UnityWebRequest.Get(uri))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string error = "Get " + imagePath + " error: " + www.error;
                Debug.LogError(error);
                mLoadState = LoadState.ERROR;
                onError?.Invoke(this, error);
                yield break;
            }

            //开始数据处理
            mLoadState = LoadState.PROCESSING;

            try
            {
                //解析APNG图片数据
                mApng = new APNG(www.downloadHandler.data);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.Message);
                mLoadState = LoadState.ERROR;
                onError?.Invoke(this, e.Message);
                yield break;
            }
            yield return null;

            //获取图片宽高
            mWidth = (uint)mApng.IHDRChunk.Width;
            mHeight = (uint)mApng.IHDRChunk.Height;
            //生成Texture
            mTexture = new Texture2D(mApng.IHDRChunk.Width, mApng.IHDRChunk.Height);
            //生成ImagePixels
            mImagePixels = new ImagePixels(mWidth, mHeight);
            yield return null;

            mFrames.Clear();
            int count = 0;
            for (int i = 0; i < mApng.Frames.Length; i++)
            {
                var frame = mApng.Frames[i];

                //生成当前帧Texture
                var data = frame.GetStream().ToArray();
                var texture = new Texture2D(1, 1);
                texture.LoadImage(data);

                var apngFrame = new APNGFrame();
                apngFrame.index = i;
                apngFrame.frame = frame;
                //读取当前帧Texture像素数据
                apngFrame.pixels = texture.GetPixels32();
                //计算当前帧持续时间
                apngFrame.duration = (float)frame.fcTLChunk.DelayNum / (float)frame.fcTLChunk.DelayDen;
                apngFrame.disposeOp = frame.fcTLChunk.DisposeOp;
                apngFrame.blendOp = frame.fcTLChunk.BlendOp;
                apngFrame.width = frame.fcTLChunk.Width;
                apngFrame.height = frame.fcTLChunk.Height;
                apngFrame.xOffset = frame.fcTLChunk.XOffset;
                //计算yOffset，因Texture采用的是从下到上顺序，所以这里需要翻转yOffset
                apngFrame.yOffset = mHeight - (frame.fcTLChunk.YOffset + apngFrame.height);

                mFrames.Add(apngFrame);

                Destroy(texture);

                count++;
                //每处理10帧，执行一下yield return以避免线程阻塞
                if (count % 10 == 0)
                    yield return null;
            }
            //yield return null;

            mLoadState = LoadState.READY;
            Debug.Log("This player is ready.");
            onReady?.Invoke(this);

            //autoPlay为true，直接开始播放
            if (autoPlay)
            {
                Play();
            }
            //为false，设置当前动画为第一帧
            else
            {
                setCurrentFrameImpl(0);
            }
        }
    }

    /// <summary>
    /// 清除已加载的数据
    /// </summary>
    public void Clear()
    {
        if (!isReady)
        {
            Debug.LogWarning("This player is not ready, can't call Clear() method.");
            return;
        }
        mPlayState = PlayState.STOPED;
        mLoadState = LoadState.UNLOADED;
        mCurrentFrameIndex = -1;
        mFrames.Clear();
        mApng = null;
        if (mTexture != null)
        {
            Destroy(mTexture);
            mTexture = null;
        }
    }

    /// <summary>
    /// 开始播放
    /// </summary>
    public void Play()
    {
        if (!isReady)
        {
            Debug.LogWarning("This player is not ready, can't call Play() method.");
            return;
        }
        if (materials.Count == 0 && rawImages.Count == 0)
        {
            Debug.LogWarning("The materials and rawImages count is 0, can't call Play() method.");
            return;
        }
        if (isPlaying)
            return;
        if (mCurrentFrameIndex == -1)
            setCurrentFrameImpl(0);
        mLastTime = Time.time;
        mPlayState = PlayState.PLAYING;
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        if (!isReady)
        {
            Debug.LogWarning("This player is not ready, can't call Stop() method.");
            return;
        }
        if (isStoped)
            return;
        mPlayState = PlayState.STOPED;
        //恢复至第一帧
        setCurrentFrameImpl(0);
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void Pause()
    {
        if (!isReady)
        {
            Debug.LogWarning("This player is not ready, can't call Pause() method.");
            return;
        }
        if (isStoped || isPaused)
            return;
        mPlayState = PlayState.PAUSED;
    }

    //public void SetCurrentFrame(int index)
    //{
    //    if (!isReady)
    //    {
    //        Debug.LogWarning("This player is not ready, can't call SetCurrentFrame() method.");
    //        return;
    //    }
    //    if (material == null && rawImage == null)
    //    {
    //        Debug.LogWarning("The material and rawImage is null, can't call SetCurrentFrame() method.");
    //        return;
    //    }
    //    if (index < 0 || index >= this.framesNumber)
    //    {
    //        Debug.LogWarning("SetCurrentFrame error, index " + index + " is out of bounds [" + 0 + ", " + this.framesNumber + ")");
    //        return;
    //    }
    //    setCurrentFrameImpl(index);
    //}

    //设置当前帧
    private void setCurrentFrameImpl(int index)
    {
        if (mCurrentFrameIndex == index)
            return;
        mCurrentFrameIndex = index;
        var frame = mFrames[index];
        //第一帧
        if (index == 0)
        {
            //绘制第一帧前将动画整体区域清空
            mImagePixels.Clear();
            //置空上一帧
            mPrevFrame = null;
        }
        //存在上一帧
        if (mPrevFrame != null)
        {
            switch (mPrevFrame.disposeOp)
            {
                case DisposeOps.APNGDisposeOpNone://不作处理，直接绘制
                    break;
                case DisposeOps.APNGDisposeOpBackground://清空上一帧区域
                    mImagePixels.ClearRect(mPrevFrame.xOffset, mPrevFrame.yOffset, mPrevFrame.width, mPrevFrame.height);
                    break;
                case DisposeOps.APNGDisposeOpPrevious://恢复为上一帧绘制前的数据
                    mImagePixels.SetPixels(mPrevFrame.pixels, mPrevFrame.xOffset, mPrevFrame.yOffset, mPrevFrame.width, mPrevFrame.height);
                    break;
            }
        }
        mPrevFrame = frame.clone();
        //存储当前的绘制数据，用于下一帧绘制前恢复该数据
        if (mPrevFrame.disposeOp == DisposeOps.APNGDisposeOpPrevious)
            mPrevFrame.pixels = mImagePixels.GetPixels(mPrevFrame.xOffset, mPrevFrame.yOffset, mPrevFrame.width, mPrevFrame.height);
        //清空当前帧区域的数据
        if (mPrevFrame.blendOp == BlendOps.APNGBlendOpSource)
            mImagePixels.ClearRect(mPrevFrame.xOffset, mPrevFrame.yOffset, mPrevFrame.width, mPrevFrame.height);
        //绘制当前帧
        mImagePixels.SetPixels(frame.pixels, mPrevFrame.xOffset, mPrevFrame.yOffset, mPrevFrame.width, mPrevFrame.height);
        //将绘制好的数据设置给Texture
        mTexture.SetPixels32(mImagePixels.pixels);
        mTexture.Apply();
        //为Material与RawImage赋值
        foreach (var material in materials)
        {
            material.mainTexture = mTexture;
        }
        foreach (var rawImage in rawImages)
        {
            rawImage.texture = mTexture;
        }
    }

    //获取下一帧索引
    private int getNextFrameIndex()
    {
        var index = mCurrentFrameIndex;
        index++;
        if (index >= framesNumber)
            index = 0;
        return index;
    }

    //检查是否跳转下一帧
    private void checkNextFrame()
    {
        var nowTime = Time.time;
        if (nowTime - mLastTime >= mFrames[mCurrentFrameIndex].duration / playSpeed)
        {
            setCurrentFrameImpl(getNextFrameIndex());
            mLastTime = nowTime;
        }
    }
}
