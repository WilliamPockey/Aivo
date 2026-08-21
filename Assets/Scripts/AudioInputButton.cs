using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 用于检测按下和松开

public class AudioInputButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI 设置")]
    public Image buttonIcon;
    public Sprite micIdleSprite; // 没录音时的图标 (话筒)
    public Sprite micRecordingSprite; // 录音时的图标 (停止/波形)
    public Color recordingColor = Color.red;
    
    [Header("依赖")]
    public ChatManager chatManager;

    private bool _isRecording = false;
    private AudioClip _recordingClip;
    private string _micDeviceName;

    void Start()
    {
        // 获取默认麦克风
        if (Microphone.devices.Length > 0)
        {
            _micDeviceName = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("没有检测到麦克风！");
        }
        
        if(buttonIcon != null && micIdleSprite != null)
            buttonIcon.sprite = micIdleSprite;
    }

    // --- 交互逻辑：按住录音，松开发送 (像微信一样) ---
    // 如果你想点击开始/点击结束，改用 OnClick 即可，这里演示按住录音
    
    public void OnPointerDown(PointerEventData eventData)
    {
        StartRecording();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopAndSend();
    }

    void StartRecording()
    {
        if (_isRecording || string.IsNullOrEmpty(_micDeviceName)) return;

        _isRecording = true;
        
        // UI 变化
        if (buttonIcon) {
            buttonIcon.sprite = micRecordingSprite;
            buttonIcon.color = recordingColor;
        }

        // 开始录音：长度最长60秒，采样率16000 (ASR标准)
        _recordingClip = Microphone.Start(_micDeviceName, false, 60, 16000);
    }

    void StopAndSend()
    {
        if (!_isRecording) return;

        // 停止录音
        int position = Microphone.GetPosition(_micDeviceName);
        Microphone.End(_micDeviceName);
        
        _isRecording = false;

        // UI 恢复
        if (buttonIcon) {
            buttonIcon.sprite = micIdleSprite;
            buttonIcon.color = new Color(125/255f, 159/255f, 144/255f);
        }

        // 处理音频：剪裁掉末尾的空白 (因为Microphone.Start分配了固定的60秒内存)
        if (_recordingClip != null && position > 0)
        {
            AudioClip validClip = TrimAudio(_recordingClip, position);
            
            // 编码为 WAV
            byte[] wavBytes = AudioUtils.EncodeToWAV(validClip);
            string base64Audio = System.Convert.ToBase64String(wavBytes);

            // 通过 ChatManager 发送
            chatManager.SendAudioInput(base64Audio);
        }
    }

    // 辅助：剪裁 AudioClip
    AudioClip TrimAudio(AudioClip original, int position)
    {
        float[] soundData = new float[position * original.channels];
        original.GetData(soundData, 0);
        
        AudioClip trimmed = AudioClip.Create(original.name, position, original.channels, original.frequency, false);
        trimmed.SetData(soundData, 0);
        return trimmed;
    }
}