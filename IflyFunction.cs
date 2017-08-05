using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.DirectX.DirectSound;
using Microsoft.DirectX;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;


namespace Speak
{
    public class Speak
    {
    }
    #region ISR枚举常量
    public enum AudioStatus
    {
        ISR_AUDIO_SAMPLE_INIT = 0x00,
        ISR_AUDIO_SAMPLE_FIRST = 0x01,
        ISR_AUDIO_SAMPLE_CONTINUE = 0x02,
        ISR_AUDIO_SAMPLE_LAST = 0x04,
        ISR_AUDIO_SAMPLE_SUPPRESSED = 0x08,
        ISR_AUDIO_SAMPLE_LOST = 0x10,
        ISR_AUDIO_SAMPLE_NEW_CHUNK = 0x20,
        ISR_AUDIO_SAMPLE_END_CHUNK = 0x40,

        ISR_AUDIO_SAMPLE_VALIDBITS = 0x7f /* to validate the value of sample->status */
    }

    public enum EpStatus
    {
        ISR_EP_NULL = -1,
        ISR_EP_LOOKING_FOR_SPEECH = 0,          ///还没有检测到音频的前端点
        ISR_EP_IN_SPEECH = 1,                   ///已经检测到了音频前端点，正在进行正常的音频处理。
        ISR_EP_AFTER_SPEECH = 3,                ///检测到音频的后端点，后继的音频会被MSC忽略。
        ISR_EP_TIMEOUT = 4,                     ///超时
        ISR_EP_ERROR = 5,                       ///出现错误
        ISR_EP_MAX_SPEECH = 6                   ///音频过大
    }

    public enum RecogStatus
    {
        ISR_REC_NULL = -1,
        ISR_REC_STATUS_SUCCESS = 0,             ///识别成功，此时用户可以调用QISRGetResult来获取（部分）结果。
        ISR_REC_STATUS_NO_MATCH = 1,            ///识别结束，没有识别结果
        ISR_REC_STATUS_INCOMPLETE = 2,          ///正在识别中
        ISR_REC_STATUS_NON_SPEECH_DETECTED = 3, ///保留
        ISR_REC_STATUS_SPEECH_DETECTED = 4,     ///发现有效音频
        ISR_REC_STATUS_SPEECH_COMPLETE = 5,     ///识别结束
        ISR_REC_STATUS_MAX_CPU_TIME = 6,        ///保留
        ISR_REC_STATUS_MAX_SPEECH = 7,          ///保留
        ISR_REC_STATUS_STOPPED = 8,             ///保留
        ISR_REC_STATUS_REJECTED = 9,            ///保留
        ISR_REC_STATUS_NO_SPEECH_FOUND = 10     ///没有发现音频
    }
    #endregion


    #region ITT枚举常量
    /**
         *  MSPSampleStatus indicates how the sample buffer should be handled
         *  MSP_AUDIO_SAMPLE_FIRST		- The sample buffer is the start of audio
         *								  If recognizer was already recognizing, it will discard
         *								  audio received to date and re-start the recognition
         *  MSP_AUDIO_SAMPLE_CONTINUE	- The sample buffer is continuing audio
         *  MSP_AUDIO_SAMPLE_LAST		- The sample buffer is the end of audio
         *								  The recognizer will cease processing audio and
         *								  return results
         *  Note that sample statii can be combined; for example, for file-based input
         *  the entire file can be written with SAMPLE_FIRST | SAMPLE_LAST as the
         *  status.
         *  Other flags may be added in future to indicate other special audio
         *  conditions such as the presence of AGC
         */
    enum SampleStatus
    {
        MSP_AUDIO_SAMPLE_INIT = 0x00,
        MSP_AUDIO_SAMPLE_FIRST = 0x01,
        MSP_AUDIO_SAMPLE_CONTINUE = 0x02,
        MSP_AUDIO_SAMPLE_LAST = 0x04,
    };

    /*
     *  The enumeration MSPRecognizerStatus contains the recognition status
     *  MSP_REC_STATUS_SUCCESS				- successful recognition with partial results
     *  MSP_REC_STATUS_NO_MATCH				- recognition rejected
     *  MSP_REC_STATUS_INCOMPLETE			- recognizer needs more time to compute results
     *  MSP_REC_STATUS_NON_SPEECH_DETECTED	- discard status, no more in use
     *  MSP_REC_STATUS_SPEECH_DETECTED		- recognizer has detected audio, this is delayed status
     *  MSP_REC_STATUS_COMPLETE				- recognizer has return all result
     *  MSP_REC_STATUS_MAX_CPU_TIME			- CPU time limit exceeded
     *  MSP_REC_STATUS_MAX_SPEECH			- maximum speech length exceeded, partial results may be returned
     *  MSP_REC_STATUS_STOPPED				- recognition was stopped
     *  MSP_REC_STATUS_REJECTED				- recognizer rejected due to low confidence
     *  MSP_REC_STATUS_NO_SPEECH_FOUND		- recognizer still found no audio, this is delayed status
     */
    enum RecognizerStatus
    {
        MSP_REC_STATUS_SUCCESS = 0,
        MSP_REC_STATUS_NO_MATCH = 1,
        MSP_REC_STATUS_INCOMPLETE = 2,
        MSP_REC_STATUS_NON_SPEECH_DETECTED = 3,
        MSP_REC_STATUS_SPEECH_DETECTED = 4,
        MSP_REC_STATUS_COMPLETE = 5,
        MSP_REC_STATUS_MAX_CPU_TIME = 6,
        MSP_REC_STATUS_MAX_SPEECH = 7,
        MSP_REC_STATUS_STOPPED = 8,
        MSP_REC_STATUS_REJECTED = 9,
        MSP_REC_STATUS_NO_SPEECH_FOUND = 10,
        MSP_REC_STATUS_FAILURE = 1,
    };

    /**
     * The enumeration MSPepState contains the current endpointer state
     *  MSP_EP_LOOKING_FOR_SPEECH	- Have not yet found the beginning of speech
     *  MSP_EP_IN_SPEECH			- Have found the beginning, but not the end of speech
     *  MSP_EP_AFTER_SPEECH			- Have found the beginning and end of speech
     *  MSP_EP_TIMEOUT				- Have not found any audio till timeout
     *  MSP_EP_ERROR				- The endpointer has encountered a serious error
     *  MSP_EP_MAX_SPEECH			- Have arrive the max size of speech
     */
    enum epState
    {
        MSP_EP_LOOKING_FOR_SPEECH = 0,
        MSP_EP_IN_SPEECH = 1,
        MSP_EP_AFTER_SPEECH = 3,
        MSP_EP_TIMEOUT = 4,
        MSP_EP_ERROR = 5,
        MSP_EP_MAX_SPEECH = 6,
        MSP_EP_IDLE = 7  // internal state after stop and before start
    };

    /* Synthesizing process flags */
    enum Synthesizing
    {
        MSP_TTS_FLAG_STILL_HAVE_DATA = 1,
        MSP_TTS_FLAG_DATA_END = 2,
        MSP_TTS_FLAG_CMD_CANCELED = 4,
    };
    #endregion


    #region 人物声音枚举
    public enum VoiceName
    {

        //intp65引擎：
        xiaoyan,//（青年女声，普通话）
        xiaoyu,//（青年男声，普通话）

        //intp65_en引擎:
        Catherine,//（英文女声）
        henry,//（英文男声）

        //vivi21引擎：
        vixy,//（小燕，普通话）
        vixm,//（小梅，粤语）
        vixl,//（小莉，台湾普通话）
        vixr,//（小蓉，四川话）
        vixyun,//（小芸，东北话）
    }
    #endregion


    #region 委托，方便多线程调用

    /// <summary>
    /// 表示听写方法
    /// </summary>
    /// <param name="_login_configs">登录参数</param>
    /// <param name="_param1">听写相关参数</param>
    public delegate void VoiceToTextDelegate(string _login_configs, string _param1);

    /// <summary>
    /// 表示声音合成方法
    /// </summary>
    /// <param name="text">需要合成音频的文本</param>
    /// <param name="filename">输出音频位置</param>
    /// <param name="_params">音频相关参数</param>
    /// <param name="_login_configs">登录参数</param>
    public delegate void TextToVoiceDelegate(string text, string filename, string _params, string _login_configs);
    #endregion

    #region 录音相关类
    public class SoundRecorder
    {

        public List<byte> currentdata = new List<byte>();

        public List<byte> CurrentData
        {
            get { return currentdata; }
        }

        #region 成员数据
        private Capture mCapDev = null;              // 音频捕捉设备
        private CaptureBuffer mRecBuffer = null;     // 缓冲区对象
        private WaveFormat mWavFormat;               // 录音的格式

        private int mNextCaptureOffset = 0;         // 该次录音缓冲区的起始点
        private int mSampleCount = 0;               // 录制的样本数目

        private Notify mNotify = null;               // 消息通知对象
        public const int cNotifyNum = 16;           // 通知的个数
        private int mNotifySize = 0;                // 每次通知大小
        private int mBufferSize = 0;                // 缓冲队列大小
        private Thread mNotifyThread = null;                 // 处理缓冲区消息的线程
        private AutoResetEvent mNotificationEvent = null;    // 通知事件

        private string mFileName = string.Empty;     // 文件保存路径
        private FileStream mWaveFile = null;         // 文件流
        private BinaryWriter mWriter = null;         // 写文件
        #endregion



        #region 对外操作函数

        /// <summary>
        /// 清楚缓存数据
        /// </summary>
        public void ClearDate()
        {
            currentdata.Clear();
        }
        /// <summary>
        /// 构造函数,设定录音设备,设定录音格式.
        /// <summary>
        /// 
        public SoundRecorder()
        {
            // 初始化音频捕捉设备
            InitCaptureDevice();
            // 设定录音格式
            mWavFormat = CreateWaveFormat();
        }

        /// <summary>
        /// 创建录音格式,此处使用16bit,16KHz,Mono的录音格式
        /// <summary>
        private WaveFormat CreateWaveFormat()
        {
            WaveFormat format = new WaveFormat();
            format.FormatTag = WaveFormatTag.Pcm;   // PCM
            format.SamplesPerSecond = 16000;        // 采样率：16KHz
            format.BitsPerSample = 16;              // 采样位数：16Bit
            format.Channels = 1;                    // 声道：Mono
            format.BlockAlign = (short)(format.Channels * (format.BitsPerSample / 8));  // 单位采样点的字节数 
            format.AverageBytesPerSecond = format.BlockAlign * format.SamplesPerSecond;
            return format;
            // 按照以上采样规格，可知采样1秒钟的字节数为 16000*2=32000B 约为31K
        }

        /// <summary>
        /// 设定录音结束后保存的文件,包括路径
        /// </summary>
        /// <param name="filename">保存wav文件的路径名</param>
        public void SetFileName(string filename)
        {
            mFileName = filename;
        }

        /// <summary>
        /// 开始录音
        /// </summary>
        public void RecStart()
        {
            // 创建录音文件
            CreateSoundFile();
            // 创建一个录音缓冲区，并开始录音
            CreateCaptureBuffer();
            // 建立通知消息,当缓冲区满的时候处理方法
            InitNotifications();
            mRecBuffer.Start(true);
        }


        /// <summary>
        /// 停止录音
        /// </summary>
        public void RecStop()
        {
            mRecBuffer.Stop();      // 调用缓冲区的停止方法，停止采集声音
            if (null != mNotificationEvent)
                mNotificationEvent.Set();       //关闭通知
            mNotifyThread.Abort();  //结束线程
            RecordCapturedData();   // 将缓冲区最后一部分数据写入到文件中

            // 写WAV文件尾
            mWriter.Seek(4, SeekOrigin.Begin);
            mWriter.Write((int)(mSampleCount + 36));   // 写文件长度
            mWriter.Seek(40, SeekOrigin.Begin);
            mWriter.Write(mSampleCount);                // 写数据长度

            mWriter.Close();
            mWaveFile.Close();
            mWriter = null;
            mWaveFile = null;
        }
        #endregion


        #region 对内操作函数
        /// <summary>
        /// 初始化录音设备,此处使用主录音设备.
        /// </summary>
        /// <returns>调用成功返回true,否则返回false</returns>
        private bool InitCaptureDevice()
        {
            // 获取默认音频捕捉设备
            CaptureDevicesCollection devices = new CaptureDevicesCollection();  // 枚举音频捕捉设备
            Guid deviceGuid = Guid.Empty;

            if (devices.Count > 0)
                deviceGuid = devices[0].DriverGuid;
            else
            {
                //MessageBox.Show("系统中没有音频捕捉设备");
                return false;
            }

            // 用指定的捕捉设备创建Capture对象
            try
            {
                mCapDev = new Capture(deviceGuid);
            }
            catch (DirectXException e)
            {
                //MessageBox.Show(e.ToString());
                return false;
            }
            return true;
        }

        /// <summary>
        /// 创建录音使用的缓冲区
        /// </summary>
        private void CreateCaptureBuffer()
        {
            // 缓冲区的描述对象
            CaptureBufferDescription bufferdescription = new CaptureBufferDescription();
            if (null != mNotify)
            {
                mNotify.Dispose();
                mNotify = null;
            }
            if (null != mRecBuffer)
            {
                mRecBuffer.Dispose();
                mRecBuffer = null;
            }
            // 设定通知的大小,默认为1s钟
            mNotifySize = (1024 > mWavFormat.AverageBytesPerSecond / 8) ? 1024 : (mWavFormat.AverageBytesPerSecond / 8);
            mNotifySize -= mNotifySize % mWavFormat.BlockAlign;
            // 设定缓冲区大小
            mBufferSize = mNotifySize * cNotifyNum;
            // 创建缓冲区描述
            bufferdescription.BufferBytes = mBufferSize;
            bufferdescription.Format = mWavFormat;           // 录音格式
            // 创建缓冲区
            mRecBuffer = new CaptureBuffer(bufferdescription, mCapDev);
            mNextCaptureOffset = 0;
        }

        /// <summary>
        /// 初始化通知事件,将原缓冲区分成16个缓冲队列,在每个缓冲队列的结束点设定通知点.
        /// </summary>
        /// <returns>是否成功</returns>
        private bool InitNotifications()
        {
            if (null == mRecBuffer)
            {
                //MessageBox.Show("未创建录音缓冲区");
                return false;
            }
            // 创建一个通知事件,当缓冲队列满了就激发该事件.
            mNotificationEvent = new AutoResetEvent(false);
            // 创建一个线程管理缓冲区事件
            if (null == mNotifyThread)
            {
                mNotifyThread = new Thread(new ThreadStart(WaitThread));
                mNotifyThread.Start();
            }
            // 设定通知的位置
            BufferPositionNotify[] PositionNotify = new BufferPositionNotify[cNotifyNum + 1];
            for (int i = 0; i < cNotifyNum; i++)
            {
                PositionNotify[i].Offset = (mNotifySize * i) + mNotifySize - 1;
                PositionNotify[i].EventNotifyHandle = mNotificationEvent.SafeWaitHandle.DangerousGetHandle();
            }
            mNotify = new Notify(mRecBuffer);
            mNotify.SetNotificationPositions(PositionNotify, cNotifyNum);
            return true;
        }

        /// <summary>
        /// 接收缓冲区满消息的处理线程
        /// </summary>
        private void WaitThread()
        {
            while (true)
            {
                // 等待缓冲区的通知消息
                mNotificationEvent.WaitOne(Timeout.Infinite, true);
                // 录制数据
                RecordCapturedData();
            }
        }

        /// <summary>
        /// 将录制的数据写入wav文件
        /// </summary>
        private void RecordCapturedData()
        {
            byte[] CaptureData = null;
            int ReadPos = 0, CapturePos = 0, LockSize = 0;
            mRecBuffer.GetCurrentPosition(out CapturePos, out ReadPos);
            LockSize = ReadPos - mNextCaptureOffset;
            if (LockSize < 0)       // 因为是循环的使用缓冲区，所以有一种情况下为负：当文以载读指针回到第一个通知点，而Ibuffeoffset还在最后一个通知处
                LockSize += mBufferSize;
            LockSize -= (LockSize % mNotifySize);   // 对齐缓冲区边界,实际上由于开始设定完整,这个操作是多余的.
            if (0 == LockSize)
                return;

            // 读取缓冲区内的数据
            CaptureData = (byte[])mRecBuffer.Read(mNextCaptureOffset, typeof(byte), LockFlag.None, LockSize);
            lock (currentdata)
            {
                currentdata.AddRange(CaptureData);
            }

            // 写入Wav文件
            mWriter.Write(CaptureData, 0, CaptureData.Length);
            // 更新已经录制的数据长度.
            mSampleCount += CaptureData.Length;
            // 移动录制数据的起始点,通知消息只负责指示产生消息的位置,并不记录上次录制的位置
            mNextCaptureOffset += CaptureData.Length;
            mNextCaptureOffset %= mBufferSize; // Circular buffer
        }

        /// <summary>
        /// 创建保存的波形文件,并写入必要的文件头.
        /// </summary>
        private void CreateSoundFile()
        {
            // Open up the wave file for writing.
            mWaveFile = new FileStream(mFileName, FileMode.Create);
            mWriter = new BinaryWriter(mWaveFile);
            /************************************************************************** 
               Here is where the file will be created. A 
               wave file is a RIFF file, which has chunks 
               of data that describe what the file contains. 
               A wave RIFF file is put together like this: 
               The 12 byte RIFF chunk is constructed like this: 
               Bytes 0 - 3 :  'R' 'I' 'F' 'F' 
               Bytes 4 - 7 :  Length of file, minus the first 8 bytes of the RIFF description. 
                                 (4 bytes for "WAVE" + 24 bytes for format chunk length + 
                                 8 bytes for data chunk description + actual sample data size.) 
                Bytes 8 - 11: 'W' 'A' 'V' 'E' 
                The 24 byte FORMAT chunk is constructed like this: 
                Bytes 0 - 3 : 'f' 'm' 't' ' ' 
                Bytes 4 - 7 : The format chunk length. This is always 16. 
                Bytes 8 - 9 : File padding. Always 1. 
                Bytes 10- 11: Number of channels. Either 1 for mono,  or 2 for stereo. 
                Bytes 12- 15: Sample rate. 
                Bytes 16- 19: Number of bytes per second. 
                Bytes 20- 21: Bytes per sample. 1 for 8 bit mono, 2 for 8 bit stereo or 
                                16 bit mono, 4 for 16 bit stereo. 
                Bytes 22- 23: Number of bits per sample. 
                The DATA chunk is constructed like this: 
                Bytes 0 - 3 : 'd' 'a' 't' 'a' 
                Bytes 4 - 7 : Length of data, in bytes. 
                Bytes 8 -: Actual sample data. 
              ***************************************************************************/
            // Set up file with RIFF chunk info.
            char[] ChunkRiff = { 'R', 'I', 'F', 'F' };
            char[] ChunkType = { 'W', 'A', 'V', 'E' };
            char[] ChunkFmt = { 'f', 'm', 't', ' ' };
            char[] ChunkData = { 'd', 'a', 't', 'a' };

            short shPad = 1;                // File padding
            int nFormatChunkLength = 0x10;  // Format chunk length.
            int nLength = 0;                // File length, minus first 8 bytes of RIFF description. This will be filled in later.
            short shBytesPerSample = 0;     // Bytes per sample.

            // 一个样本点的字节数目
            if (8 == mWavFormat.BitsPerSample && 1 == mWavFormat.Channels)
                shBytesPerSample = 1;
            else if ((8 == mWavFormat.BitsPerSample && 2 == mWavFormat.Channels) || (16 == mWavFormat.BitsPerSample && 1 == mWavFormat.Channels))
                shBytesPerSample = 2;
            else if (16 == mWavFormat.BitsPerSample && 2 == mWavFormat.Channels)
                shBytesPerSample = 4;

            // RIFF 块
            mWriter.Write(ChunkRiff);
            mWriter.Write(nLength);
            mWriter.Write(ChunkType);

            // WAVE块
            mWriter.Write(ChunkFmt);
            mWriter.Write(nFormatChunkLength);
            mWriter.Write(shPad);
            mWriter.Write(mWavFormat.Channels);
            mWriter.Write(mWavFormat.SamplesPerSecond);
            mWriter.Write(mWavFormat.AverageBytesPerSecond);
            mWriter.Write(shBytesPerSample);
            mWriter.Write(mWavFormat.BitsPerSample);

            // 数据块
            mWriter.Write(ChunkData);
            mWriter.Write((int)0);   // The sample length will be written in later.
        }
        #endregion
    }

    #endregion

    #region 语音识别类

    public class SpeakFunction
    {


        #region 私有字段

        private string quitState;

        bool isstop;

        struct wave_pcm_hdr
        {
            public char[] riff;                        // = "RIFF"
            public int size_8;                         // = FileSize - 8
            public char[] wave;                        // = "WAVE"
            public char[] fmt;                       // = "fmt "
            public int dwFmtSize;                      // = 下一个结构体的大小 : 16

            public short format_tag;              // = PCM : 1
            public short channels;                       // = 通道数 : 1
            public int samples_per_sec;        // = 采样率 : 8000 | 6000 | 11025 | 16000
            public int avg_bytes_per_sec;      // = 每秒字节数 : dwSamplesPerSec * wBitsPerSample / 8
            public short block_align;            // = 每采样点字节数 : wBitsPerSample / 8
            public short bits_per_sample;         // = 量化比特数: 8 | 16

            public char[] data;                        // = "data";
            public int data_size;                // = 纯数据长度 : FileSize - 44 



        } ;

        //默认音频头部数据
        wave_pcm_hdr default_pcmwavhdr = new wave_pcm_hdr
        {
            riff = new char[] { 'R', 'I', 'F', 'F' },
            size_8 = 0,
            wave = new char[] { 'W', 'A', 'V', 'E' },
            fmt = new char[] { 'f', 'm', 't', ' ' },
            dwFmtSize = 16,
            format_tag = 1,
            channels = 1,
            samples_per_sec = 16000,
            avg_bytes_per_sec = 32000,
            block_align = 2,
            bits_per_sample = 16,
            data = new char[] { 'd', 'a', 't', 'a' },
            data_size = 0
        };


        #endregion

        #region 公共事件

        public delegate void MyEventHandler(string info);

        /// <summary>
        /// 需要发送消息如“停止录音，正在转换等”触发该事件
        /// </summary>
        public event MyEventHandler ShowInfomation;

        /// <summary>
        /// 部分录音转换成文本后触发该事件
        /// </summary>
        public event MyEventHandler DataReceive;

        /// <summary>
        /// 声音转换文本，停止后触发该事件
        /// </summary>
        public event EventHandler VoiceToTextStopEven;

        /// <summary>
        /// 文本合成声音停止后，触发该事件
        /// </summary>
        public event EventHandler TextToVoiceStopEven;

        #endregion

        #region 内部方法



        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr QISRSessionBegin(string grammarList, string _params, ref int errorCode);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QISRGrammarActivate(string sessionID, string grammar, string type, int weight);


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QISRAudioWrite(string sessionID, byte[] waveData, uint waveLen, AudioStatus audioStatus, ref  EpStatus epStatus, ref RecogStatus recogStatus);


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr QISRGetResult(string sessionID, ref RecogStatus rsltStatus, int waitTime, ref int errorCode);


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QISRSessionEnd(string sessionID, string hints);


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QISRGetParam(string sessionID, string paramName, string paramValue, ref uint valueLen);


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QISRFini();


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr QISRUploadData(string sessionID, string dataName, byte[] userData, uint lenght, string paramValue, ref int errorCode);


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int MSPLogin(string usr, string pwd, string _params);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int MSPLogout();


        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr QTTSSessionBegin(string _params, ref int errorCode);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QTTSTextPut(string sessionID, string text, uint textLen, string _params);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr QTTSAudioGet(string sessionID, ref uint audioLen, ref int synthStatus, ref int errorCode);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr QTTSAudioInfo(string sessionID, ref int errorCode);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int QTTSSessionEnd(string sessionID, string hints);

        [DllImport("msc.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MSPUploadData(string filename, byte[] UserData, uint len, string param, ref int ret);



        private string PtrToStr(IntPtr p)
        {
            return Marshal.PtrToStringAnsi(p);
        }

        private int upload_user_vocabulary(string filename, int mode, ref string testID)
        {
            if (filename == null)
                return -1;//文件名为空,不上传;

            FileStream fs = new FileStream(filename, FileMode.OpenOrCreate);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);


            uint len = (uint)fs.Length;
            int ret = -1;
            byte[] UserData = new byte[len + 1];
            Encoding.Default.GetString(UserData);
            br.Read(UserData, 0, (int)len);
            UserData[len] = 0;
            br.Close();
            fs.Close();
            br.Dispose();
            fs.Dispose();
            //听写模式用户词典
            if (mode == 0)
            {
                testID = null;//上传用户词典时，testID无意义
                PtrToStr(MSPUploadData("userwords", UserData, len, "dtt=userword,sub=uup", ref ret));
            }
            //识别模式关键字
            else if (mode == 1)
            {
                if (testID != null)//使用服务器上已经传上的关键词
                    return 0;
                testID = PtrToStr(MSPUploadData("userwords", UserData, len, "dtt = userword, sub = asr", ref ret));
            }
            if (ret != 0)
            {
                if (ShowInfomation != null) ShowInfomation("出错代码" + ret.ToString());
                return ret;
            }
            return ret;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 停止识别
        /// </summary>
        public void Stop_Translate()
        {
            isstop = true;
        }

        #region 语音转文本
        /// <summary>
        /// 根据传入的音频文件名，转换成字符，用户需订阅DataReceive事件接收数据
        /// </summary>
        /// <param name="filename">音频文件名</param>
        public void IatModeTranslate(string filename)
        {
            string login_configs = "appid = 58d376fc, work_dir =   .  ";//登录参数
            string param1 = "sub=iat,ssm=1,auf=audio/L16;rate=16000,aue=speex-wb;7,ent=sms16k,rst=plain,rse=gb2312";
            TranslateVoiceFile(login_configs, param1, filename);
        }

        /// <summary>
        /// 根据传入的音频文件名，转换成字符，用户需订阅DataReceive事件接收数据
        /// </summary>
        /// <param name="filename">音频文件名</param>
        /// <param name="userwordFileName">用户词典名</param>
        public void IatModeTranslate(string filename, string userwordFileName)
        {
            string login_configs = "dvc=Name32890,appid = 58d376fc, work_dir =   .  ";//登录参数
            string param1 = "sub=iat,ssm=1,auf=audio/L16;rate=16000,aue=speex-wb;7,ent=sms16k,rst=plain,rse=gb2312";
            TranslateVoiceFile(login_configs, param1, filename, 0, userwordFileName);
        }

        /// <summary>
        /// 根据传入的音频文件名，根据关键词识别语义,用户需订阅DataReceive事件接收数据
        /// </summary>
        /// <param name="filename">音频文件名</param>
        /// <param name="userwordFileName">语义识别关键词</param>
        /// <param name="testId">服务器上存储的语法ID(可为null)</param>
        public void AsrModeTranslate(string filename, string userwordFileName, string testId)
        {
            string login_config = "appid = 58d376fc, work_dir =   .  ";//登录参数
            string param = "sub=asr,ssm=1,aue=speex-wb,auf=audio/L16;rate=16000,rst=xml,ent=sms16k";//注意sub=asr
            TranslateVoiceFile(login_config, param, filename, 1, userwordFileName);
        }

        /// <summary>
        /// 根据传入的音频文件名，根据语法识别语义,用户需订阅DataReceive事件接收数据
        /// </summary>
        /// <param name="filename">音频文件名</param>
        /// <param name="gramFileName">语法文件名</param>
        public void AsrModeTranslate(string filename, string gramFileName)
        {
            string login_config = "appid = 58d376fc, work_dir =   .  ";//登录参数
            string param = "sub=asr,ssm=1,aue=speex-wb,auf=audio/L16;rate=16000,rst=xml,ent=sms16k";//注意sub=asr
            TranslateVoiceFile(login_config, param, filename, 1, null, null, gramFileName);
        }
        /// <summary>
        /// 根据传入的音频文件名，根据语法和关键词，识别语义,用户需订阅DataReceive事件接收数据
        /// </summary>
        /// <param name="filename">文件名</param>
        /// <param name="userwordFileName">关键词</param>
        /// <param name="testId">服务器上存储的语法ID(可为null)</param>
        /// <param name="gramFileName">语法文件名</param>
        public void AsrModeTranslate(string filename, string userwordFileName, string testId, string gramFileName)
        {
            string login_config = "appid = 58d376fc, work_dir =   .  ";//登录参数
            string param = "sub=asr,ssm=1,aue=speex-wb,auf=audio/L16;rate=16000,rst=xml,ent=sms16k";//注意sub=asr
            TranslateVoiceFile(login_config, param, filename, 1, userwordFileName, testId, gramFileName);
        }

        /// <summary>
        ///  将声音文件转换成文字或语义识别
        /// </summary>
        /// <param name="_login_configs">登录参数</param>
        /// <param name="_param1">合成文字参数</param>
        /// <param name="filename">声音源文件</param>
        /// <param name="userwordType">字典类型,
        /// 0:听写用户词典，需要登录参数中指定词典名，且为听写模式。
        /// 1:识别模式时，需要为语义识别模式。                                   
        /// </param>
        /// <param name="userword">用户词典/关键词 文件名</param>
        /// <param name="testId">语法ID(关键词在服务器的ID)，上传后可获得</param>
        /// <param name="gramname">语法文件名</param>
        public void TranslateVoiceFile(string _login_configs, string _param1, string filename,
                    int userwordType = 0, string userword = null, string testId = null, string gramname = null)
        {

            FileStream fs = new FileStream(filename, FileMode.OpenOrCreate);
            BinaryReader br = new BinaryReader(fs);
            fs.Seek(44, 0);
            byte[] data = new byte[fs.Length];
            br.Read(data, 0, (int)fs.Length);


            AudioStatus audStat = AudioStatus.ISR_AUDIO_SAMPLE_CONTINUE;
            EpStatus epStatus = EpStatus.ISR_EP_NULL;
            RecogStatus recStatus = RecogStatus.ISR_REC_NULL;



            string login_configs = _login_configs;
            string param1 = _param1;
            string sessionID = null;
            int errCode = 0;
            long len = 6400;
            long write_len = 0;
            long totalLen = data.Count();

            //登陆
            MSPLogin(null, null, login_configs);

            //用户词典或关键字上传
            if (userword != null)
                upload_user_vocabulary(userword, userwordType, ref testId);

            //开始一路会话   
            sessionID = PtrToStr(QISRSessionBegin(testId, param1, ref errCode));
            if (sessionID == null)
                if (ShowInfomation != null) ShowInfomation("APPID不正确或与msc.dll不匹配");

            //激活语法
            if (gramname != null)
            {
                FileStream gramfs = new FileStream(gramname, FileMode.OpenOrCreate);
                StreamReader grmsr = new StreamReader(gramfs, Encoding.Default);

                string gram = grmsr.ReadToEnd();

                int result = QISRGrammarActivate(sessionID, gram, null, 0);

                grmsr.Close();
                gramfs.Close();
                gramfs.Dispose();
                grmsr.Dispose();

            }

            //开始正式转换
            while (audStat != AudioStatus.ISR_AUDIO_SAMPLE_LAST)
            {

                audStat = AudioStatus.ISR_AUDIO_SAMPLE_CONTINUE;
                if (epStatus == EpStatus.ISR_EP_NULL)
                    audStat = AudioStatus.ISR_AUDIO_SAMPLE_FIRST;

                if ((totalLen - write_len) <= len)
                {
                    len = (totalLen - write_len);
                    audStat = AudioStatus.ISR_AUDIO_SAMPLE_LAST;
                }

                byte[] dataTemp = new byte[len];

                Array.Copy(data, write_len, dataTemp, 0, len);

                QISRAudioWrite(sessionID, dataTemp, (uint)len, audStat, ref epStatus, ref recStatus);

                if (recStatus == RecogStatus.ISR_REC_STATUS_SUCCESS)
                {
                    string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));//服务端已经有识别结果，可以获取
                    if (null != rslt)
                        if (DataReceive != null) DataReceive(rslt);
                    System.Threading.Thread.Sleep(10);
                }
                write_len += len;
                if (epStatus == EpStatus.ISR_EP_AFTER_SPEECH)
                {
                    break;
                }

                System.Threading.Thread.Sleep(100);

            }
            QISRAudioWrite(sessionID, new byte[1], 0, AudioStatus.ISR_AUDIO_SAMPLE_LAST, ref epStatus, ref recStatus);
            while (recStatus != RecogStatus.ISR_REC_STATUS_SPEECH_COMPLETE && 0 == errCode)
            {
                string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));
                if (null != rslt)
                    if (DataReceive != null) DataReceive(rslt);
                System.Threading.Thread.Sleep(30);
            }
            QISRSessionEnd(sessionID, "normal");
            MSPLogout();
            if (ShowInfomation != null) ShowInfomation(string.Format("Error Code:{0}\n", errCode));

            if (ShowInfomation != null) ShowInfomation("转换结束");
            if (VoiceToTextStopEven != null) VoiceToTextStopEven(this, new EventArgs());

            br.Close();
            fs.Close();
            fs.Dispose();

        }


        /// <summary>
        /// 将录取的录音持续转换成文本直到Stop_Translate方法被执行
        /// </summary>
        /// <param name="_login_configs"> 登录参数,需求appid </param>
        /// <param name="_param1"> QISRSessionBegin转换相关参数，详见使用手册 </param>
        public void Begin_Translate1(string _login_configs, string _param1)
        {

            Console.WriteLine("Start");
            // MscNet mn = new MscNet(null, null, null, null);
            SoundRecorder sr = new SoundRecorder();//录音相关类

            //参数
            AudioStatus audStat = AudioStatus.ISR_AUDIO_SAMPLE_CONTINUE;
            EpStatus epStatus = EpStatus.ISR_EP_NULL;
            RecogStatus recStatus = RecogStatus.ISR_REC_NULL;



            string login_configs = _login_configs;
            string param1 = _param1;
            string sessionID = null;
            int errCode = 0;


            isstop = false;//开始停止属性        





            sr.SetFileName("output.wav");//设置录音存储文件
            sr.RecStart();//开始录音
            MSPLogin(null, null, login_configs);//登陆


            sessionID = PtrToStr(QISRSessionBegin(null, param1, ref errCode));//开始一路会话   
            while (!isstop)
            {


                System.Threading.Thread.Sleep(100);

                byte[] data = new byte[sr.CurrentData.Count];
                data = sr.CurrentData.ToArray<byte>();
                lock (sr.CurrentData)
                {
                    sr.ClearDate();
                }


                int len = data.Count();
                audStat = AudioStatus.ISR_AUDIO_SAMPLE_CONTINUE;

                QISRAudioWrite(sessionID, data, (uint)len, audStat, ref epStatus, ref recStatus);

                if (recStatus == RecogStatus.ISR_REC_STATUS_SUCCESS)
                {
                    string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));//服务端已经有识别结果，可以获取
                    if (null != rslt)
                        if (DataReceive != null) DataReceive(rslt);
                    System.Threading.Thread.Sleep(10);
                }
                if (epStatus == EpStatus.ISR_EP_AFTER_SPEECH)
                {


                    while (recStatus != RecogStatus.ISR_REC_STATUS_SPEECH_COMPLETE && 0 == errCode)
                    {
                        string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));
                        if (null != rslt)
                            if (DataReceive != null) DataReceive(rslt); ;
                        System.Threading.Thread.Sleep(10);
                    }
                    QISRSessionEnd(sessionID, "Wraing");
                    System.Threading.Thread.Sleep(30);
                    sessionID = PtrToStr(QISRSessionBegin(null, param1, ref errCode));//开始一路会话

                    if (sessionID == null)
                    {
                        if (ShowInfomation != null) ShowInfomation("错误,可能由于appid与msc.dll不匹配，或appid不存在");
                        return;
                    }
                }
            }
            QISRAudioWrite(sessionID, new byte[1], 0, AudioStatus.ISR_AUDIO_SAMPLE_LAST, ref epStatus, ref recStatus);
            while (recStatus != RecogStatus.ISR_REC_STATUS_SPEECH_COMPLETE && 0 == errCode)
            {
                string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));
                if (null != rslt)
                    if (DataReceive != null) DataReceive(rslt);
                System.Threading.Thread.Sleep(30);
            }
            QISRSessionEnd(sessionID, "normal");
            MSPLogout();
            sr.RecStop();

            if (ShowInfomation != null) ShowInfomation("录音结束");
            if (VoiceToTextStopEven != null) VoiceToTextStopEven(this, new EventArgs());
            byte[] mb = new byte[10];
            mb.Contains((byte)(7 * 16 + 11));

        }

        /// <summary>
        /// 将录取的录音持续转换成文本,自动识别结尾或Stop_Translate方法被执行时停止
        /// </summary>
        /// <param name="_login_configs">登录参数,需求appid</param>
        /// <param name="_param1">QISRSessionBegin转换相关参数，详见使用手册</param>
        public void Begin_Translate2(string _login_configs, string _param1)
        {
            string login_configs = _login_configs;//登录参数
            string param1 = _param1;


            // MscNet mn = new MscNet(null, null, null, null);
            SoundRecorder sr = new SoundRecorder();
            AudioStatus audStat = AudioStatus.ISR_AUDIO_SAMPLE_CONTINUE;
            EpStatus epStatus = EpStatus.ISR_EP_NULL;
            RecogStatus recStatus = RecogStatus.ISR_REC_NULL;

            string sessionID = null;
            int errCode = 0;
            quitState = "normal";
            isstop = false;


            sr.SetFileName("output.wav");
            sr.RecStart();
            MSPLogin(null, null, login_configs);

            sessionID = PtrToStr(QISRSessionBegin(null, param1, ref errCode));//开始一路会话
            if (sessionID == null)
            {
                if (ShowInfomation != null) ShowInfomation("错误,可能由于appid与msc.dll不匹配，或appid不存在");
                return;
            }

            while (audStat != AudioStatus.ISR_AUDIO_SAMPLE_LAST)
            {
                if (epStatus != EpStatus.ISR_EP_NULL)
                    audStat = AudioStatus.ISR_AUDIO_SAMPLE_CONTINUE;

                if (isstop)//在传输数据期间点击按钮
                {
                    audStat = AudioStatus.ISR_AUDIO_SAMPLE_LAST;
                    break;
                }

                System.Threading.Thread.Sleep(100);
                if (isstop)////在延时获取数据期间点击按钮
                {
                    audStat = AudioStatus.ISR_AUDIO_SAMPLE_LAST;
                }

                byte[] data = new byte[sr.CurrentData.Count];
                lock (sr.currentdata)
                {
                    data = sr.CurrentData.ToArray<byte>();
                    sr.ClearDate();
                }

                int len = data.Count();
                QISRAudioWrite(sessionID, data, (uint)len, audStat, ref epStatus, ref recStatus);//向讯飞服务端发送音频                  
                if (recStatus == RecogStatus.ISR_REC_STATUS_SUCCESS)
                {
                    string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));//服务端已经有识别结果，可以获取
                    if (null != rslt)
                        if (DataReceive != null) DataReceive(rslt);//触发数据接收事件
                }
                if (epStatus == EpStatus.ISR_EP_AFTER_SPEECH)//服务端判断音频结束
                {
                    break;
                }
            }

            QISRAudioWrite(sessionID, new byte[1], 0, AudioStatus.ISR_AUDIO_SAMPLE_LAST, ref epStatus, ref recStatus);
            sr.RecStop();
            if (ShowInfomation != null) ShowInfomation("录音结束，请等待转换结果");

            while (recStatus != RecogStatus.ISR_REC_STATUS_SPEECH_COMPLETE && 0 == errCode)
            {
                string rslt = PtrToStr(QISRGetResult(sessionID, ref recStatus, 0, ref errCode));
                if (null != rslt)
                    if (DataReceive != null) DataReceive(rslt); //触发数据接收事件
                System.Threading.Thread.Sleep(30);
            }
            //QISRSessionEnd(sessionID, quitState);
            MSPLogout();
            if (ShowInfomation != null) ShowInfomation("完成");
            if (VoiceToTextStopEven != null) VoiceToTextStopEven(this, new EventArgs());
            sr.ClearDate();
        }

        #endregion


        #region 文本合成语音

        /// <summary>
        /// 将文本合成语音。
        /// </summary>
        /// <param name="text">要转换的文本</param>
        /// <param name="filename">输出的音频</param>
        /// <param name="_params">输出音频的参数，详细使用见手册</param>
        /// <param name="_login_configs">登录参数，需求appid</param>
        public void Begin_ProcessVoice(string text, string filename, string _params, string _login_configs)//合成声音
        {

            string login_configs = _login_configs;//登录参数
            MSPLogin(null, null, login_configs);


            wave_pcm_hdr pcmwavhdr = default_pcmwavhdr;
            string sess_id = null;
            int ret = -1;
            uint text_len = 0; ;
            uint audio_len = 0;
            int synth_status = (int)Synthesizing.MSP_TTS_FLAG_STILL_HAVE_DATA;



            byte[] byteText = Encoding.Default.GetBytes(text);//计算字节数
            text_len = (uint)byteText.Count();

            sess_id = PtrToStr(QTTSSessionBegin(_params, ref ret));
            if (sess_id == null)
            {
                if (ShowInfomation != null) ShowInfomation("Appid出现问题！");
                return;
            }
            FileStream fs = new FileStream(filename, FileMode.Create);
            BinaryWriter sw = new BinaryWriter(fs, Encoding.Default);


            sw.Write(default_pcmwavhdr.riff);                        // = "RIFF"
            sw.Write(default_pcmwavhdr.size_8);                        // = FileSize - 8
            sw.Write(default_pcmwavhdr.wave);                        // = "WAVE"
            sw.Write(default_pcmwavhdr.fmt);
            sw.Write(default_pcmwavhdr.dwFmtSize);
            sw.Write(default_pcmwavhdr.format_tag);
            sw.Write(default_pcmwavhdr.channels);
            sw.Write(default_pcmwavhdr.samples_per_sec);
            sw.Write(default_pcmwavhdr.avg_bytes_per_sec);
            sw.Write(default_pcmwavhdr.block_align);
            sw.Write(default_pcmwavhdr.bits_per_sample);
            sw.Write(default_pcmwavhdr.data);
            sw.Write(default_pcmwavhdr.data_size);




            ret = QTTSTextPut(sess_id, text, text_len, null);
            while (true)
            {
                IntPtr ptr = QTTSAudioGet(sess_id, ref audio_len, ref synth_status, ref ret);

                if (ptr != IntPtr.Zero)
                {
                    byte[] data = new byte[audio_len];
                    Marshal.Copy(ptr, data, 0, (int)audio_len);
                    sw.Write(data);
                    pcmwavhdr.data_size += (int)audio_len;//修正pcm数据的大小
                }
                if (synth_status == (int)Synthesizing.MSP_TTS_FLAG_DATA_END || ret != 0)
                    break;
            }//合成状态synth_status取值可参考开发文档

            pcmwavhdr.size_8 += pcmwavhdr.data_size + 36;

            //将修正过的数据写回文件头部
            fs.Seek(4, SeekOrigin.Begin);
            sw.Write(pcmwavhdr.size_8);
            fs.Seek(40, SeekOrigin.Begin);
            sw.Write(pcmwavhdr.data_size);

            sw.Flush();
            sw.Close();
            fs.Close();
            sw.Dispose();
            fs.Dispose();
            ret = QTTSSessionEnd(sess_id, null);
            MSPLogout();
            if (ShowInfomation != null) ShowInfomation("转换结束");
            if (TextToVoiceStopEven != null) TextToVoiceStopEven(this, new EventArgs());//触发结束事件

        }

        /// <summary>
        /// 转换语音
        /// </summary>
        /// <param name="text">合成语语音所用文本</param>
        /// <param name="filename">"输出音频文件名"</param>
        /// <param name="voicename">声音人物选择</param>
        /// <param name="voicespeed">语速</param>
        public void ProcessVoice(string text, string filename, VoiceName voicename = VoiceName.xiaoyan, int voicespeed = 5)
        {
            string param = string.Format(" vcn={0}, spd ={1}, vol = 50, bgs=0, aue=speex-wb, smk = 3", voicename, voicespeed);
            string login_param = " appid = 58d376fc,work_dir =   .  ";
            Begin_ProcessVoice(text, filename, param, login_param);
        }
        #endregion

        #endregion

    }

    #endregion
}
