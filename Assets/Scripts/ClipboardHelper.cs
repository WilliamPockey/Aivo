using UnityEngine;
using System;
using System.Runtime.InteropServices;

public static class ClipboardHelper
{
    // 导入 Windows 原生 API
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    
    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);
    
    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    // 常量定义
    private const uint CF_BITMAP = 2;
    private const uint DIB_RGB_COLORS = 0;
    private const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    /// <summary>
    /// 安全地从剪贴板获取图片并转换为 Texture2D
    /// </summary>
    public static Texture2D GetImage()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;

        Texture2D texture = null;

        try
        {
            if (IsClipboardFormatAvailable(CF_BITMAP))
            {
                IntPtr hBitmap = GetClipboardData(CF_BITMAP);
                if (hBitmap != IntPtr.Zero)
                {
                    texture = ConvertBitmapToTexture(hBitmap);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Clipboard Error: {e.Message}");
        }
        finally
        {
            CloseClipboard();
        }

        return texture;
    }

    private static Texture2D ConvertBitmapToTexture(IntPtr hBitmap)
    {
        // 这是一个简化的转换过程，通过 GDI 获取原始数据
        // 为了稳健性，这里使用一个较为通用的方法：
        // 获取位图信息 -> 创建字节数组 -> 填充 Texture2D
        
        BITMAPINFO info = new BITMAPINFO();
        info.biSize = Marshal.SizeOf(info);
        
        IntPtr hDC = CreateCompatibleDC(IntPtr.Zero);
        
        // 第一次调用 GetDIBits 获取尺寸信息
        GetDIBits(hDC, hBitmap, 0, 0, null, ref info, DIB_RGB_COLORS);

        int width = info.biWidth;
        int height = Math.Abs(info.biHeight);

        // 这里的 height 可能是负数，表示自上而下，但 Texture2D 需要修正
        info.biHeight = -height; 
        info.biCompression = 0; // BI_RGB

        byte[] data = new byte[width * height * 4];
        
        // 第二次调用，获取实际像素数据
        if (GetDIBits(hDC, hBitmap, 0, (uint)height, data, ref info, DIB_RGB_COLORS) != IntPtr.Zero)
        {
            // Windows GDI 返回的是 BGRA 格式，且是上下颠倒的（或者是正常的，取决于 biHeight）
            // Unity Texture2D 是 RGBA 格式，原点在左下角
            
            // 我们需要手动交换 B 和 R 分量，并处理翻转
            Texture2D tex = new Texture2D(width, height, TextureFormat.BGRA32, false);
            tex.LoadRawTextureData(data);
            tex.Apply();
            return tex;
        }

        DeleteDC(hDC);
        return null;
    }
}