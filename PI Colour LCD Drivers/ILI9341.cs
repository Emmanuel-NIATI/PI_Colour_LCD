using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PI_Colour_LCD
{
    public class ILI9341Display
    {
        public string currentImage;
        public  UInt16 LCD_VERTICAL_MAX = 320; // Y
        public  UInt16 LCD_HORIZONTAL_MAX = 240; // X
        public static UInt16 BLACK = 0x0000;
        public static UInt16 WHITE = 0xFFFF;
        public static UInt16 VERTICAL_MAX_DEFAULT = 320; // y
        public static UInt16 HORIZONTAL_MAX_DEFAULT = 240; // X
        public SpiDevice SpiDisplay;
        public GpioPin DCPin;
        public GpioPin ResetPin;
        public byte cursorX;
        public byte cursorY;
        public byte[] DisplayBuffer; // A working pixel buffer for your code
        public ILI9341Display(string defaultText, int ulValue, int dcpin, int resetpin)
        {
            LCD_VERTICAL_MAX = VERTICAL_MAX_DEFAULT; // Y
            LCD_HORIZONTAL_MAX = HORIZONTAL_MAX_DEFAULT; // X

            //InitSPI(SpiDevice SPI, int SPIpin, int speed, SpiMode mode, string SPI_CONTROLLER_NAME)
            DCPin = ILI9341.InitGPIO(dcpin, GpioPinDriveMode.Output, GpioPinValue.High);
            ResetPin = ILI9341.InitGPIO(resetpin, GpioPinDriveMode.Output, GpioPinValue.High);
            cursorX = 0;
            cursorY = 0;
            DisplayBuffer = new byte[LCD_VERTICAL_MAX * LCD_HORIZONTAL_MAX * 2]; // A working pixel buffer for your code, RGB565
            currentImage = "";
        }
    }
    public static class ILI9341
    {
        private const string SPI_CONTROLLER_NAME = "SPI0";  /* For Raspberry Pi 2, use SPI0                             */

        private static readonly byte[] CMD_SLEEP_OUT = { 0x11 };
        private static readonly byte[] CMD_DISPLAY_ON = { 0x29 };
        private static readonly byte[] CMD_MEMORY_WRITE_MODE = { 0x2C };
        private static readonly byte[] CMD_DISPLAY_OFF = { 0x28 };
        private static readonly byte[] CMD_ENTER_SLEEP = { 0x10 };

        private static readonly byte[] CMD_COLUMN_ADDRESS_SET = { 0x2a };
        private static readonly byte[] CMD_PAGE_ADDRESS_SET = { 0x2b };
        private static readonly byte[] CMD_POWER_CONTROL_A = { 0xcb };
        private static readonly byte[] CMD_POWER_CONTROL_B = { 0xcf };
        private static readonly byte[] CMD_DRIVER_TIMING_CONTROL_A = { 0xe8 };
        private static readonly byte[] CMD_DRIVER_TIMING_CONTROL_B = { 0xea };
        private static readonly byte[] CMD_POWER_ON_SEQUENCE_CONTROL = { 0xed };
        private static readonly byte[] CMD_PUMP_RATIO_CONTROL = { 0xf7 };
        private static readonly byte[] CMD_POWER_CONTROL_1 = { 0xc0 };
        private static readonly byte[] CMD_POWER_CONTROL_2 = { 0xc1 };
        private static readonly byte[] CMD_VCOM_CONTROL_1 = { 0xc5 };
        private static readonly byte[] CMD_VCOM_CONTROL_2 = { 0xc7 };
        private static readonly byte[] CMD_MEMORY_ACCESS_CONTROL = { 0x36 };
        private static readonly byte[] CMD_PIXEL_FORMAT = { 0x3a };
        private static readonly byte[] CMD_FRAME_RATE_CONTROL = { 0xb1 };
        private static readonly byte[] CMD_DISPLAY_FUNCTION_CONTROL = { 0xb6 };
        private static readonly byte[] CMD_ENABLE_3G = { 0xf2 };
        private static readonly byte[] CMD_GAMMA_SET = { 0x26 };
        private static readonly byte[] CMD_POSITIVE_GAMMA_CORRECTION = { 0xe0 };
        private static readonly byte[] CMD_NEGATIVE_GAMMA_CORRECTION = { 0xe1 };

        public static GpioPin InitGPIO(int GPIOpin, GpioPinDriveMode mode, GpioPinValue HiLow)
        {
            var gpio = GpioController.GetDefault();
            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                return null;
            }

            var pin = gpio.OpenPin(GPIOpin);

            if (pin == null)
            {
                return null;
            }
            pin.SetDriveMode(mode);
            pin.Write(HiLow);
            return pin;
        }

        public static async Task InitILI9341DisplaySPI(ILI9341Display display, int SPIDisplaypin, int speed, SpiMode mode, string SPI_CONTROLLER_NAME, string DefaultDisplay)
        {
            var displaySettings = new SpiConnectionSettings(SPIDisplaypin);
            displaySettings.ClockFrequency = speed;// 500kHz;
            displaySettings.Mode = mode; //Mode0,1,2,3;  MCP23S17 needs mode 0
            string DispspiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
            var DispdeviceInfo = await DeviceInformation.FindAllAsync(DispspiAqs);
            display.SpiDisplay = await SpiDevice.FromIdAsync(DispdeviceInfo[0].Id, displaySettings);
            if(String.IsNullOrEmpty(DefaultDisplay))
                InitializeDisplayBuffer(display, ILI9341Display.BLACK);
            else
                await LoadBitmap(display, DefaultDisplay);
            await PowerOnSequence(display);
            await Wakeup(display);
            Flush(display);


        }

        public static async Task PowerOnSequence(ILI9341Display display)
        {
            // assume power has just been turned on
            await Task.Delay(5);
            display.ResetPin.Write(GpioPinValue.Low);   // reset
            await Task.Delay(5);                        // wait 5 ms
            display.ResetPin.Write(GpioPinValue.High);  // out of reset
            await Task.Delay(20);
        }

        public static async Task Wakeup(ILI9341Display display)
        {
            SendCommand(display, CMD_SLEEP_OUT);
            await Task.Delay(60);

            SendCommand(display, CMD_POWER_CONTROL_A);              SendData(display, new byte[] { 0x39, 0x2C, 0x00, 0x34, 0x02 });
            SendCommand(display, CMD_POWER_CONTROL_B);              SendData(display, new byte[] { 0x00, 0xC1, 0x30 });
            SendCommand(display, CMD_DRIVER_TIMING_CONTROL_A);      SendData(display, new byte[] { 0x85, 0x00, 0x78 });
            SendCommand(display, CMD_DRIVER_TIMING_CONTROL_B);      SendData(display, new byte[] { 0x00, 0x00 });

            SendCommand(display, CMD_POWER_ON_SEQUENCE_CONTROL);    SendData(display, new byte[] { 0x64, 0x03, 0x12, 0x81 });
            SendCommand(display, CMD_PUMP_RATIO_CONTROL);           SendData(display, new byte[] { 0x20 });

            SendCommand(display, CMD_POWER_CONTROL_1);              SendData(display, new byte[] { 0x23 });
            SendCommand(display, CMD_POWER_CONTROL_2);              SendData(display, new byte[] { 0x10 });

            SendCommand(display, CMD_VCOM_CONTROL_1);               SendData(display, new byte[] { 0x3e, 0x28 });
            SendCommand(display, CMD_VCOM_CONTROL_2);               SendData(display, new byte[] { 0x86 });

            SendCommand(display, CMD_MEMORY_ACCESS_CONTROL);        SendData(display, new byte[] { 0x48 });
            SendCommand(display, CMD_PIXEL_FORMAT);                 SendData(display, new byte[] { 0x55 });
            SendCommand(display, CMD_FRAME_RATE_CONTROL);           SendData(display, new byte[] { 0x00, 0x18 });
            SendCommand(display, CMD_DISPLAY_FUNCTION_CONTROL);     SendData(display, new byte[] { 0x08, 0x82, 0x27});
            SendCommand(display, CMD_ENABLE_3G);                    SendData(display, new byte[] { 0x00 });
            SendCommand(display, CMD_GAMMA_SET);                    SendData(display, new byte[] { 0x01 });
            SendCommand(display, CMD_POSITIVE_GAMMA_CORRECTION);    SendData(display, new byte[] { 0x0F, 0x31, 0x2B, 0x0C, 0x0E, 0x08, 0x4E, 0xF1, 0x37, 0x07, 0x10, 0x03, 0x0E, 0x09, 0x00 });
            SendCommand(display, CMD_NEGATIVE_GAMMA_CORRECTION);    SendData(display, new byte[] { 0x00,0x0E,0x14,0x03,0x11,0x07,0x31,0xC1,0x48,0x08,0x0F,0x0C,0x31,0x36, 0x0F});

            SendCommand(display, CMD_SLEEP_OUT);
            await Task.Delay(120);
            SendCommand(display, CMD_DISPLAY_ON);
        }

        public static void Sleep(ILI9341Display display)
        {
            SendCommand(display, CMD_DISPLAY_OFF);
            SendCommand(display, CMD_ENTER_SLEEP);
        }

        public static void CleanUp()
        {
            //SpiGPIO.Dispose();
            //ResetPin.Dispose();
            //DataCommandPin.Dispose();
        }
        public static async Task landscapeMode(ILI9341Display display)
        {
            try {
                SendCommand(display, CMD_MEMORY_ACCESS_CONTROL); SendData(display, new byte[] { 0xE8 });
                display.LCD_HORIZONTAL_MAX = ILI9341Display.VERTICAL_MAX_DEFAULT;
                display.LCD_VERTICAL_MAX = ILI9341Display.HORIZONTAL_MAX_DEFAULT;
                await LoadBitmap(display, display.currentImage);
                Flush(display);
            }
            catch (Exception  ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public static async Task PortrateMode(ILI9341Display display)
        {
            try
            {
                SendCommand(display, CMD_MEMORY_ACCESS_CONTROL); SendData(display, new byte[] { 0x48 });
                display.LCD_VERTICAL_MAX = ILI9341Display.VERTICAL_MAX_DEFAULT;
                display.LCD_HORIZONTAL_MAX = ILI9341Display.HORIZONTAL_MAX_DEFAULT;
                await LoadBitmap(display, display.currentImage);
                Flush(display);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        private static void SetAddress(ILI9341Display display, UInt16 x0, UInt16 y0, UInt16 x1, UInt16 y1)
        {
            SendCommand(display, CMD_COLUMN_ADDRESS_SET);
            SendData(display, new byte[] { (byte)(x0 >> 8), (byte)(x0), (byte)(x1 >> 8), (byte)(x1) });
            SendCommand(display, CMD_PAGE_ADDRESS_SET);
            SendData(display, new byte[] { (byte)(y0 >> 8), (byte)(y0), (byte)(y1 >> 8), (byte)(y1) });
        }

        public static ushort RGB24ToRGB565(byte Red, byte Green, byte Blue)
        {
            UInt16 red565 = (UInt16)((Red * 249 + 1014) >> 11);
            UInt16 green565 = (UInt16)((Green * 253 + 505) >> 10);
            UInt16 blue565 = (UInt16)((Blue * 249 + 1014) >> 11);
            return (UInt16)(red565 << 11 | green565 << 5 | blue565);
        }

        public static void InitializeDisplayBuffer(ILI9341Display display, UInt16 colour)
        {
            for (uint i = 0; i < display.LCD_HORIZONTAL_MAX * display.LCD_VERTICAL_MAX; i++)
            {
                display.DisplayBuffer[i*2] = (byte)(colour >> 8);
                display.DisplayBuffer[i*2+1] = (byte)(colour & 0xFF);
            }
        }
        public static async Task LoadBitmap(ILI9341Display display, string name)
        {
            try
            {
                StorageFile srcfile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(name));

                using (IRandomAccessStream fileStream = await srcfile.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);
                    BitmapTransform transform = new BitmapTransform()
                    {
                        ScaledWidth = Convert.ToUInt32(display.LCD_HORIZONTAL_MAX),
                        ScaledHeight = Convert.ToUInt32(display.LCD_VERTICAL_MAX)
                    };
                    PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        transform,
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.DoNotColorManage
                    );
                    byte[] sourcePixels = pixelData.DetachPixelData();
                    if (sourcePixels.Length != display.LCD_HORIZONTAL_MAX * display.LCD_VERTICAL_MAX * 4)
                        return;
                    int pi = 0;
                    for (UInt32 x = 0; x < sourcePixels.Length - 1; x += 4)
                    {
                        // we ignore the alpha value [3]
                        ushort temp = ILI9341.RGB24ToRGB565(sourcePixels[x + 2], sourcePixels[x + 1], sourcePixels[x]);
                        display.DisplayBuffer[pi * 2] = (byte)((temp >> 8) & 0xFF);
                        display.DisplayBuffer[pi * 2 + 1] = (byte)(temp & 0xFF);
                        pi++;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            display.currentImage = name;
        }

        public static void Flush(ILI9341Display display)
        {
            if (display.DisplayBuffer.Length != display.LCD_VERTICAL_MAX * display.LCD_HORIZONTAL_MAX * 2) return;
            SetAddress(display, 0, 0, (UInt16)(display.LCD_HORIZONTAL_MAX - 1), (UInt16)(display.LCD_VERTICAL_MAX - 1));
            int block_size = 51200; // limits of the SPI interface is 64K but this is an even block for display ???
            byte[] buffer = new byte[block_size];
            // now we start to write the buffer out
            SendCommand(display, CMD_MEMORY_WRITE_MODE);
            Array.Copy(display.DisplayBuffer, 0,buffer, 0, 51200);
            SendData(display, buffer);
            Array.Copy(display.DisplayBuffer, 51200, buffer, 0, 51200);
            SendData(display, buffer);
            Array.Copy(display.DisplayBuffer, 51200*2, buffer, 0, 51200);
            SendData(display, buffer);
        }

        private static void SendData(ILI9341Display display, byte[] Data)
        {
            display.DCPin.Write(GpioPinValue.High);
            display.SpiDisplay.Write(Data);
        }

        private static void SendCommand(ILI9341Display display, byte[] Command)
        {
            display.DCPin.Write(GpioPinValue.Low);
            display.SpiDisplay.Write(Command);
        }
    }
}
