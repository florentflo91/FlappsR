using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TemperatureHumidityDHT11
{
    class PrecisionCronometer
    {

        public void start()
        {
            watch_.Start();
        }
        public void stop()
        {
            watch_.Stop();
        }
        public long ticks
        {
            get { return watch_.ElapsedTicks; }
        }

        public void wait(long us)
        {
            long tk = us * ticksPerUs_;
            long t = watch_.ElapsedTicks + tk;

            while (watch_.ElapsedTicks < t) ;
        }

        public long getTickToWaitFor(long us)
        {
            long tk = us * ticksPerUs_;
            return watch_.ElapsedTicks + tk;
        }

        public long ticksToUs(long t)
        {
            return t / ticksPerUs_;
        }

        public long usToTicks(long us)
        {
            return us * ticksPerUs_;
        }


        Stopwatch watch_ = new Stopwatch();
        static readonly long frequency_ = Stopwatch.Frequency;
        static readonly long ticksPerUs_ = frequency_ / 1000000;
        static readonly long ticksPerMs_ = frequency_ / 1000;
    }
}
