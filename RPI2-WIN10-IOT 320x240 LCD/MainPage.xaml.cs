using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Spi;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using PI_Colour_LCD;
using TouchPanels;
using Windows.Security.Cryptography;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RPI2_WIN10_IOT_LCD
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer timer;         // create a timer
        const Int32 DisplayCS = 0;                  // 0 maps to CS0 on the Rpi2        */
        const Int32 TouchCS = 1;                    // 1 maps to CS1 on the Rpi2        */
        const Int32 DCPIN = 24;          // GPIO 22 as it is hard wired on the display im using
        const Int32 RESETPIN = 25;                 // GPIO 27 as it is hard wired on the display im using
        const string Defaulttxt = "Display 1";
        const string TSC2046CalFilename = "TSC2046";
        WS_ILI9488Display display1 = new WS_ILI9488Display(Defaulttxt, WS_ILI9488Display.BLACK, DCPIN, RESETPIN);
        //HX8357Display display1 = new HX8357Display(Defaulttxt, HX8357Display.BLACK, DCPIN, RESETPIN);
        bool penPressed = false;
        public MainPage()
        {
            this.InitializeComponent();
            Init();
            Status.Text = "Init Success";
        }
        ~MainPage()
        {
            WS_HX8357.CleanUp();
        }
        private void initTimer()
        {
            // read timer
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50); //sample every 50mS
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        // read GPIO and display it
        private void Timer_Tick(object sender, object e)
        {
            TouchShow();    // do something with the values
        }
        private async void Init()
        {
            await WS_ILI9488.InitILI9488DisplaySPI(display1, 0, 50000000, SpiMode.Mode0, "SPI0", "ms-appx:///assets/SPLASH1 480.png");
            await TSC2046.InitTSC2046SPI();
            initTimer();
            if (! await TSC2046.CalibrationMatrix.LoadCalData(TSC2046CalFilename))
            {
                calibrateTouch();
            }

        }
        private void TouchShow()
        {
            TSC2046.CheckTouch();

            int x = TSC2046.getTouchX();
            int y = TSC2046.getTouchY();
            int p = TSC2046.getPressure();
            if (p > 5)
            {
                Status.Text =  ("Xraw= " + TSC2046.getTouchX() + "    Xcal= " + TSC2046.getDispX() + Environment.NewLine);
                Status.Text += ("Yraw= " + TSC2046.getTouchY() + "    Ycal= " + TSC2046.getDispY() + Environment.NewLine);
                Status.Text += ("P= " + TSC2046.getPressure() + Environment.NewLine);
            //move an elypse why not:)
            moveme.Margin = new Thickness( TSC2046.getDispX() * (1680/480), TSC2046.getDispY()*(1050/320),0,0) ;
            penPressed = true;
            }
            else if (p < 2 && penPressed == true )
            {
                checkAction(TSC2046.getDispX(), TSC2046.getDispY());
                penPressed = false;
            }

        }

        private async void button1_Click(object sender, RoutedEventArgs e)
        {
            await WS_ILI9488.LoadBitmap(display1, "ms-appx:///assets/TEST.jpg");
            WS_ILI9488.Flush(display1);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Exit();
        }

        private void Calibrate_Click(object sender, RoutedEventArgs e)
        {
            calibrateTouch();
        }
        private async  void calibrateTouch()
        {
            //3 point calibration
            TouchPanels.CAL_POINT[] touchPoints = new CAL_POINT[3];
            touchPoints[0] = new CAL_POINT();
            touchPoints[1] = new CAL_POINT();
            touchPoints[2] = new CAL_POINT();
            TouchPanels.CAL_POINT[] screenPoints = new CAL_POINT[3];
            screenPoints[0] = new CAL_POINT();
            screenPoints[1] = new CAL_POINT();
            screenPoints[2] = new CAL_POINT();

            WS_ILI9488.fillRect(display1, 0, 0, 480, 320, 0x0000);

            WS_ILI9488.LineDrawH(display1, 50, 50, 50, 0xFFFF);
            WS_ILI9488.LineDrawV(display1, 75, 25, 50, 0xFFFF);
            while (TSC2046.pressure < 5) { TSC2046.CheckTouch(); }//wait for pen pressure
            screenPoints[0].x = 75;
            screenPoints[0].y = 50;
            touchPoints[0].x = TSC2046.tp_x;
            touchPoints[0].y = TSC2046.tp_y;
            while (TSC2046.pressure > 1) { TSC2046.CheckTouch(); } // wait for release of pen
            WS_ILI9488.LineDrawH(display1, 400, 50, 50, 0xFFFF);
            WS_ILI9488.LineDrawV(display1, 425, 25, 50, 0xFFFF);
            while (TSC2046.pressure < 5) { TSC2046.CheckTouch(); }//wait for pen pressure
            screenPoints[1].x = 425;
            screenPoints[1].y = 50;
            touchPoints[1].x = TSC2046.tp_x;
            touchPoints[1].y = TSC2046.tp_y;
            while (TSC2046.pressure > 1) { TSC2046.CheckTouch(); }// wait for release of pen
            WS_ILI9488.LineDrawH(display1, 225, 275, 50, 0xFFFF);
            WS_ILI9488.LineDrawV(display1, 250, 250, 50, 0xFFFF);
            while (TSC2046.pressure < 5) { TSC2046.CheckTouch(); }//wait for pen pressure
            screenPoints[2].x = 250;
            screenPoints[2].y = 275;
            touchPoints[2].x = TSC2046.tp_x;
            touchPoints[2].y = TSC2046.tp_y;
            while (TSC2046.pressure > 1) { TSC2046.CheckTouch(); } // wait for release of pen

            TSC2046.setCalibration(screenPoints, touchPoints);
            if (await TSC2046.CalibrationMatrix.SaveCalData(TSC2046CalFilename))
            {
                Status.Text = TSC2046.CalibrationMatrix.message; 
            }
            else
            {
                Status.Text = TSC2046.CalibrationMatrix.message;
            }
            WS_ILI9488.Flush(display1);
        }
        private async void Green_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/GREEN.png");
        }
        private async void Blue_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/BLUE.png");
        }
        private async void Red_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/RED.png");
        }
        private async void Image1_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/image1 320.png");
        }
        private async void Image2_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/image2 320.png");
        }
        private async void Image3_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/image3 320.png");
        }
        private async void Image4_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/image4 320.jpg");
        }
        private async void Image5_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/image5 320.png");
        }
        private async void Image6_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/Element14 Logo.png");
        }
        private async void Image7_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/Me 320.jpg");
        }
        private async void Image8_Click(object sender, RoutedEventArgs e)
        {
            await loadImageToScreen("ms-appx:///assets/Breadboard 320.png");
        }
        private async Task loadImageToScreen(string imageName)
        {
            try
            {
                await WS_ILI9488.LoadBitmap(display1, 190, 10, 280, 220, imageName);
                var myBrush = new ImageBrush();
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(imageName))
                };
                myBrush.ImageSource = image.Source;
                imageArea.Background = myBrush;
            }
            catch (Exception ex)
            {
                Status.Text = ex.Message;
            }
        }

        private async Task checkAction(int x, int y)
        {
            if (x > 20 && x < 90 && y > 20 && y < 320)
            {
                // first column of buttons
                int button = (y-20) /(320/8);
                switch (button)
                {
                    case 0: await loadImageToScreen("ms-appx:///assets/RED.png"); break;
                    case 1: await loadImageToScreen("ms-appx:///assets/GREEN.png"); break;
                    case 2: await loadImageToScreen("ms-appx:///assets/BLUE.png"); break;
                    case 3: calibrateTouch(); break;
                    case 4: await doDemo(); break;
                    case 5:  break;
                    case 6: App.Current.Exit(); break;
                }

            }
            else if (x > 140 && x < 180 && y > 20 && y < 229)
            {
                // second column of buttons
                int button = (y - 20) / (229 /9);
                switch (button)
                {
                    case 0: await loadImageToScreen("ms-appx:///assets/image1 320.png"); break;
                    case 1: await loadImageToScreen("ms-appx:///assets/image2 320.png"); break;
                    case 2: await loadImageToScreen("ms-appx:///assets/image3 320.png"); break;
                    case 3: await loadImageToScreen("ms-appx:///assets/image4 320.jpg"); break;
                    case 4: await loadImageToScreen("ms-appx:///assets/image5 320.png"); break;
                    case 5: await loadImageToScreen("ms-appx:///assets/Element14 Logo.png"); break;
                    case 6: await loadImageToScreen("ms-appx:///assets/Me 320.jpg"); break;
                    case 7: await loadImageToScreen("ms-appx:///assets/Breadboard 320.png"); break;
                }

            }

        }
        private async Task doDemo()
        {
            int LoopDelay = 1000;
            try
            {
                WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);
                WS_ILI9488.fillRect(display1, 220, 40, 220, 160, 0xFFFF);
                WS_ILI9488.fillRect(display1, 250, 70, 160, 100, 0x0000);
                WS_ILI9488.fillRect(display1, 280, 100, 100, 40, 0xFFFF);
                await Task.Delay((int)LoopDelay);

                WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);
                for (UInt16 x = 0; x < 220 / 2; x += 4)
                {
                    UInt16 x1 = (UInt16)(x + 190);
                    UInt16 y1 = (UInt16)(x + 10);
                    UInt16 x2 = (UInt16)((280 - x * 2));
                    UInt16 y2 = (UInt16)((220 - x * 2));
                    WS_ILI9488.LineDrawH(display1, x1, y1, x2, 0xFFFF);
                    WS_ILI9488.LineDrawH(display1, x1, (UInt16)(y1 + y2), x2, 0xFFFF);
                    WS_ILI9488.LineDrawV(display1, x1, y1, y2, 0xFFFF);
                    WS_ILI9488.LineDrawV(display1, (UInt16)(x1 + x2), y1, y2, 0xFFFF);

                }

                await Task.Delay((int)LoopDelay);

                WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);
                for (int x = 0; x < 50; x++)
                {
                    int x1 = (190 + GenerateRndNumber());
                    int y1 = (10 + GenerateRndNumber());
                    int x2 = (190 + GenerateRndNumber());
                    int y2 = (10 + GenerateRndNumber());
                    WS_ILI9488.drawLine(display1, (UInt16)x1, (UInt16)y1, (UInt16)x2, (UInt16)y2, 0xFe1F);
                }
                await Task.Delay((int)LoopDelay);

                WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);

                WS_ILI9488.Arc(display1, 275, 75, 120, 90, 180, 0xFFFF);
                WS_ILI9488.Arc(display1, 375, 175, 120, 270, 360, 0xFFFF);
                WS_ILI9488.Arc(display1, 275, 175, 120, 0, 90, 0xFFFF);
                WS_ILI9488.Arc(display1, 375, 75, 120, 180, 270, 0xFFFF);

                WS_ILI9488.DrawCircle(display1, 325, 125, 100, 0xF81F);
                await Task.Delay((int)LoopDelay);

                WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);
                WS_ILI9488.setCursor(display1, 195, 30);
                WS_ILI9488.write(display1, "Hello".ToCharArray(), 2, 0xFFFF);
                WS_ILI9488.setCursor(display1, 195, 47);
                WS_ILI9488.write(display1, "Hi There".ToCharArray(), 1, 0xFFFF);
                await Task.Delay((int)LoopDelay);

                WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);
                WS_ILI9488.setCursor(display1, 195, 30);
                WS_ILI9488.write(display1, "Hi There".ToCharArray(), 1, 0xFF00);
                WS_ILI9488.setCursor(display1, 195, 40);
                WS_ILI9488.write(display1, "Hi There".ToCharArray(), 2, 0x00FF);
                WS_ILI9488.setCursor(display1, 195, 70);
                WS_ILI9488.write(display1, "Hi There".ToCharArray(), 4, 0xFFFF);
                WS_ILI9488.setCursor(display1, 195, 120);
                WS_ILI9488.write(display1, "Hi There".ToCharArray(), 6, 0xF81F);
                await Task.Delay((int)LoopDelay);

                for (byte x = 1; x < 6; x++)
                {
                    WS_ILI9488.fillRect(display1, 190, 10, 280, 220, 0x0000);
                    WS_ILI9488.setCursor(display1, 195, 30);
                    WS_ILI9488.write(display1, "Hi there".ToCharArray(), x, 0xFFFF);
                    await Task.Delay((int)LoopDelay / 4);
                    WS_ILI9488.setCursor(display1, 195, 30);
                    WS_ILI9488.write(display1, "Hi there".ToCharArray(), x, 0x0000);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }
        private async void btnDemo_Click(object sender, RoutedEventArgs e)
        {
            await doDemo();
        }
        public UInt16 GenerateRndNumber()
        {
            // Generate a random number.
            UInt32 Rnd = CryptographicBuffer.GenerateRandomNumber() % 220; //limit between 0 and 220
            return (UInt16)Rnd;
        }


}
}

