using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AnimalsAutoML_ConsoleApp1
{
    public class RekognitionService
    {
        private readonly AmazonRekognitionClient _client;

        public RekognitionService(string awsAccessKey, string awsSecretAccessKey)
        {
            _client = new AmazonRekognitionClient(
                awsAccessKey,
                awsSecretAccessKey,
                RegionEndpoint.EUCentral1
            );
        }

        public async Task<DetectLabelsResponse> AnalyzeAsync(byte[] imageBytes)
        {
            var request = new DetectLabelsRequest
            {
                Image = new Image
                {
                    Bytes = new MemoryStream(imageBytes)
                },

                MaxLabels = 10,
                MinConfidence = 75F,

                Features = new List<string>
                {
                    "GENERAL_LABELS",
                    "IMAGE_PROPERTIES"
                }
            };

            return await _client.DetectLabelsAsync(request);
        }

        public async Task<List<TextDetection>> DetectTextAsync(byte[] imageBytes)
        {
            var request = new DetectTextRequest
            {
                Image = new Image
                {
                    Bytes = new MemoryStream(imageBytes)
                }
            };

            var response = await _client.DetectTextAsync(request);

            return response.TextDetections;
        }

        public async Task<CompareFacesResponse> CompareFacesAsync(
            byte[] sourceBytes,
            byte[] targetBytes)
        {
            var request = new CompareFacesRequest
            {
                SourceImage = new Image
                {
                    Bytes = new MemoryStream(sourceBytes)
                },

                TargetImage = new Image
                {
                    Bytes = new MemoryStream(targetBytes)
                },

                SimilarityThreshold = 0F
            };

            return await _client.CompareFacesAsync(request);
        }

        public async Task<List<ModerationLabel>> DetectModerationAsync(
            byte[] imageBytes)
        {
            var request = new DetectModerationLabelsRequest
            {
                Image = new Image
                {
                    Bytes = new MemoryStream(imageBytes)
                },

                MinConfidence = 50F
            };

            var response =
                await _client.DetectModerationLabelsAsync(request);

            return response.ModerationLabels;
        }
    }
}