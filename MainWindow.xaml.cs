using onnxruntime;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WPF_Exploration_Days_ML
{

    public partial class MainWindow : System.Windows.Window
    {
        private DispatcherTimer remainTimer = new DispatcherTimer();
        VideoCapture capture;
        Mat frame;
        Bitmap image;

        public MainWindow()
        {
            InitializeComponent();

            capture = new VideoCapture();
            capture.Open(0);

            remainTimer.Tick += ProcessCapture;
            remainTimer.Interval = TimeSpan.FromMilliseconds(100);
            remainTimer.Start();
        }

        private void ProcessCapture(object sender, EventArgs e)
        {
            try
            {
                if (!capture.IsOpened())
                {
                    capture = new VideoCapture();
                    capture.Open(0);
                }

                if (capture.IsOpened())
                {
                    frame = new Mat();
                    capture.Read(frame);

                    if (frame.Empty())
                    {
                        return;
                    }

                    var processedImage = ImageProcessor.DetectObjects(frame.ToMemoryStream());
                    Webcam.Source = Convert(processedImage);
                }
            }
            catch (Exception)
            {

            }
        }

        private BitmapSource Convert(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }


    }
}
