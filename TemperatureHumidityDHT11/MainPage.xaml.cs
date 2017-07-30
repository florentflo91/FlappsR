using System;
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
using System.Diagnostics;
using Windows.Devices.Gpio;
using Microsoft.IoT.Lightning.Providers;
using System.Data.Common;
using System.Data;
using SQLite.Net.Attributes;
using Windows.Storage;

namespace TemperatureHumidityDHT11
{
    public sealed partial class MainPage : Page
    {
        string path;
        SQLite.Net.SQLiteConnection conn;

        public MainPage()
        {
            
            this.InitializeComponent();

            path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "db.sqlite");
            //StorageFolder storageFolder = KnownFolders.DocumentsLibrary;
            //path = Path.Combine(storageFolder.Path, "db.sqlite");
            


            // path = @"C:\Data\Users\DefaultAccount\AppData\Local\FloPi\db.sqlite";
            conn = new SQLite.Net.SQLiteConnection(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), path);
            conn.CreateTable<Message>();

            initializeGpio();
            testClock();
            if (dht11Pin_ != null)
            {
                initializeSensor();
                initializeTimer();
            }
        }

        private void initializeSensor()
        {
            sensor_ = new DHT11Sensor(dht11Pin_, cronometer_);
            sensor_.initialize();
            float tpr = sensor_.measureTicksPerRead(testPin_, 100);
            float tpw = sensor_.measureUsPerWrite(testPin_, 100);

            textBlockPins.Text = string.Format("Pin read op {0}us, write op {1}us", tpr, tpw);
        }

        private void initializeTimer()
        {
            timer_ = new DispatcherTimer();
            timer_.Interval = TimeSpan.FromSeconds(5);
            timer_.Tick += getTemperatureHumidity;
            timer_.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        private bool initializeGpio()
        {
          if (LightningProvider.IsLightningEnabled) {

            Windows.Devices.LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
            textBlockStatus.Text = "Gpio using low level controller";
          }
          else {
            textBlockStatus.Text = "Gpio using default driver";
          }

          var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                textBlockStatus.Text = "Gpio initialization error";
                return false;
            }

            testPin_ = gpio.OpenPin(27);
            testPin_.SetDriveMode(GpioPinDriveMode.Output);
            dht11Pin_ = gpio.OpenPin(4);
            dht11Pin_.SetDriveMode(GpioPinDriveMode.Input);

            textBlockStatus.Text = "Gpio initialized!";
            return true;
        }


        private void testClock()
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

            double d = 123.4567;

            long frequency = Stopwatch.Frequency;
            long nsPerTick = 1000000000 / frequency;
            long ticksPerUs = frequency / 1000000;
            long ticksPerMs = frequency / 1000;

            watch.Start();

            long t1 = watch.ElapsedTicks;
            d = d / 3.33333;
            long t2 = watch.ElapsedTicks;

            textBlockTimer.Text = string.Format("isHighPrec:{0}, ticks/sec={1:N}, ns/tick={2:N}, ticks/us={3:N}, ticks/ms={4:N}, ticks/div op={5:N}, div={6:N}",
                    Stopwatch.IsHighResolution, frequency, nsPerTick, ticksPerUs, ticksPerMs, t2 - t1, d);
        }

        //private void timerTick(object sender, object args)
        //{
        //    textBlockTemperatureHumidity.Text = "Start reading the temperature";
        //    String displayStr = "";
        //    int[] data = new int[4];
        //    PrecisionCronometer c = new PrecisionCronometer();
        //    c.start();
        //    long t = c.ticks;

        //    if (sensor_.read())
        //    {
        //        sensor_.getData(data);
        //        displayStr = string.Format("Temperature:{0}.{1}, humidity:{2}.{3} ({4}us)", 
        //                    data[0], data[1], data[2], data[3], c.ticksToUs(c.ticks - t));
        //    }
        //    else
        //    {
        //        sensor_.getData(data);
        //        displayStr = sensor_.getErrorString() + string.Format(", d[0]={0},d[1]={1},d[2]={2},d[3]={3} ({4}us)", 
        //                    data[0], data[1], data[2], data[3], c.ticksToUs(c.ticks - t));
        //    }
        //    textBlockTemperatureHumidity.Text = displayStr;
        //}


        private void getTemperatureHumidity(object sender, object args)
        {
            int[] data = new int[4];
            PrecisionCronometer c = new PrecisionCronometer();
            c.start();
            long t = c.ticks;
            DateTime tnow = DateTime.Now;
            if (sensor_ == null)
            {
                textBoxHumidityTemperature.Foreground = new SolidColorBrush(Windows.UI.Colors.Blue);
                textBoxHumidityTemperature.Text = string.Format("Temperature:{0}.{1}, humidity:{2}.{3} ({4}us)",
                            data[0], data[1], data[2], data[3], c.ticksToUs(c.ticks - t));
               
                return;
            }

            ellipseErrorLed.Fill = new SolidColorBrush(Windows.UI.Colors.LightSalmon);
            ellipseTHLed.Fill = new SolidColorBrush(Windows.UI.Colors.LightGreen);

            if (sensor_.read())
            {
              ellipseTHLed.Fill = new SolidColorBrush(Windows.UI.Colors.Green);
              sensor_.getData(data);
              textBoxHumidityTemperature.Foreground = new SolidColorBrush(Windows.UI.Colors.Blue);
              
              textBoxHumidityTemperature.Text = string.Format("Hum:{0}.{1}, temp:{2}.{3} ({4}us) {5}",
                            data[0], data[1], data[2], data[3], c.ticksToUs(c.ticks - t), tnow);
               AddMessagev1(string.Format("Hum:{0}.{1}, temp:{2}.{3} ({4}us) {5}",
                            data[0], data[1], data[2], data[3], c.ticksToUs(c.ticks - t), tnow));
            }
            else
            {
              ellipseErrorLed.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
              sensor_.getData(data);
              textBoxError.Foreground = new SolidColorBrush(Windows.UI.Colors.Red);
              textBoxError.Text = sensor_.getErrorString() + string.Format(", d[0]={0},d[1]={1},d[2]={2},d[3]={3} ({4}us)",
                            data[0], data[1], data[2], data[3], c.ticksToUs(c.ticks - t));
            }
        }

        private void RetrieveMessage(object sender, RoutedEventArgs e)
        {
            var query = conn.Table<Message>();

            string text = "";
            foreach (var message in query)
            {
                text = text + " " + message.Content;
            }
            textBlock.Text = text;
        }

        private void AddMessage(object sender, RoutedEventArgs e)
        {
            var s = conn.Insert(new Message()
            {
                Content = textBox.Text
            });


        }
        private void AddMessagev1(string sender)
        {
            var s = conn.Insert(new Message()
            {
                Content = sender
            });


        }

        public class Message
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            public string Content { get; set; }
        }

        private void buttonExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }



  
        //private GpioPin testOutPin_;
        private GpioPin testPin_;
        private GpioPin dht11Pin_;
        private DHT11Sensor sensor_;
        private DispatcherTimer timer_;
        private PrecisionCronometer cronometer_ = new PrecisionCronometer();

    
    }
}
