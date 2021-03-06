﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using GIS = GHIElectronics.UWP.Shields;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Diagnostics;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HomeController
{
    public sealed partial class MainPage : Page
    {
        private GIS.FEZHAT hat;

        public MainPage()
        {
            this.InitializeComponent();

            this.Setup();
        }

        private async void Setup()
        {
            this.hat = await GIS.FEZHAT.CreateAsync();
            this.hat.D2.Color = GIS.FEZHAT.Color.Black;
            this.hat.D3.Color = GIS.FEZHAT.Color.Black;

           
            //mqtt
            if (client == null)
            {
                // create client instance 
                MQTT_BROKER_ADDRESS = "cloud.makestro.com";
                client = new MqttClient(MQTT_BROKER_ADDRESS);
                string clientId = Guid.NewGuid().ToString();
                client.Connect(clientId, "mifmasterz", "123qweasd");
                SubscribeMessage();

            }
        }
        public MqttClient client { set; get; }

        public string MQTT_BROKER_ADDRESS
        {
            set; get;
        }

        void SubscribeMessage()
        {

            // register to message received 

            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            client.Subscribe(new string[] { "mifmasterz/assistant/data", "mifmasterz/assistant/control" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

        }
        
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)

        {

            string Pesan = Encoding.UTF8.GetString(e.Message);

            switch (e.Topic)

            {

                case "mifmasterz/assistant/data":


                    break;

                case "mifmasterz/assistant/control":

                    switch (Pesan)
                    {
                        case "LIGHT_ON":
                            this.hat.D2.Color = GIS.FEZHAT.Color.Blue;
                            this.hat.D3.Color = GIS.FEZHAT.Color.Blue;
                            break;
                        case "LIGHT_OFF":
                            this.hat.D2.Color = GIS.FEZHAT.Color.Black;
                            this.hat.D3.Color = GIS.FEZHAT.Color.Black;
                            break;
                    }
                    break;


            }
            Debug.WriteLine(e.Topic + ":" + Pesan);
        }
    }
}
