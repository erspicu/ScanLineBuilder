using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WINAPIGDI;

using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;



namespace ScanLineBuilder
{
    unsafe public partial class Form1 : Form
    {


        uint* ScreenBuf1x, result2, result3;// , line;

        float* yiq, yuv, yiq_org;

        int* yiq_y, yiq_i, yiq_q, yiq_t, yiq_org_int, clip;



        byte* rates;




        public Form1()
        {
            InitializeComponent();

            ScreenBuf1x = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 512 * 480);

            result2 = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 1196 * 960);
            result3 = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 1196 * 960);

            yiq = (float*)Marshal.AllocHGlobal(sizeof(float) * 1196 * 960 * 3);
            yiq_org = (float*)Marshal.AllocHGlobal(sizeof(float) * 512 * 480 * 3);

            yiq_org_int = (int*)Marshal.AllocHGlobal(sizeof(int) * 512 * 480 * 3);

            clip = (int*)Marshal.AllocHGlobal(sizeof(int) * 1024);

            yuv = (float*)Marshal.AllocHGlobal(sizeof(float) * 1196 * 960 * 3);


            yiq_y = (int*)Marshal.AllocHGlobal(sizeof(int) * 16777216);
            yiq_i = (int*)Marshal.AllocHGlobal(sizeof(int) * 16777216);
            yiq_q = (int*)Marshal.AllocHGlobal(sizeof(int) * 16777216);

            yiq_t = (int*)Marshal.AllocHGlobal(sizeof(int) * 16777216);


            rates = (byte*)Marshal.AllocHGlobal(sizeof(byte) * 256);


            Bitmap bg = new Bitmap(Application.StartupPath + "/sample.png");
            ScreenBuf1x = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 512 * 512);
            BitmapData srcData = bg.LockBits(new Rectangle(Point.Empty, bg.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 480; y++)
                for (int x = 0; x < 512; x++)
                    ScreenBuf1x[x + y * 512] = ((uint*)srcData.Scan0.ToPointer())[x + y * 512];
            bg.UnlockBits(srcData);

            pictureBox1.Load(Application.StartupPath + "/sample.png");


            for (int i = 0; i < 256; i++)
            {
                rates[i] = (byte)(i * 0.9f);
            }


            for (int i = 0; i < 1024; i++)
            {

                int v = i - 512;
                if (v > 255) v = 255;
                else if (v < 0) v = 0;
                clip[i] = v;
            }


            //build table
            int _y = 0, _i = 0, _q = 0;
            int r = 0, g = 0, b = 0;
            for (int i = 0; i <= 16777215; i++)
            {
                int color_o = i;

                int b1_o = (byte)color_o;//Blue
                int b2_o = (byte)(color_o >> 8);//Green
                int b3_o = (byte)(color_o >> 16);//Red


                //convert
                yiq_y[i] = (int)(306 * b3_o + 601 * b2_o + 117 * b1_o);
                yiq_i[i] = (int)(610 * b3_o - 281 * b2_o - 330 * b1_o);
                yiq_q[i] = (int)(216 * b3_o - 536 * b2_o + 319 * b1_o);


                yiq_t[i] = (((byte)yiq_y[i]) << 16) | (((byte)yiq_i[i]) << 8) | ((byte)yiq_q[i]);


                int R_f = ((yiq_y[i] << 10) + yiq_i[i] * 979 + yiq_q[i] * 636) >> 20;
                int G_f = ((yiq_y[i] << 10) + yiq_i[i] * -279 + yiq_q[i] * -663) >> 20;
                int B_f = ((yiq_y[i] << 10) + yiq_i[i] * -1133 + yiq_q[i] * 1744) >> 20;


                if (yiq_y[i] > _y) _y = yiq_y[i];
                if (yiq_i[i] > _i) _i = yiq_i[i];
                if (yiq_q[i] > _q) _q = yiq_q[i];


                if (R_f > r) r = R_f;
                if (G_f > g) g = G_f;// yiq_i[i];
                if (B_f > b) b = B_f;// yiq_q[i];

            }
            Console.WriteLine(_y + " " + _i + " " + _q);

            Console.WriteLine(r + " " + g + " " + b);

            for (int i = 0; i <= 16777215; i++)
            {
                int yiq_v = i;

                byte y1_o = (byte)yiq_v;//Blue
                byte y2_o = (byte)(yiq_v >> 8);//Green
                byte y3_o = (byte)(yiq_v >> 16);//Red
            }

            Console.WriteLine(_y + " " + _i + " " + _q);


        }

        private void button3_Click(object sender, EventArgs e)
        {


            new Thread(() =>
            {

                Stopwatch ST = new Stopwatch();
                ST.Restart();




                //YIQ版本 4:1:1取樣 且加入pixel模糊特效
                for (int y = 0; y < 960; y++)
                {
                    for (int x = 0; x < 1196; x++)
                    {

                        int _x = (int)(x / 1196.0 * 512.0);
                        int _y = y / 2;

                        //format ARGB 8:8:8:8
                        uint color_o = ScreenBuf1x[_x + _y * 512];
                        byte b1_o = (byte)(color_o & 0xff);//Blue
                        byte b2_o = (byte)((color_o & 0xff00) >> 8);//Green
                        byte b3_o = (byte)((color_o & 0xff0000) >> 16);//Red

                        //get float
                        float TO_FLOAT = 1 / 255.0f;
                        float R_f = b3_o * TO_FLOAT;
                        float G_f = b2_o * TO_FLOAT;
                        float B_f = b1_o * TO_FLOAT;
                        //convert
                        float Y = 0.299f * R_f + 0.587f * G_f + 0.114f * B_f;
                        float I = 0.596f * R_f - 0.274f * G_f - 0.322f * B_f;
                        float Q = 0.211f * R_f - 0.523f * G_f + 0.312f * B_f;

                        yiq[(x + y * 1196) * 3] = Y;
                        yiq[(x + y * 1196) * 3 + 1] = I;
                        yiq[(x + y * 1196) * 3 + 2] = Q;
                    }
                }

                bool even = false;
                for (int y = 0; y < 960; y++)
                {
                    for (int x = 0; x < 1196; x++)
                    {

                        float Y = yiq[(x + y * 1196) * 3];
                        float I = yiq[((x & 0xffc) + y * 1196) * 3 + 1];
                        float Q = yiq[((x & 0xffc) + y * 1196) * 3 + 2];

                        Y += 0.05F;

                        float R_f = Y + I * 0.956f + Q * 0.621f;
                        float G_f = Y + I * (-0.272f) + Q * (-0.647f);
                        float B_f = Y + I * (-1.106f) + Q * (1.703f);

                        R_f *= 255.0f;
                        G_f *= 255.0f;
                        B_f *= 255.0f;

                        if (R_f > 255.0) R_f = 255.0f;
                        if (G_f > 255.0) G_f = 255.0f;
                        if (B_f > 255.0) B_f = 255.0f;


                        if (R_f < .0) R_f = .0f;
                        if (G_f < .0) G_f = .0f;
                        if (B_f < .0) B_f = .0f;


                        byte b1_o = (byte)(B_f);
                        byte b2_o = (byte)(G_f);
                        byte b3_o = (byte)(R_f);
                        result2[x + y * 1196] = (uint)(0xff000000 | (b3_o << 16) | (b2_o << 8) | b1_o);
                    }

                }

                //模糊化

                for (int y = 0; y < 960; y++)
                {
                    for (int x = 0; x < 1196; x++)
                    {

                        uint color_l = 0xffffffff, color_r = 0xffffffff, color_o;

                        color_o = result2[x + y * 1196];

                        if (x > 0) color_l = result2[x - 1 + y * 1196];

                        if (x < 1195) color_r = result2[x + 1 + y * 1196];


                        byte b1_o = (byte)(color_o & 0xff);//Blue
                        byte b2_o = (byte)((color_o & 0xff00) >> 8);//Green
                        byte b3_o = (byte)((color_o & 0xff0000) >> 16);//Red

                        byte b1_l = (byte)(color_l & 0xff);//Blue
                        byte b2_l = (byte)((color_l & 0xff00) >> 8);//Green
                        byte b3_l = (byte)((color_l & 0xff0000) >> 16);//Red


                        byte b1_r = (byte)(color_r & 0xff);//Blue
                        byte b2_r = (byte)((color_r & 0xff00) >> 8);//Green
                        byte b3_r = (byte)((color_r & 0xff0000) >> 16);//Red


                        b1_o = (byte)((b1_o * 1 + b1_l * 3 + b1_r * 1) / 5);
                        b2_o = (byte)((b2_o * 1 + b2_l * 3 + b2_r * 1) / 5);
                        b3_o = (byte)((b3_o * 1 + b3_l * 3 + b3_r * 1) / 5);


                        #region blend scanline pattern
                        float m = 0.1f;
                        if (even) m = 0;
                        byte b1_n = (byte)(b1_o * (1 - m));
                        byte b2_n = (byte)(b2_o * (1 - m));
                        byte b3_n = (byte)(b3_o * (1 - m));
                        #endregion

                        result3[x + y * 1196] = (uint)(0xff000000 | (b3_n << 16) | (b2_n << 8) | b1_n);

                    }
                    if (y % 2 == 0)
                        even = !even;
                }


                ST.Stop();

                Console.WriteLine(ST.ElapsedMilliseconds);

                NativeGDI.initHighSpeed(panel1.CreateGraphics(), 1196, 960, result3, 0, 0);
                NativeGDI.DrawImageHighSpeedtoDevice();

            }).Start();



        }

        private void button7_Click(object sender, EventArgs e)
        {

            uint* input = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 768 * 720);
            Bitmap bg = new Bitmap(Application.StartupPath + "/sample3x.png");
            BitmapData srcData = bg.LockBits(new Rectangle(Point.Empty, bg.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 720; y++)
                for (int x = 0; x < 768; x++)
                    input[x + y *768] = ((uint*)srcData.Scan0.ToPointer())[x + y * 768];
            bg.UnlockBits(srcData);
            uint* output = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 1792 * 1440);

            LibScanline.init(input, output);


            new Thread(() =>
            {

                Stopwatch ST = new Stopwatch();

                ST.Restart();
                for (int i = 0; i < 400; i++) LibScanline.ScanlineFor3x();
                ST.Stop();

                Console.WriteLine(ST.ElapsedMilliseconds);
                Console.WriteLine(ST.ElapsedMilliseconds / 400f);

                NativeGDI.initHighSpeed(panel1.CreateGraphics(), 1792, 1440, output, 0, 0);
                NativeGDI.DrawImageHighSpeedtoDevice();
  
            }).Start();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //單純YIQ轉換 4:4:4取樣
            for (int y = 0; y < 960; y++)
            {
                for (int x = 0; x < 1196; x++)
                {

                    //nearest get sample 512*480 -> 1197*960
                    int _x = (int)(x / 1196.0 * 512.0);
                    int _y = y / 2;

                    //format ARGB 8:8:8:8
                    uint color_o = ScreenBuf1x[_x + _y * 512];
                    byte b1_o = (byte)(color_o & 0xff);//Blue
                    byte b2_o = (byte)((color_o & 0xff00) >> 8);//Green
                    byte b3_o = (byte)((color_o & 0xff0000) >> 16);//Red


                    //get float
                    float TO_FLOAT = 1 / 255.0f;
                    float R_f = b3_o * TO_FLOAT;
                    float G_f = b2_o * TO_FLOAT;
                    float B_f = b1_o * TO_FLOAT;
                    //convert
                    float Y = 0.299f * R_f + 0.587f * G_f + 0.114f * B_f;
                    float I = 0.596f * R_f - 0.274f * G_f - 0.322f * B_f;
                    float Q = 0.211f * R_f - 0.523f * G_f + 0.312f * B_f;

                    #region YIQ TO TGB

                    R_f = Y + I * 0.956f + Q * 0.621f;
                    G_f = Y + I * (-0.272f) + Q * (-0.647f);
                    B_f = Y + I * (-1.106f) + Q * (1.703f);

                    b1_o = (byte)(B_f * 255.0f);
                    b2_o = (byte)(G_f * 255.0f);
                    b3_o = (byte)(R_f * 255.0f);
                    #endregion

                    result2[x + y * 1196] = (uint)(0xff000000 | (b3_o << 16) | (b2_o << 8) | b1_o);

                }
            }
            NativeGDI.initHighSpeed(panel1.CreateGraphics(), 1196, 960, result2, 0, 0);
            NativeGDI.DrawImageHighSpeedtoDevice();
        }


        int get_withd_out(int w)
        {

            return ((w - 1) / 3 + 1) * 7;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //簡易版本 pattern 樣版明暗排列模擬版本           
            bool even = false;
            for (int y = 0; y < 960; y++)
            {
                for (int x = 0; x < 1196; x++)
                {
                    int _x = (int)(x / 1196.0 * 512.0);
                    int _y = y / 2;
                    uint color_o = ScreenBuf1x[_x + _y * 512];
                    byte b1_o = (byte)(color_o & 0xff);
                    byte b2_o = (byte)((color_o & 0xff00) >> 8);
                    byte b3_o = (byte)((color_o & 0xff0000) >> 16);
                    float m = 0.2f;
                    if (even) m = 0;
                    byte b1_n = (byte)(b1_o * (1 - m));
                    byte b2_n = (byte)(b2_o * (1 - m));
                    byte b3_n = (byte)(b3_o * (1 - m));
                    result2[x + y * 1196] = (uint)(0xff000000 | (b3_n << 16) | (b2_n << 8) | b1_n);
                }
                even = !even;
            }
            NativeGDI.initHighSpeed(panel1.CreateGraphics(), 1196, 960, result2, 0, 0);
            NativeGDI.DrawImageHighSpeedtoDevice();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //YUV取樣版本

            bool even = false;
            for (int y = 0; y < 960; y++)
            {
                for (int x = 0; x < 1196; x++)
                {

                    //nearest get sample 512*480 -> 1197*960
                    int _x = (int)(x / 1196.0 * 512.0);
                    int _y = y / 2;

                    //format ARGB 8:8:8:8
                    uint color_o = ScreenBuf1x[_x + _y * 512];
                    byte b3_o = (byte)(color_o & 0xff);//Blue
                    byte b2_o = (byte)((color_o & 0xff00) >> 8);//Green
                    byte b1_o = (byte)((color_o & 0xff0000) >> 16);//Red

                    //get float
                    float R_f = b1_o;
                    float G_f = b2_o;
                    float B_f = b3_o;
                    //convert
                    float Y = 0.299f * R_f + 0.587f * G_f + 0.114f * B_f;
                    float U = (-0.169f) * R_f - 0.331f * G_f + 0.5f * B_f + 128;
                    float V = 0.5f * R_f - 0.419f * G_f - 0.081f * B_f + 128;

                    if (Y > 255.0) Y = 255.0f;
                    if (U > 255.0) U = 255.0f;
                    if (V > 255.0) V = 255.0f;

                    if (Y < .0) Y = .0f;
                    if (U < .0) U = .0f;
                    if (V < .0) V = .0f;

                    yuv[(x + y * 1196) * 3] = Y;
                    yuv[(x + y * 1196) * 3 + 1] = U;
                    yuv[(x + y * 1196) * 3 + 2] = V;
                }
            }


            for (int y = 0; y < 960; y++)
            {
                for (int x = 0; x < 1196; x++)
                {

                    float Y = yuv[(x + y * 1196) * 3];
                    float U = yuv[(((x >> 2) << 2) + y * 1196) * 3 + 1];
                    float V = yuv[(((x >> 2) << 2) + y * 1196) * 3 + 2];

                    float R_f = Y + 1.13983f * (V - 128f);
                    float G_f = Y - 0.39465f * (U - 128f) - 0.5806f * (V - 128f);
                    float B_f = Y + 2.03211f * (U - 128f);


                    if (R_f > 255.0) R_f = 255.0f;
                    if (G_f > 255.0) G_f = 255.0f;
                    if (B_f > 255.0) B_f = 255.0f;

                    if (R_f < .0) R_f = .0f;
                    if (G_f < .0) G_f = .0f;
                    if (B_f < .0) B_f = .0f;

                    byte b1_o = (byte)(B_f);
                    byte b2_o = (byte)(G_f);
                    byte b3_o = (byte)(R_f);

                    #region blend scanline pattern
                    float m = 0.1f;
                    if (even) m = 0;
                    byte b1_n = (byte)(b1_o * (1 - m));
                    byte b2_n = (byte)(b2_o * (1 - m));
                    byte b3_n = (byte)(b3_o * (1 - m));
                    #endregion

                    result2[x + y * 1196] = (uint)(0xff000000 | (b3_n << 16) | (b2_n << 8) | b1_n);

                }
                even = !even;
            }

            NativeGDI.initHighSpeed(panel1.CreateGraphics(), 1196, 960, result2, 0, 0);
            NativeGDI.DrawImageHighSpeedtoDevice();

        }


    }
}
