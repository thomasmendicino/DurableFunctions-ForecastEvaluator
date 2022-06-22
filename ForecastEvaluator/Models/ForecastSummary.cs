namespace ForecastEvaluator.Models
{
    public class ForecastSummary
    {
        public string average_wind_speed { get; set; }
        public string max_wind_speed { get; set; }
        public ForecastLocation location { get; set; }
    }
}