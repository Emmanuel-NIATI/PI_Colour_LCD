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
    public class HX8357Display
    {
        public string currentImage;
        public  UInt16 LCD_VERTICAL_MAX = 480; // Y
        public  UInt16 LCD_HORIZONTAL_MAX = 320; // X
        public static UInt16 BLACK = 0x0000;
        public static UInt16 WHITE = 0xFFFF;
        public static UInt16 VERTICAL_MAX_DEFAULT = 480; // y
        public static UInt16 HORIZONTAL_MAX_DEFAULT = 320; // X
        public SpiDevice SpiDisplay;
        public GpioPin DCPin;
        public GpioPin ResetPin;
        public byte cursorX;
        public byte cursorY;
        public byte[] DisplayBuffer; // A working pixel buffer for your code
        public HX8357Display(string defaultText, int ulValue, int dcpin, int resetpin)
        {
            LCD_VERTICAL_MAX = VERTICAL_MAX_DEFAULT; // Y
            LCD_HORIZONTAL_MAX = HORIZONTAL_MAX_DEFAULT; // X

            //InitSPI(SpiDevice SPI, int SPIpin, int speed, SpiMode mode, string SPI_CONTROLLER_NAME)
            DCPin = WS_HX8357.InitGPIO(dcpin, GpioPinDriveMode.Output, GpioPinValue.High);
            ResetPin = WS_HX8357.InitGPIO(resetpin, GpioPinDriveMode.Output, GpioPinValue.High);
            cursorX = 0;
            cursorY = 0;
            DisplayBuffer = new byte[LCD_VERTICAL_MAX * LCD_HORIZONTAL_MAX * 2]; // A working pixel buffer for your code, RGB565
            currentImage = "";
        }
    }
    public static class WS_HX8357
    {
        private const string SPI_CONTROLLER_NAME = "SPI0";  /* For Raspberry Pi 2, use SPI0                             */

        private static readonly byte padding = 0x00; 
        // for making stuff into 16bit as it is using a shift register on the Waveshare board
        // if your using true SPI then you can remove the padding bytes

        private static readonly byte[] HX8357_ENTER_SLEEP =                 { padding, 0x10 };
        private static readonly byte[] HX8357_EXIT_SLEEP_MODE =             { padding, 0x11 };
        private static readonly byte[] HX8357_SET_INVOFF =                  { padding, 0x20 };
        private static readonly byte[] HX8357_SET_INVON =                   { padding, 0x21 };
        private static readonly byte[] CMD_GAMMA_SET =                      { padding, 0x26 };
        private static readonly byte[] HX8357_SET_DISPLAY_OFF =             { padding, 0x28 };
        private static readonly byte[] HX8357_SET_DISPLAY_ON =              { padding, 0x29 };
        private static readonly byte[] HX8357_SET_COLUMN_ADDRESS =          { padding, 0x2A };
        private static readonly byte[] HX8357_SET_PAGE_ADDRESS =            { padding, 0x2B };
        private static readonly byte[] HX8357_WRITE_MEMORY_START =          { padding, 0x2C };
        private static readonly byte[] HX8357_READ_MEMORY_START =           { padding, 0x2E };

        private static readonly byte[] HX8357_SET_TEAR_ON =                 { padding, 0x35 };
        private static readonly byte[] HX8357_SET_ADDRESS_MODE =            { padding, 0x36 };
        private static readonly byte[] HX8357_SET_PIXEL_FORMAT =            { padding, 0x3A };
        private static readonly byte[] HX8357_WRITE_MEMORY_CONTINUE =       { padding, 0x3C };
        private static readonly byte[] HX8357_READ_MEMORY_CONTINUE =        { padding, 0x3E };
        private static readonly byte[] HX8357_SET_INTERNAL_OSCILLATOR =     { padding, 0xB0 };
        private static readonly byte[] HX8357_SET_POWER_CONTROL =           { padding, 0xB1 };
        private static readonly byte[] HX8357_SET_DISPLAY_MODE =            { padding, 0xB4 };
        private static readonly byte[] HX8357_SET_VCOM_VOLTAGE =            { padding, 0xB6 };
        private static readonly byte[] HX8357_ENABLE_EXTENSION_COMMAND =    { padding, 0xB9 };
        private static readonly byte[] HX8357_SET_PANEL_DRIVING =           { padding, 0xC0 };
        private static readonly byte[] HX8357_SET_PANEL_CHARACTERISTIC =    { padding, 0xCC };
        private static readonly byte[] HX8357_SET_GAMMA_CURVE =             { padding, 0xE0 };



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

        public static async Task InitHX8357DisplaySPI(HX8357Display display, int SPIDisplaypin, int speed, SpiMode mode, string SPI_CONTROLLER_NAME, string DefaultDisplay)
        {
            var displaySettings = new SpiConnectionSettings(SPIDisplaypin);
            displaySettings.ClockFrequency = speed;// 500kHz;
            displaySettings.Mode = mode; //Mode0,1,2,3;  MCP23S17 needs mode 0
            string DispspiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
            var DispdeviceInfo = await DeviceInformation.FindAllAsync(DispspiAqs);
            display.SpiDisplay = await SpiDevice.FromIdAsync(DispdeviceInfo[0].Id, displaySettings);
            if(String.IsNullOrEmpty(DefaultDisplay))
                InitializeDisplayBuffer(display, HX8357Display.BLACK);
            else
                await LoadBitmap(display, DefaultDisplay);
            await PowerOnSequence(display);
            await Wakeup(display);
            Flush(display);


        }

        public static async Task PowerOnSequence(HX8357Display display)
        {
            // assume power has just been turned on
            await Task.Delay(5);
            display.ResetPin.Write(GpioPinValue.Low);   // reset
            await Task.Delay(5);                        // wait 5 ms
            display.ResetPin.Write(GpioPinValue.High);  // out of reset
            await Task.Delay(20);
        }

        public static async Task Wakeup(HX8357Display display)
        {
            // the WAVESHARE uses a 16bit shift register and a counter to latch in data so you have to send 16bits min for anything to work
            // if your using true SPI then you can remove the padding bytes
            //Parameters are also not set on the high byte so you need to padd all the parameters with leading 00

            SendCommand(display, HX8357_EXIT_SLEEP_MODE);
            await Task.Delay(60);

            SendCommand(display, HX8357_ENABLE_EXTENSION_COMMAND);  SendData(display, new byte[] { padding, 0xFF, padding, 0x83, padding, 0x57 });
            SendCommand(display, HX8357_SET_POWER_CONTROL);         SendData(display, new byte[] { padding, 0x00, padding, 0x12, padding, 0x12, padding, 0x12, padding, 0xC3, padding, 0x44 });
            SendCommand(display, HX8357_SET_DISPLAY_MODE);          SendData(display, new byte[] { padding, 0x02, padding, 0x40, padding, 0x00, padding, 0x2A, padding, 0x2A, padding, 0x20, padding, 0x91});
            SendCommand(display, HX8357_SET_VCOM_VOLTAGE);          SendData(display, new byte[] { padding, 0x38 });

            SendCommand(display, HX8357_SET_INTERNAL_OSCILLATOR);    SendData(display, new byte[] { padding, 0x68 });
            SendCommand(display, new byte[] { padding, 0xE3 });      SendData(display, new byte[] { padding, 0x2F, padding, 0x1F });
            SendCommand(display, new byte[] { padding, 0xB5 });      SendData(display, new byte[] { padding, 0x01, padding, 0x01, padding, 0x67 });

            SendCommand(display, HX8357_SET_PANEL_DRIVING);         SendData(display, new byte[] { padding, 0x70, padding, 0x70, padding, 0x01, padding, 0x3C, padding, 0xC8, padding, 0x08 });
            SendCommand(display, new byte[] { padding, 0xC2 });     SendData(display, new byte[] { padding, 0x00, padding, 0x08, padding, 0x04 });

            SendCommand(display, HX8357_SET_PANEL_CHARACTERISTIC);  SendData(display, new byte[] { padding, 0x09 });
            SendCommand(display, HX8357_SET_GAMMA_CURVE);           SendData(display, new byte[] {padding,  0x01,padding,  0x02,padding,  0x03,padding,  0x05,padding,  0x0E, padding, 0x22,padding,  0x32, padding, 0x3B,padding,  0x5C,padding,  0x54,padding,  0x4C,padding,  0x41,padding,  0x3D,padding,  0x37,padding,  0x31,padding,  0x21,
                                                                                                  padding,   0x01, padding, 0x02, padding, 0x03, padding, 0x05,padding,  0x0E, padding, 0x22,padding,  0x32, padding, 0x3B, padding, 0x5C, padding, 0x54, padding, 0x4C, padding, 0x41,padding,  0x3D,padding,  0x37,padding,  0x31, padding, 0x21,padding,  0x00, padding, 0x01 });
            SendCommand(display, HX8357_SET_PIXEL_FORMAT);          SendData(display, new byte[] { padding, 0x55 });
            SendCommand(display, HX8357_SET_ADDRESS_MODE);          SendData(display, new byte[] { padding, 0x00 });
            SendCommand(display, HX8357_SET_TEAR_ON);               SendData(display, new byte[] { padding, 0x00 });
            SendCommand(display, HX8357_SET_DISPLAY_ON);           
            SendCommand(display, HX8357_WRITE_MEMORY_START);    
        }

        public static void Sleep(HX8357Display display)
        {
            SendCommand(display, HX8357_SET_DISPLAY_OFF);
            SendCommand(display, HX8357_ENTER_SLEEP);
        }

        public static void CleanUp()
        {
            //SpiGPIO.Dispose();
            //ResetPin.Dispose();
            //DataCommandPin.Dispose();
        }
        public static async Task landscapeMode(HX8357Display display)
        {
            try {
                SendCommand(display, HX8357_SET_ADDRESS_MODE); SendData(display, new byte[] { 0x00, 0x60 });
                display.LCD_HORIZONTAL_MAX = HX8357Display.VERTICAL_MAX_DEFAULT;
                display.LCD_VERTICAL_MAX = HX8357Display.HORIZONTAL_MAX_DEFAULT;
                await LoadBitmap(display, display.currentImage);
                Flush(display);
            }
            catch (Exception  ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public static async Task PortrateMode(HX8357Display display)
        {
            try
            {
                SendCommand(display, HX8357_SET_ADDRESS_MODE); SendData(display, new byte[] { 0x00, 0x00 });
                display.LCD_VERTICAL_MAX = HX8357Display.VERTICAL_MAX_DEFAULT;
                display.LCD_HORIZONTAL_MAX = HX8357Display.HORIZONTAL_MAX_DEFAULT;
                await LoadBitmap(display, display.currentImage);
                Flush(display);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        private static void SetAddress(HX8357Display display, UInt16 x0, UInt16 y0, UInt16 x1, UInt16 y1)
        {
            SendCommand(display, HX8357_SET_COLUMN_ADDRESS);
            SendData(display, new byte[] {padding,  (byte)(x0 >> 8), padding, (byte)(x0), padding, (byte)(x1 >> 8), padding, (byte)(x1) });
            SendCommand(display, HX8357_SET_PAGE_ADDRESS);
            SendData(display, new byte[] { padding, (byte)(y0 >> 8), padding, (byte)(y0), padding, (byte)(y1 >> 8), padding, (byte)(y1) });
        }

        public static ushort RGB24ToRGB565(byte Red, byte Green, byte Blue)
        {
            UInt16 red565 = (UInt16)((Red * 249 + 1014) >> 11);
            UInt16 green565 = (UInt16)((Green * 253 + 505) >> 10);
            UInt16 blue565 = (UInt16)((Blue * 249 + 1014) >> 11);
            return (UInt16)(red565 << 11 | green565 << 5 | blue565);
        }

        public static void InitializeDisplayBuffer(HX8357Display display, UInt16 colour)
        {
            for (uint i = 0; i < display.LCD_HORIZONTAL_MAX * display.LCD_VERTICAL_MAX; i++)
            {
                display.DisplayBuffer[i*2] = (byte)(colour >> 8);
                display.DisplayBuffer[i*2+1] = (byte)(colour & 0xFF);
            }
        }
        public static async Task LoadBitmap(HX8357Display display, string name)
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
                        ushort temp = WS_HX8357.RGB24ToRGB565(sourcePixels[x + 2], sourcePixels[x + 1], sourcePixels[x]);
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

        public static void Flush(HX8357Display display)
        {
            if (display.DisplayBuffer.Length != display.LCD_VERTICAL_MAX * display.LCD_HORIZONTAL_MAX * 2) return;
            SetAddress(display, 0, 0, (UInt16)(display.LCD_HORIZONTAL_MAX - 1), (UInt16)(display.LCD_VERTICAL_MAX - 1));
            int block_size = 51200; // limits of the SPI interface is 64K but this is an even block for display ???
            byte[] buffer = new byte[block_size];
            // now we start to write the buffer out
            SendCommand(display, HX8357_WRITE_MEMORY_START);
            Array.Copy(display.DisplayBuffer, 0,buffer, 0, 51200);
            SendData(display, buffer);
            Array.Copy(display.DisplayBuffer, 51200, buffer, 0, 51200);
            SendData(display, buffer);
            Array.Copy(display.DisplayBuffer, 51200*2, buffer, 0, 51200);
            SendData(display, buffer);
        }

        private static void SendData(HX8357Display display, byte[] Data)
        {
            display.DCPin.Write(GpioPinValue.High);
            display.SpiDisplay.Write(Data);
        }

        private static void SendCommand(HX8357Display display, byte[] Command)
        {
            display.DCPin.Write(GpioPinValue.Low);
            display.SpiDisplay.Write(Command);
        }
    }
}
