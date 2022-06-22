using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForecastEvaluator.Models
{
    public class LocationMetadata
    {
        public MetadataProperties properties { get; set; }
    }

    public class MetadataProperties
    {
        public string forecastHourly { get; set; }
    }
}
