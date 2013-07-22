using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using FastPhoneImageProcessing.Resources;
using Microsoft.Devices;
using Windows.Phone.Media.Capture;
using System.Windows.Media.Imaging;
using System.IO;
using System.ComponentModel;
using System.Windows.Threading;

namespace FastPhoneImageProcessing
{
    public partial class MainPage : PhoneApplicationPage
    {
        Action<int[], int> selectedPixelFunction;
        PhotoCaptureDevice captureDevice;
        DispatcherTimer timer;
        WriteableBitmap processBitmap;
        const int FULLALPHA = 255 << 24;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            BuildLocalizedApplicationBar();
        }

        // Sample code for building a localized ApplicationBar
        private void BuildLocalizedApplicationBar()
        {
            // Set the page's ApplicationBar to a new instance of ApplicationBar.
            ApplicationBar = new ApplicationBar();

            // Create a new button and set the text value to the localized string from AppResources.
            //ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
            //appBarButton.Text = AppResources.AppBarButtonText;
            //ApplicationBar.Buttons.Add(appBarButton);

            // Create a new menu item with the localized string from AppResources.
            ApplicationBarMenuItem appBarGreyMenuItem = new ApplicationBarMenuItem("Grey");
            appBarGreyMenuItem.Click += appBarGreyMenuItem_Click;
            ApplicationBar.MenuItems.Add(appBarGreyMenuItem);

            ApplicationBarMenuItem appBarBlackWhiteMenuItem = new ApplicationBarMenuItem("Black/White");
            appBarBlackWhiteMenuItem.Click += appBarBlackWhiteMenuItem_Click;
            ApplicationBar.MenuItems.Add(appBarBlackWhiteMenuItem);

            ApplicationBarMenuItem appBarRedMenuItem = new ApplicationBarMenuItem("Red");
            appBarRedMenuItem.Click += appBarRedMenuItem_Click;
            ApplicationBar.MenuItems.Add(appBarRedMenuItem);

            ApplicationBarMenuItem appBarInverseMenuItem = new ApplicationBarMenuItem("Inverse");
            appBarInverseMenuItem.Click += appBarInverseMenuItem_Click;
            ApplicationBar.MenuItems.Add(appBarInverseMenuItem);

            ApplicationBarMenuItem appBarBlueYellowMenuItem = new ApplicationBarMenuItem("Blue/Yellow");
            appBarBlueYellowMenuItem.Click += appBarBlueYellowMenuItem_Click;
            ApplicationBar.MenuItems.Add(appBarBlueYellowMenuItem);

            ApplicationBar.Mode = ApplicationBarMode.Minimized;
        }

        void appBarBlueYellowMenuItem_Click(object sender, EventArgs e)
        {
            this.UpdateProcess(BlueYellowFunction);
        }

        void appBarInverseMenuItem_Click(object sender, EventArgs e)
        {
            this.UpdateProcess(InverseFunction);
        }

        void appBarRedMenuItem_Click(object sender, EventArgs e)
        {
            this.UpdateProcess(HighlightRedFunction);
        }

        void appBarBlackWhiteMenuItem_Click(object sender, EventArgs e)
        {
            this.UpdateProcess(BlackWhiteFunction);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            timer.Stop();
            captureDevice.Dispose();

            base.OnNavigatedFrom(e);
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1);
            timer.Tick += timer_Tick;

            // Check to see if the camera is available on the device.
            if (PhotoCaptureDevice.AvailableSensorLocations.Contains(CameraSensorLocation.Back) ||
                PhotoCaptureDevice.AvailableSensorLocations.Contains(CameraSensorLocation.Front))
            {
                // Initialize the camera, when available.
                if (PhotoCaptureDevice.AvailableSensorLocations.Contains(CameraSensorLocation.Back))
                {
                    // Use the back camera.
                    System.Collections.Generic.IReadOnlyList<Windows.Foundation.Size> SupportedResolutions =
                        PhotoCaptureDevice.GetAvailableCaptureResolutions(CameraSensorLocation.Back);
                    Windows.Foundation.Size res = SupportedResolutions[0];
                    this.captureDevice = await PhotoCaptureDevice.OpenAsync(CameraSensorLocation.Back, res);

                    System.Collections.Generic.IReadOnlyList<Windows.Foundation.Size> SupportedPreviewResolutions =
                        PhotoCaptureDevice.GetAvailablePreviewResolutions(CameraSensorLocation.Back);

                    var lowRes = SupportedPreviewResolutions.OrderBy(r => r.Width).First();
                    await this.captureDevice.SetPreviewResolutionAsync(lowRes);
                
                }
                else
                {
                    // Otherwise, use the front camera.
                    System.Collections.Generic.IReadOnlyList<Windows.Foundation.Size> SupportedResolutions =
                        PhotoCaptureDevice.GetAvailableCaptureResolutions(CameraSensorLocation.Front);
                    Windows.Foundation.Size res = SupportedResolutions[0];
                    this.captureDevice = await PhotoCaptureDevice.OpenAsync(CameraSensorLocation.Front, res);
                }

                //Set the VideoBrush source to the camera.
                viewfinderBrush.SetSource(this.captureDevice);

                int width = (int)this.captureDevice.PreviewResolution.Width;
                int height = (int)this.captureDevice.PreviewResolution.Height;
                processBitmap = new WriteableBitmap(width, height);
                processed.Source = processBitmap;
            }
            else
            {
                // The camera is not available.
                this.Dispatcher.BeginInvoke(delegate()
                {
                });
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            DoImageProcessing();
        }

        void appBarGreyMenuItem_Click(object sender, EventArgs e)
        {
            this.UpdateProcess(GreyScaleFunction);
        }

        private void UpdateProcess(Action<int[], int> pixelFunction)
        {
            if (!timer.IsEnabled)
            {
                selectedPixelFunction = pixelFunction;
                timer.Start();
            }
            else if (selectedPixelFunction != pixelFunction)
            {
                selectedPixelFunction = pixelFunction;
            }
            else
            {
                timer.Stop();
            }
        }

        private void DoImageProcessing()
        {
            int[] bitmapPixelArray = processBitmap.Pixels;
            this.captureDevice.GetPreviewBufferArgb(bitmapPixelArray);
            int bitmapSize = bitmapPixelArray.Count();

            for (int i = 0; i < bitmapSize; ++i)
            {
                selectedPixelFunction(bitmapPixelArray, i);
            }

            processBitmap.Invalidate();
        }

        public static byte NormalizeColorComponent(double componenet)
        {
            if (componenet > 255) componenet = 255;
            if (componenet < 0) componenet = 0;
            return (byte)componenet;
        }

        private static void GreyScaleFunction(int[] bitmapPixelArray, int i)
        {
            byte red = (byte)(bitmapPixelArray[i] >> 16);
            byte green = (byte)(bitmapPixelArray[i] >> 8);
            byte blue = (byte)(bitmapPixelArray[i]);

            double intensity = 0.299 * red + 0.587 * green + 0.114 * blue;
            byte grayscaleColor = NormalizeColorComponent(intensity);

            bitmapPixelArray[i] = FULLALPHA + ((int)grayscaleColor << 16) + ((int)grayscaleColor << 8) + (int)grayscaleColor;
        }

        private static void BlackWhiteFunction(int[] bitmapPixelArray, int i)
        {
            byte red = (byte)(bitmapPixelArray[i] >> 16);
            byte green = (byte)(bitmapPixelArray[i] >> 8);
            byte blue = (byte)(bitmapPixelArray[i]);

            double intensity = 0.299 * red + 0.587 * green + 0.114 * blue;
            byte grayscaleColor = NormalizeColorComponent(intensity);
            grayscaleColor = grayscaleColor > 127 ? grayscaleColor = byte.MaxValue : grayscaleColor = byte.MinValue;

            bitmapPixelArray[i] = FULLALPHA + ((int)grayscaleColor << 16) + ((int)grayscaleColor << 8) + (int)grayscaleColor;
        }

        private static void HighlightRedFunction(int[] bitmapPixelArray, int i)
        {
            byte red = (byte)(bitmapPixelArray[i] >> 16);

            bitmapPixelArray[i] = FULLALPHA + ((int)red << 16);
        }

        private static void InverseFunction(int[] bitmapPixelArray, int i)
        {
            byte red = (byte)(byte.MaxValue - (byte)(bitmapPixelArray[i] >> 16));
            byte green = (byte)(byte.MaxValue - (byte)(bitmapPixelArray[i] >> 8));
            byte blue = (byte)(byte.MaxValue - (byte)(bitmapPixelArray[i]));

            bitmapPixelArray[i] = FULLALPHA + ((int)red << 16) + ((int)green << 8) + (int)blue;
        }

        private static void BlueYellowFunction(int[] bitmapPixelArray, int i)
        {
            byte red = (byte)(byte.MaxValue - (byte)(bitmapPixelArray[i] >> 16));
            byte green = (byte)(byte.MaxValue - (byte)(bitmapPixelArray[i] >> 8));
            byte blue = (byte)(byte.MaxValue - (byte)(bitmapPixelArray[i]));

            double intensity = 0.299 * red + 0.587 * green + 0.114 * blue;
            byte grayscaleColor = NormalizeColorComponent(intensity);

            bitmapPixelArray[i] = FULLALPHA + ((int)(byte.MaxValue - grayscaleColor) << 16) + ((int)(byte.MaxValue - grayscaleColor) << 8) + (int)grayscaleColor;
        }
    }
}