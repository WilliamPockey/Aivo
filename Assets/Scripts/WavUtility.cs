using UnityEngine;
using System;

public static class WavUtility
{
    // 将 WAV 文件的字节数组转换为 AudioClip
    public static AudioClip ToAudioClip(byte[] wavFile)
    {
        int channels = wavFile[22]; // 读取声道数
        int frequency = BitConverter.ToInt32(wavFile, 24); // 读取采样率
        int pos = 12; // 从第12个字节开始寻找 "data" 块

        // 寻找 data 块的位置
        while (!(wavFile[pos] == 100 && wavFile[pos + 1] == 97 && wavFile[pos + 2] == 116 && wavFile[pos + 3] == 97))
        {
            pos += 4;
            int chunkSize = wavFile[pos] + wavFile[pos + 1] * 256 + wavFile[pos + 2] * 65536 + wavFile[pos + 3] * 16777216;
            pos += 4 + chunkSize;
        }
        pos += 8;

        // 计算样本数量
        int sampleCount = (wavFile.Length - pos) / 2; 
        if (channels == 2) sampleCount /= 2;

        float[] data = new float[sampleCount * channels];

        int i = 0;
        while (pos < wavFile.Length)
        {
            data[i] = BytesToFloat(wavFile[pos], wavFile[pos + 1]);
            pos += 2;
            if (channels == 2)
            {
                data[i + 1] = BytesToFloat(wavFile[pos], wavFile[pos + 1]);
                pos += 2;
                i++;
            }
            i++;
        }

        AudioClip clip = AudioClip.Create("GeneratedVoice", sampleCount, channels, frequency, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float BytesToFloat(byte firstByte, byte secondByte)
    {
        short s = (short)((secondByte << 8) | firstByte);
        return s / 32768.0f;
    }
}