using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RewatchTranscriptFetcher.Services
{
    public class RewatchService
    {
        private readonly HttpClient _httpClient;
        private readonly bool _debugMode;

        public RewatchService(bool debugMode = false)
        {
            _httpClient = new HttpClient();
            _debugMode = debugMode;
        }

        public async Task<string> FetchTranscriptsAsync(string subdomain, string apiKey, DateTime startDate, DateTime endDate)
        {
            var transcripts = new StringBuilder();
            var debugLog = new StringBuilder();
            int meetingCount = 0;
            string endCursor = null;

            var overallStopwatch = Stopwatch.StartNew();
            var keepGoing = true;

            do
            {
                var queryStopwatch = Stopwatch.StartNew();

                string query = @"
                    query ($endCursor: String) {
                        channel {
                            videos(first: 100, after: $endCursor, orderBy: {field: CREATED_AT, direction: DESC}) {
                                edges {
                                    node {
                                        id
                                        title
                                        createdAt
                                        transcript {
                                            sections {
                                                startTime
                                                endTime
                                                cues {
                                                    startTime
                                                    endTime
                                                    speakerName
                                                    text
                                                }
                                            }
                                            summary {
                                                recap
                                                actionItems {
                                                    content
                                                    completed
                                                }
                                                chapterMarkers {
                                                    title
                                                    items {
                                                        content
                                                    }
                                                }
                                                summaryItems {
                                                    content
                                                }
                                            }
                                        }
                                    }
                                }
                                pageInfo {
                                    endCursor
                                    hasNextPage
                                }
                            }
                        }
                    }";

                var variables = new { endCursor };

                var requestStopwatch = Stopwatch.StartNew();
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://{subdomain}.rewatch.com/api/graphql"),
                    Headers = { { "Authorization", $"Token token=\"{apiKey}\"" } },
                    Content = new StringContent(JsonConvert.SerializeObject(new { query, variables }), Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                requestStopwatch.Stop();

                var processingStopwatch = Stopwatch.StartNew();
                var data = JObject.Parse(jsonResponse)["data"]["channel"]["videos"];
                var edges = data["edges"];
                var pageInfo = data["pageInfo"];

                foreach (var video in edges)
                {
                    var videoNode = video["node"];
                    DateTime createdAt = DateTime.Parse(videoNode["createdAt"].ToString());

                    meetingCount++;

                    if (IsDateInRange(createdAt, startDate, endDate))
                    {
                        transcripts.AppendLine(videoNode["title"]?.ToString());
                        transcripts.AppendLine(new string('-', 50));

                        var transcript = videoNode["transcript"];
                        if (transcript != null)
                        {
                            foreach (var section in transcript["sections"])
                            {
                                if (section["cues"] != null)
                                {
                                    foreach (var cue in section["cues"])
                                    {
                                        var speakerName = cue["speakerName"]?.ToString();
                                        var text = cue["text"]?.ToString();
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            transcripts.AppendLine($"{speakerName}: {text}");
                                        }
                                    }
                                }
                            }
                        }
                        transcripts.AppendLine(new string('-', 50));
                    }
                    else if (createdAt < startDate)
                        keepGoing = false;
                }

                processingStopwatch.Stop();
                queryStopwatch.Stop();

                if (_debugMode)
                {
                    debugLog.AppendLine($"Query execution time: {queryStopwatch.ElapsedMilliseconds} ms");
                    debugLog.AppendLine($"Request time: {requestStopwatch.ElapsedMilliseconds} ms");
                    debugLog.AppendLine($"Processing time: {processingStopwatch.ElapsedMilliseconds} ms");
                    debugLog.AppendLine(new string('=', 50));
                }

                endCursor = pageInfo["endCursor"]?.ToString();
                bool hasNextPage = (bool)pageInfo["hasNextPage"];

                if (!hasNextPage)
                {
                    break;
                }

                // TODO: Check if the date is outside of our range and end the query loop.

            } while (!string.IsNullOrEmpty(endCursor) && keepGoing);

            overallStopwatch.Stop();

            if (_debugMode)
            {
                debugLog.AppendLine($"Overall execution time: {overallStopwatch.ElapsedMilliseconds} ms");
                File.WriteAllText("debug_log.txt", debugLog.ToString());
            }

            var result = new StringBuilder();
            result.AppendLine($"Meeting count: {meetingCount}");
            result.AppendLine(new string('=', 50));
            result.Append(transcripts.ToString());

            return result.ToString();
        }

        private bool IsDateInRange(DateTime date, DateTime startDate, DateTime endDate)
        {
            // Normalize start date to the beginning of the day before the specified start date
            DateTime start = startDate.Date; // 00:00:00 of the day before the start date
            DateTime end = endDate.Date.AddDays(1).AddTicks(-1); // End of the end date (23:59:59.9999999)

            return date >= start && date <= end;
        }
    }
}
