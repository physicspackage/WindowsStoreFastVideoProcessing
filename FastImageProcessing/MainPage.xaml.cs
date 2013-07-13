using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI;
using Windows.Devices.Enumeration;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FastImageProcessing
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture captureMgr;
        Action<byte[], int> selectedPixelFunction;
        bool processImage;

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await this.InitializePreview();
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            await this.StopPreview();
            base.OnNavigatedFrom(e);
        }

        private async System.Threading.Tasks.Task StopPreview()
        {
            selectedPixelFunction = null;
            await captureMgr.StopPreviewAsync();
        }

        private void buttonGreyScale_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcess(GreyScaleFunction);
        }

        private void buttonBlackWhite_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcess(BlackWhiteFunction);
        }

        private void buttonHighlightRed_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcess(HighlightRedFunction);
        }

        private void buttonInverse_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcess(InverseFunction);
        }

        private void UpdateProcess(Action<byte[], int> pixelFunction)
        {
            if (selectedPixelFunction == null)
            {
                selectedPixelFunction = pixelFunction;
                processImage = true;
                DoImageProcessing();
            }
            else if (selectedPixelFunction != pixelFunction)
            {
                selectedPixelFunction = pixelFunction;
            }
            else
            {
                processImage = false;
            }
        }

        private async System.Threading.Tasks.Task InitializePreview()
        {
            captureMgr = new MediaCapture();
            MediaCaptureInitializationSettings settings = new Windows.Media.Capture.MediaCaptureInitializationSettings();

            // Hardwire to front camera for surface pro.
            //DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            //settings.VideoDeviceId = devices[1].Id;

            settings.StreamingCaptureMode = StreamingCaptureMode.Video;
            await captureMgr.InitializeAsync(settings);
            capturePreview.Source = captureMgr;
            await captureMgr.StartPreviewAsync();
        }

        private async void DoImageProcessing()
        {
            var ps = new InMemoryRandomAccessStream();

            while (processImage)
            {
                await capturePreview.Source.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), ps);
                await ps.FlushAsync();
                ps.Seek(0);

                WriteableBitmap bitmap = new WriteableBitmap(424, 240);
                bitmap.SetSource(ps);
                int bitmapSize = bitmap.PixelHeight * bitmap.PixelWidth * 4;

                using (Stream imageStream = bitmap.PixelBuffer.AsStream())
                {
                    byte[] bitmapPixelArray = new byte[bitmapSize];
                    await imageStream.ReadAsync(bitmapPixelArray, 0, bitmapSize);

                    for (int i = 0; i < bitmapSize; i += 4)
                    {
                        selectedPixelFunction(bitmapPixelArray, i);
                    }

                    imageStream.Seek(0, SeekOrigin.Begin);
                    await imageStream.WriteAsync(bitmapPixelArray, 0, bitmapSize);
                }
                imageProcess.Source = bitmap;
            }
            selectedPixelFunction = null;
        }

        private static void GreyScaleFunction(byte[] bitmapPixelArray, int i)
        {
            byte blue = bitmapPixelArray[i + 0];
            byte green = bitmapPixelArray[i + 1];
            byte red = bitmapPixelArray[i + 2];
            byte alpha = bitmapPixelArray[i + 3];

            double intensity = 0.299 * red + 0.587 * green + 0.114 * blue;
            byte grayscaleColor = NormalizeColorComponent(intensity);

            bitmapPixelArray[i + 0] = grayscaleColor;
            bitmapPixelArray[i + 1] = grayscaleColor;
            bitmapPixelArray[i + 2] = grayscaleColor;
            bitmapPixelArray[i + 3] = grayscaleColor;
        }

        private static void BlackWhiteFunction(byte[] bitmapPixelArray, int i)
        {
            byte blue = bitmapPixelArray[i + 0];
            byte green = bitmapPixelArray[i + 1];
            byte red = bitmapPixelArray[i + 2];
            byte alpha = bitmapPixelArray[i + 3];

            double intensity = 0.299 * red + 0.587 * green + 0.114 * blue;
            byte grayscaleColor = NormalizeColorComponent(intensity);
            grayscaleColor = grayscaleColor > 127 ? grayscaleColor = byte.MaxValue : grayscaleColor = byte.MinValue;

            bitmapPixelArray[i + 0] = grayscaleColor;
            bitmapPixelArray[i + 1] = grayscaleColor;
            bitmapPixelArray[i + 2] = grayscaleColor;
            bitmapPixelArray[i + 3] = grayscaleColor;
        }

        private static void HighlightRedFunction(byte[] bitmapPixelArray, int i)
        {
            byte red = bitmapPixelArray[i + 2];

            bitmapPixelArray[i + 0] = 0;
            bitmapPixelArray[i + 1] = 0;
            bitmapPixelArray[i + 2] = red;
            bitmapPixelArray[i + 3] = red;
        }

        private static void InverseFunction(byte[] bitmapPixelArray, int i)
        {
            byte blue = bitmapPixelArray[i + 0];
            byte green = bitmapPixelArray[i + 1];
            byte red = bitmapPixelArray[i + 2];
            byte alpha = bitmapPixelArray[i + 3];

            bitmapPixelArray[i + 0] = (byte)(byte.MaxValue - blue);
            bitmapPixelArray[i + 1] = (byte)(byte.MaxValue - green);
            bitmapPixelArray[i + 2] = (byte)(byte.MaxValue - red);
            bitmapPixelArray[i + 3] = (byte)(byte.MaxValue - alpha);
        }

        private static void CrazyFunction(byte[] bitmapPixelArray, int i)
        {
            byte blue = bitmapPixelArray[i + 0];
            byte green = bitmapPixelArray[i + 1];
            byte red = bitmapPixelArray[i + 2];
            byte alpha = bitmapPixelArray[i + 3];

            bitmapPixelArray[i + 0] = red;
            bitmapPixelArray[i + 1] = blue;
            bitmapPixelArray[i + 2] = green;
            bitmapPixelArray[i + 3] = (byte)(byte.MaxValue - alpha);
        }

        public static byte NormalizeColorComponent(double componenet)
        {
            if (componenet > 255) componenet = 255;
            if (componenet < 0) componenet = 0;
            return (byte)componenet;
        }

        private void buttonCrazy_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcess(CrazyFunction);
        }
    }
}
//FPSStack.Visibility = Windows.UI.Xaml.Visibility.Visible;
//FPSStack.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
//DateTime start = DateTime.Now;
//DateTime end;

//end = DateTime.Now;
//double fps = 1 / (end - start).TotalSeconds;
//start = end;
//Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low,
//    () =>
//    {
//        FPSTextBlock.Text = fps.ToString("F1");
//    });
