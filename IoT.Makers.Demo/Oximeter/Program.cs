using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using Microsoft.SPOT.Hardware;
using GHI.Processor;
using GHI.Glide;
using GHI.Glide.Geom;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using Microsoft.SPOT.Net.NetworkInformation;

namespace Oximeter
{
    public partial class Program
    {
        public static MqttClient client { set; get; }
        string MQTT_BROKER_ADDRESS = "13.76.210.96";
        //UI
        GHI.Glide.UI.Image img = null;
        GHI.Glide.UI.TextBlock txtLora = null;
        GHI.Glide.UI.TextBlock txtStatus = null;
        GHI.Glide.UI.TextBlock txtSPO2 = null;
        GHI.Glide.UI.TextBlock txtSignal = null;
        GHI.Glide.UI.TextBlock txtPulseRate = null;
        GHI.Glide.UI.TextBlock txtDesc = null;
        GHI.Glide.Display.Window window = null;
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            multicolorLED.BlinkOnce(GT.Color.Red);
            //7" Displays
            Display.Width = 800;
            Display.Height = 480;
            Display.OutputEnableIsFixed = false;
            Display.OutputEnablePolarity = true;
            Display.PixelPolarity = false;
            Display.PixelClockRateKHz = 30000;
            Display.HorizontalSyncPolarity = false;
            Display.HorizontalSyncPulseWidth = 48;
            Display.HorizontalBackPorch = 88;
            Display.HorizontalFrontPorch = 40;
            Display.VerticalSyncPolarity = false;
            Display.VerticalSyncPulseWidth = 3;
            Display.VerticalBackPorch = 32;
            Display.VerticalFrontPorch = 13;
            Display.Type = Display.DisplayType.Lcd;
            if (Display.Save())      // Reboot required?
            {
                PowerState.RebootDevice(false);
            }
            //set up touch screen
            CapacitiveTouchController.Initialize(GHI.Pins.FEZRaptor.Socket14.Pin3);

            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.MyForm));
            //glide init
            GlideTouch.Initialize();

            GHI.Glide.UI.Button btn = (GHI.Glide.UI.Button)window.GetChildByName("btnTest");
            img = (GHI.Glide.UI.Image)window.GetChildByName("img1");
            txtLora = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtLora");
            txtStatus = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtStatus");
            txtSPO2 = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtSPO2");
            txtSignal = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtSignal");
            txtPulseRate = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtPulseRate");
            txtDesc = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtDesc");
            img.Visible = false;

            btn.TapEvent += btn_TapEvent;

            Glide.MainWindow = window;
            JoinWifi();
            Thread.Sleep(500);
            if (client == null)
            {
                // create client instance 
                MQTT_BROKER_ADDRESS = "cloud.makestro.com";
                client = new MqttClient(MQTT_BROKER_ADDRESS);
                string clientId = Guid.NewGuid().ToString();
                client.Connect(clientId, "mifmasterz", "123qweasd");
                SubscribeMessage();

            }
            Thread th1 = new Thread(new ThreadStart(Loop));
            th1.Start();
        }
        void btn_TapEvent(object sender)
        {
            Bitmap bmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.setan), Bitmap.BitmapImageType.Jpeg);
            img.Visible = true;
            img.Bitmap = bmp;

            img.Invalidate();
            Thread.Sleep(2000);
            img.Visible = false;
            img.Invalidate();
            window.Invalidate();
        }
        void Loop()
        {
            //loop forever
            for (; ; )
            {
                if (pulseOximeter.IsProbeAttached)
                {
                    var msg = "";
                    //get data from oximeter
                    var item = new DataSensor() { SPO2 = pulseOximeter.LastReading.SPO2, PulseRate = pulseOximeter.LastReading.PulseRate, SignalStrength = pulseOximeter.LastReading.SignalStrength, Tanggal = DateTime.Now };
                    txtStatus.Text = "Read data from sensor...";
                    txtSPO2.Text = "SPO2 : " + item.SPO2;
                    if ((long)item.SPO2 >= 95)
                        msg += "alhamdulilah sehat bang! ";
                    else
                        msg += "antum kurang tidur nih, kurang oksigen. ";
                    txtPulseRate.Text = "Pulse Rate : " + item.PulseRate;
                    if ((long)item.PulseRate >= 60 && (long)item.PulseRate <= 100)
                        msg += "detak jantung normal. ";
                    else
                        msg += "detak jantung abnormal. ";
                    //update display
                    txtDesc.Text = msg;
                    txtLora.Text = "Lora Status : OK";
                    window.Invalidate();
                    txtLora.Invalidate();
                    txtStatus.Invalidate();
                    txtSPO2.Invalidate();
                    txtSignal.Invalidate();
                    txtPulseRate.Invalidate();
                    txtDesc.Invalidate();
                    var Pesan = Encoding.UTF8.GetBytes(Json.NETMF.JsonSerializer.SerializeObject(item));
                    client.Publish("mifmasterz/medical/data", Pesan, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);

                }
                Thread.Sleep(50);
            }
        }

        void SubscribeMessage()
        {
            //event handler saat message dari mqtt masuk
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            //subcribe ke topik tertentu di mqtt
            client.Subscribe(new string[] { "mifmasterz/medical/data", "mifmasterz/medical/control" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

        }

        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            //conversi byte message ke string
            string pesan = new string(Encoding.UTF8.GetChars(e.Message));
            if (e.Topic == "mifmasterz/medical/data")
            {
                switch (pesan)
                {
                  
                    default:
                        break;
                }
            }
            //cetak ke console tuk kebutuhan debug
            Debug.Print("Message : " + pesan);
        }

        void JoinWifi()
        {
            var SSID = "wifi berbayar";
            var WifiKey = "123qweasd";
            //setup network
            wifiRS21.DebugPrintEnabled = true;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            //setup network
            wifiRS21.NetworkInterface.Open();
            wifiRS21.NetworkInterface.EnableDhcp();
            wifiRS21.NetworkInterface.EnableDynamicDns();
            if (SSID.Length > 0 && WifiKey.Length > 0)
            {
                GHI.Networking.WiFiRS9110.NetworkParameters[] info = wifiRS21.NetworkInterface.Scan(SSID);
                if (info != null)
                {
                    wifiRS21.NetworkInterface.Join(SSID, WifiKey);
                    PrintLine("Waiting for DHCP...");
                    while (wifiRS21.NetworkInterface.IPAddress == "0.0.0.0")
                    {
                        Thread.Sleep(250);
                    }
                    PrintLine("network joined");
                    PrintLine("active network:" + SSID);
                    Thread.Sleep(250);

                    ListNetworkInterfaces();
                }
                else
                {
                    PrintLine("SSID cannot be found!");
                    
                }
            }
        }
        private static void NetworkChange_NetworkAddressChanged(object sender, Microsoft.SPOT.EventArgs e)
        {
            Debug.Print("Network address changed");
        }

        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Debug.Print("Network availability: " + e.IsAvailable.ToString());
        }
        void ListNetworkInterfaces()
        {
            var settings = wifiRS21.NetworkSettings;

            PrintLine("------------------------------------------------");
            PrintLine("MAC: " + ByteExt.ToHexString(settings.PhysicalAddress, "-"));
            PrintLine("IP Address:   " + settings.IPAddress);
            PrintLine("DHCP Enabled: " + settings.IsDhcpEnabled);
            PrintLine("Subnet Mask:  " + settings.SubnetMask);
            PrintLine("Gateway:      " + settings.GatewayAddress);
            PrintLine("------------------------------------------------");
       
        }
        void PrintLine(string ObjStr)
        {
            Debug.Print(ObjStr);
        }
    }
    public static class ByteExt
    {
        private static char[] _hexCharacterTable = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

#if MF_FRAMEWORK_VERSION_V4_1
    public static string ToHexString(byte[] array, string delimiter = "-")
#else
        public static string ToHexString(this byte[] array, string delimiter = "-")
#endif
        {
            if (array.Length > 0)
            {
                // it's faster to concatenate inside a char array than to
                // use string concatenation
                char[] delimeterArray = delimiter.ToCharArray();
                char[] chars = new char[array.Length * 2 + delimeterArray.Length * (array.Length - 1)];

                int j = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    chars[j++] = (char)_hexCharacterTable[(array[i] & 0xF0) >> 4];
                    chars[j++] = (char)_hexCharacterTable[array[i] & 0x0F];

                    if (i != array.Length - 1)
                    {
                        foreach (char c in delimeterArray)
                        {
                            chars[j++] = c;
                        }

                    }
                }

                return new string(chars);
            }
            else
            {
                return string.Empty;
            }
        }
    }
    //driver for touch screen
    public class CapacitiveTouchController
    {
        private InterruptPort touchInterrupt;
        private I2CDevice i2cBus;
        private I2CDevice.I2CTransaction[] transactions;
        private byte[] addressBuffer;
        private byte[] resultBuffer;

        private static CapacitiveTouchController _this;

        public static void Initialize(Cpu.Pin PortId)
        {
            if (_this == null)
                _this = new CapacitiveTouchController(PortId);
        }

        private CapacitiveTouchController()
        {
        }

        private CapacitiveTouchController(Cpu.Pin portId)
        {
            transactions = new I2CDevice.I2CTransaction[2];
            resultBuffer = new byte[1];
            addressBuffer = new byte[1];
            i2cBus = new I2CDevice(new I2CDevice.Configuration(0x38, 400));
            touchInterrupt = new InterruptPort(portId, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeBoth);
            touchInterrupt.OnInterrupt += (a, b, c) => this.OnTouchEvent();
        }

        private void OnTouchEvent()
        {
            for (var i = 0; i < 5; i++)
            {
                var first = this.ReadRegister((byte)(3 + i * 6));
                var x = ((first & 0x0F) << 8) + this.ReadRegister((byte)(4 + i * 6));
                var y = ((this.ReadRegister((byte)(5 + i * 6)) & 0x0F) << 8) + this.ReadRegister((byte)(6 + i * 6));

                if (x == 4095 && y == 4095)
                    break;

                if (((first & 0xC0) >> 6) == 1)
                    GlideTouch.RaiseTouchUpEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
                else
                    GlideTouch.RaiseTouchDownEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
            }
        }

        private byte ReadRegister(byte address)
        {
            this.addressBuffer[0] = address;

            this.transactions[0] = I2CDevice.CreateWriteTransaction(this.addressBuffer);
            this.transactions[1] = I2CDevice.CreateReadTransaction(this.resultBuffer);

            this.i2cBus.Execute(this.transactions, 1000);

            return this.resultBuffer[0];
        }
    }
    public class DataSensor
    {
        public DateTime Tanggal { set; get; }
        public int PulseRate { set; get; }
        public int SignalStrength { set; get; }
        public int SPO2 { set; get; }
    }
}
