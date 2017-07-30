
using Windows.Devices.Gpio;

namespace TemperatureHumidityDHT11
{
    /// <summary>
    /// 
    /// </summary>
    class DHT11Sensor
    {
        enum Error
        {
            Sucess = 0,
            StartSignalLowNotSet = 1,
            ReadValueTimeout = 2,
            ReadValueNotDetected = 3, 
            ReadValueToShort = 4

        };
        enum Context
        {
            Null = 0,
            SendStartSignal = 1,
            WaitForAckReadLow = 2,
            WaitForAckReadHigh = 3,
            ReadBitsReadLow = 4,
            ReadBitsReadHigh = 5,

        };

        public DHT11Sensor(GpioPin pin, PrecisionCronometer c)
        {
            pin_ = pin;
            cronometer_ = c;
            cronometer_.start();
        }

        /// <summary>
        /// initialize the sensor
        /// </summary>
        public void initialize()
        {
            cronometer_.start();
        }

        /// <summary>
        /// Resets the data members, errors and time related members.
        /// </summary>
        private void reset()
        {
            for (int i = 0; i < SIZE; i++) {
                data_[i] = 0;
            }
            error_ = Error.Sucess;
            strError_ = "";
            cronometer_.stop();
            cronometer_.start();
        }

        /// <summary>
        /// Gets the array of 4 bytes, where it reads the humidity and temperature.
        /// </summary>
        /// <param name="data">
        /// Humidity and temperature, d[0] decimal humidity part, d[1] fractional 
        /// humidity part, d[2] decimal temperature part, d[3] fractional temperature
        /// </param>
        public void getData(int []data)
        {
            if (data.Length >= SIZE - 1) {
                for(int i = 0; i < SIZE - 2; i++) {
                    data[i] = data_[i];
                }
            }
        }

        public void getDatav2(int[] data)
        {
            /* laststate 1=HIGH 0=LOW*/
            GpioPinValue laststate = GpioPinValue.High;
            pin_.SetDriveMode(GpioPinDriveMode.Output);

            pin_.Write(GpioPinValue.Low);
            cronometer_.wait(18000);//-usPerWrite_

            pin_.Write(GpioPinValue.High);
            cronometer_.wait(40);//-usPerWrite_

            pin_.SetDriveMode(GpioPinDriveMode.Input);


            /* detect change and read data */
            int j = 0;
            float f;
            for (int i = 0; i < 85; i++)
            {
                int counter = 0;
                while (pin_.Read() == laststate)
                {
                    counter++;
                    cronometer_.wait(1);

                    if (counter == 255)
                    {
                        break;
                    }
                }

                laststate = pin_.Read();

                if (counter == 255)
                    break;

                /* ignore first 3 transitions */
                if ((i >= 4) && (i % 2 == 0))
                {
                    /* shove each bit into the storage bytes */

                    data[j / 8] <<= 1;
                    if (counter > 16)
                        data[j / 8] |= 0x1;
                    j++;
                }

            }
                /*
	             * check we read 40 bits (8bit x 5 ) + verify checksum in the last byte
	             * print it out if data is good
	             */
            if ((j >= 40) &&
                     (data[4] == ((data[0] + data[1] + data[2] + data[3]) & 0xFF)))
                {
                //f = data[2] * 9. / 5. + 32;
                //printf("Humidity = %d.%d %% Temperature = %d.%d *C (%.1f *F)\n",
                // data[0], data[1], data[2], data[3], f);
                //data[1] = 1;

                }
                else
                {
                //printf("Data not good, skip\n");
                data[0] = 2;
                data[1] = 2;
                data[2] = 2;
                data[3] = 2;
            }
            



           













        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="testPin"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public float measureTicksPerRead(GpioPin testPin, int n)
        {
            testPin.SetDriveMode(GpioPinDriveMode.Input);
            long t1 = cronometer_.ticks;
            for (int i = 1; i < n; i++) {
                GpioPinValue val =  testPin.Read();
            }
            long t2 = cronometer_.ticks;
            ticksPerRead_ = t2 - t1;
            usPerRead_ = (float)cronometer_.ticksToUs(ticksPerRead_)/(float)n;
            return usPerRead_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="testPin"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public float measureUsPerWrite(GpioPin testPin, int n)
        {
            testPin.SetDriveMode(GpioPinDriveMode.Output);
            long t1 = cronometer_.ticks;
            for (int i = 1; i < n; i++) {
                if (i % 2 == 0) {
                    testPin.Write(GpioPinValue.Low);
                }
                else {
                    testPin.Write(GpioPinValue.High);
                }
            }
            long t2 = cronometer_.ticks;
            ticksPerWrite_ = t2 - t1;
            usPerWrite_ = (float)cronometer_.ticksToUs(ticksPerWrite_)/(float)n;
            return usPerWrite_;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Error sendStartSignal()
        {
            pin_.SetDriveMode(GpioPinDriveMode.Output);

            pin_.Write(GpioPinValue.Low);
            cronometer_.wait(18000);//-usPerWrite_

            pin_.Write(GpioPinValue.High);
            cronometer_.wait(30);//-usPerWrite_

            pin_.SetDriveMode(GpioPinDriveMode.Input);

            int count = 0;
            while(count++<100) {
                if (pin_.Read() == GpioPinValue.Low) {
                    return Error.Sucess;
                }
            }
            strError_ += ", fn:sendStartSignal err:LowNotRead";
            error_ = Error.StartSignalLowNotSet;
            return error_;
        }

        /// <summary>
        /// Waits for the sensor acknowledge signal, pin pull down to low for
        /// 80 us then pin pull up to high for 80 us.
        /// </summary>
        /// <returns></returns>
        private Error waitForAcknowledge()
        {
            long dt;

            if (readValue(GpioPinValue.Low, 100, out dt) == Error.Sucess) {
                if (readValue(GpioPinValue.High, 100, out dt) == Error.Sucess) {
                    return Error.Sucess;
                }
                else {
                    strError_ += ", fn:WaitForAck err:ReadHigh";
                }
            }
            else {
                strError_ += ", fn:WaitForAck err:ReadLow";
            }

            return error_;
        }

        /// <summary>
        /// Reads the 40 bits of data. For each bit the sensor would pull down
        /// the line for 50 us and then pull it up for 26-28 us for a 0 or 70 us 
        /// for a 1.
        /// </summary>
        /// <returns></returns>
        private Error readBits()
        {
            long dt;
            int j = 0;
            for (int i = 1; i <= 40; i++) {
                if (readValue(GpioPinValue.Low, 70, out dt) == Error.Sucess) {
                    j = (i - 1) / 8;
                    data_[j] <<= 1;
                    if (readValue(GpioPinValue.High, 90, out dt) == Error.Sucess) {
                        if (dt >= 60) {
                            data_[j] |= 0x1;
                        }
                        else if (dt < 10) {
                            strError_ += string.Format(", fn:ReadBits err:ReadHighToShort dt:{0} bit:{1}", dt, i);
                            return Error.ReadValueToShort;
                        }
                    }
                    else {
                        strError_ += string.Format(", fn:ReadBits err:ReadHigh dt:{0} bit:{1}", dt, i);
                        return error_;
                    }
                }
                else {
                    strError_ += string.Format(", fn:ReadBits err:ReadLow dt:{0} bit:{1}", dt, i);
                    return error_;
                }
                
            }
            return Error.Sucess;
        }


        /// <summary>
        /// Reads the humidity and temperature from phisical sensor. It sends the
        /// start signal by setting up the line to low fro 18 ms, then it waits for
        /// the sensor to respond with an acknowledge followed by 40 bits of data.
        /// </summary>
        /// <returns></returns>
        public bool read()
        {
            reset();
            if(sendStartSignal() == Error.Sucess) {
                if (waitForAcknowledge() == Error.Sucess) {
                    return readBits() == Error.Sucess && checkData() == true;
                }
            }
            return false;
        }



        /// <summary>
        /// Verify the checksum for the read data, the last byte should be equal
        /// with the sum of the rest.
        /// </summary>
        /// <returns></returns>
        public bool checkData()
        {
            return data_[4] == ((data_[3] + data_[2] + data_[1] + data_[0]) & 0xFF);
        }

        /// <summary>
        /// Would read the pin until it has value = val or a timeout occurs.
        /// </summary>
        /// <param name="val">Value to be read</param>
        /// <param name="us">The maximum time interval to read the value.</param>
        /// <param name="dt">The time interval when the values was read.</param>
        /// <returns></returns>
        private Error readValue(GpioPinValue val, long us, out long dt)
        {
            GpioPinValue lastReadValue;
            int readsNr = 0;
            long tstart = cronometer_.ticks;
            long tend = cronometer_.getTickToWaitFor(us);
            do {
                lastReadValue = pin_.Read();
                if (lastReadValue == val) {
                    readsNr++;
                }
                else {
                    break;
                }
            }
            while (cronometer_.ticks < tend);

            dt = cronometer_.ticksToUs(cronometer_.ticks - tstart);

            if (lastReadValue == val) {
                strError_ += string.Format(", fn:ReadValue err:Timeout dt:{0} us:{1} val:{2} reads:{3}", dt, us, val, readsNr);
                error_ = Error.ReadValueTimeout;
                return error_;
            }
            else {
                if (readsNr == 0) {
                    strError_ += string.Format(", fn:ReadValue err:NotDetected dt:{0} us:{1} val:{2} reads:{3}", dt, us, val, readsNr);
                    error_ = Error.ReadValueNotDetected;
                    return error_;
                }
                return Error.Sucess;
            }
        }

        /// <summary>
        /// Returns the error string that would contain: functions were the error
        ///   occured, the error description, some useful variables.
        /// </summary>
        /// <returns></returns>
        public string getErrorString()
        {
            return strError_;
        }

        private const int SIZE = 5;
        private GpioPin pin_;
        private long ticksPerWrite_ = 0;
        private float usPerWrite_ = 0;
        private long ticksPerRead_ = 0;
        private float usPerRead_ = 0;
        private PrecisionCronometer cronometer_;
        private int[] data_ = new int[SIZE];

        private Error error_ = Error.Sucess;
        private string strError_ = "";

        public object Console { get; private set; }
    }
}
