using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForecastEvaluator.Models
{
    public class ForecastDetail
    {
        public ForecastProperties properties { get; set; }
    }

    public class ForecastProperties
    {
        public List<ForecastPeriod> periods { get; set; }
    }

    public class ForecastPeriod
    {
        public int number { get; set; }
        public string name { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public Boolean isDaytime { get; set; }
        public int temperature { get; set; }
        public string temperatureUnit { get; set; }
        public string windSpeed { get; set; }
        public string windDirection { get; set; }
        public string shortForecast { get; set; }

    }
}
