using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using System.Net.Http;
using BingMapsRESTToolkit;
using ForecastEvaluator.Models;

namespace ForecastEvaluator
{
    public static class ForecastEvaluator
    {
        // Create a single, static HttpClient
        private static HttpClient httpClient = new HttpClient() { BaseAddress = new Uri("https://api.weather.gov/") };

        // Arbitrary name to supply to nws api
        private static string functionName = "tmdevtest";

        [FunctionName("ForecastEvaluator_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            ForecastLocation input = ParseRequest(req);
            log.LogInformation($"Received request with input: {input.address}, {input.city}, {input.state}, {input.postalCode}");

            string instanceId = "";

            if (input != null)
            {
                // Function input comes from the request content.
                instanceId = await starter.StartNewAsync("ForecastEvaluator", input);

                log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            }
            
            // Provides details about how to retrieve status and results.
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        private static ForecastLocation ParseRequest(HttpRequestMessage req)
        {
            string jsonContent = req.Content.ReadAsStringAsync().Result;

            ForecastLocation location = JsonConvert.DeserializeObject<ForecastLocation>(jsonContent);

            return location;
        }

        [FunctionName("ForecastEvaluator")]
        public static async Task<List<ForecastSummary>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var outputs = new List<ForecastSummary>();

            ForecastLocation location = context.GetInput<ForecastLocation>();
            log.LogInformation($"Received location: {location}");
            
            double[] coordinates;

            if (location.longitude == 0 || location.latitude == 0)
            {
                coordinates = await context.CallActivityAsync<double[]>("GeocodeLocation", location);
                location.latitude = coordinates[0];
                location.longitude = coordinates[1];
            }                
            else
            {
                coordinates = new double[2];
                coordinates[0] = location.latitude;
                coordinates[1] = location.longitude;
            }

            ForecastDetail forecast = await context.CallActivityAsync<ForecastDetail>("GetForecast", coordinates);

            ForecastSummary summary = CreateSummary(forecast, location);
                        
            outputs.Add(summary);

            return outputs;
        }

        [FunctionName("GeocodeLocation")]
        public static async Task<double[]> GetCoordinates([ActivityTrigger] ForecastLocation location, ILogger log)
        {
            double[] geocodeCoords = new double[2];

            //Create a request.
            var request = new GeocodeRequest()
            {
                Query = $"{location.address}, {location.city}, {location.state}, {location.postalCode}",
                IncludeIso2 = true,
                IncludeNeighborhood = true,
                MaxResults = 25,
                BingMapsKey = System.Environment.GetEnvironmentVariable("bingMapsKey")
            };

            //Process the request by using the ServiceManager.
            var response = await request.Execute();

            if (response != null &&
                response.ResourceSets != null &&
                response.ResourceSets.Length > 0 &&
                response.ResourceSets[0].Resources != null &&
                response.ResourceSets[0].Resources.Length > 0)
            {
                var result = response.ResourceSets[0].Resources[0] as BingMapsRESTToolkit.Location;

                //Do something with the result.
                if (result != null && result.Point != null && result.Point.Coordinates != null)
                {
                    geocodeCoords = result.Point?.Coordinates;

                    log.LogInformation($"Successfully retrieved coordinates for { location.city }. Lat: {geocodeCoords[0]}, Long: {geocodeCoords[1]}");
                }
            }

            return geocodeCoords;
        }

        [FunctionName("GetForecast")]
        public static async Task<ForecastDetail> GetWeatherForecast([ActivityTrigger] double[] coordinates, ILogger log)
        {
            ForecastDetail hourlyForecast = null;

            SetHttpHeader();

            // Call NWS API for location metadata. This will contain the URL to call for weather forecast.
            var locationMetadata = await httpClient.GetAsync($"points/{coordinates[0]},{coordinates[1]}");

            if (locationMetadata.StatusCode == System.Net.HttpStatusCode.OK)
            {
                log.LogInformation("Successfully retrieved location metadata");

                LocationMetadata metadata = JsonConvert.DeserializeObject<LocationMetadata>(locationMetadata.Content.ReadAsStringAsync().Result);

                string hourlyForecastUrl = metadata.properties.forecastHourly.Replace(httpClient.BaseAddress.ToString(), "");

                SetHttpHeader();

                log.LogInformation($"Calling weather API at {httpClient.BaseAddress + hourlyForecastUrl}");

                var hourlyForecastResponse = await httpClient.GetAsync(hourlyForecastUrl);

                hourlyForecast = JsonConvert.DeserializeObject<ForecastDetail>(hourlyForecastResponse.Content.ReadAsStringAsync().Result);
            }

            return hourlyForecast;
        }

        private static void SetHttpHeader()
        {
            httpClient.DefaultRequestHeaders.Accept.Clear();

            httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue(new System.Net.Http.Headers.ProductHeaderValue(functionName)));
        }

        private static ForecastSummary CreateSummary(ForecastDetail hourlyForecast, ForecastLocation location)
        {
            List<int> windSpeeds = new List<int>();

            foreach (ForecastPeriod hours in hourlyForecast.properties.periods)
            {
                if (Int32.TryParse(hours.windSpeed.Replace(" mph", ""),out int speed))
                {
                    windSpeeds.Add(speed);
                }
            }

            ForecastSummary summary = new ForecastSummary();

            summary.location = location;
            int totalWindSpeed = 0;
            int maxWindSpeed = 0;
            foreach (int speed in windSpeeds)
            {
                totalWindSpeed += speed;
                if (speed > maxWindSpeed)
                {
                    maxWindSpeed = speed;
                }
            }
            int averageWindSpeed = totalWindSpeed / windSpeeds.Count;

            summary.average_wind_speed = String.Concat(averageWindSpeed, " mph");

            summary.max_wind_speed = String.Concat(maxWindSpeed, " mph");

            return summary;
        }        
    }
}
