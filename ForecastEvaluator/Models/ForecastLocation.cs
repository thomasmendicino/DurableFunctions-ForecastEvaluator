namespace ForecastEvaluator.Models
{
    public class ForecastLocation
    {
        public string Location { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string address { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string postalCode { get; set; }
    }
}