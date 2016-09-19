using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;


namespace FooTube
{
    /// <summary>
    /// Allows you to enter a YouTube-Channel name and fetch data
    /// mainly for the purpose of me dicking around with the YouTube_API 
    /// 
    /// Have a great stalk! 
    /// 
    /// 
    /// [TO-DO:]
    /// 
    /// BUGS:
    ///     - [DONE] only returns 5 videos despite &limit=10
    ///     
    /// FUNCTIONAL:
    ///     - [DONE] VERY UNRELIABLE -- can only find IDs of larger channels due to the way YouTube handles channels (=> YT sucks)
    ///     - [DONE] implement loop, so the process can be repeated
    ///     - [DONE] add channel class to access properties from first 
    ///     - [DONE] create method to handle request/response cycle
    ///     - [DONE] access views-property on videos, possibly need another request for this
    ///     
    /// </summary>




    class Program
    {
        // API_KEY should be constant 
        // defined via google-developer-console
        const string API_KEY = "";

        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("==== Enter the channel you wish to stalk: ====");
                var user = Console.ReadLine();
                Console.WriteLine("");

                // Sadly the search?-request requires a channel's ID rather than the username
                // Therefore we have to make two requests, first one:
                var channel = GetChannel(user);

                // In case GetID() returned null goto end of the loop---
                if (channel == null)
                {
                    // Try our alternate method, see GetChannelAlt for reference
                    channel = GetChannelAlt(user);

                    if (channel == null)
                    {
                        Console.WriteLine("\n====! Error: Channel name returned no ID. Make sure to type a valid channel name. !====\n");
                        goto Outer;
                    }
                }

                Console.WriteLine(channel);
                Console.WriteLine("");

                // Second one:
                var videos = GetVideos(channel.ID);

                // In case GetVideos() returned no videos -- which it shouldn't
                if (videos.Count == 0)
                {
                    Console.WriteLine("====! Error: Could not find any videos associated with this channel. !====");
                    goto Outer;
                }

                // Finally, print out video info
                // Return to beginning of the main loop
                foreach (Video vid in videos)
                {
                    Console.WriteLine(vid);
                }


            Outer:
                {
                    Console.WriteLine("\n\nPress any key to continue:");
                    Console.ReadKey();
                    Console.Clear();
                    continue;
                }
            }
        }

        #region Methods
        static JObject GetHttpResponse(string requestString)
        {
            JObject result = null;

            // Create a request handler from our requestString
            // Create an object to hold the response, but ---
            var request = WebRequest.Create(requestString);
            WebResponse response;

            // --- initialize it here in case it throws an exception
            try
            {
                response = request.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine("====! " + ex.Message + " !====");
                return null;
            }

            // Check how our response is doing, should be OK
            var status = ((HttpWebResponse)response).StatusCode;
            if (status != HttpStatusCode.OK)
                return null;

            Console.WriteLine("==== Http-Response status: " + status.ToString() + " ====");

            // Read our response and parse it into our J(son)Object
            using (var reader = new StreamReader(response.GetResponseStream()))
                result = (JObject)JsonConvert.DeserializeObject(reader.ReadToEnd());

            return result;
        }

        static Channel GetChannel(string user)
        {
            // This is the basic setup for HTTP requests to the YouTube API
            string requestString = "https://www.googleapis.com/youtube/v3/"     // Necessity, send all requests here
                                    + "channels?"                               // The resource we're looking for 
                                    + "key=" + API_KEY                          // Our API_KEY, Note: can use OAuth2 as an alternative
                                    + "&forUsername=" + user                    // Search parameter
                                    + "&part=id, snippet, statistics"           // We can limit the response to 'parts' to minimize data usage
                                    + "&alt=json";                              // The format of our response. Here: 




            // Create a JSON object that equals the API response
            JObject jChannel = GetHttpResponse(requestString);

            // Entering a non-existing username still returns a valid response for some reason
            // If that's the case our jChannel won't contain an id

            try
            {
                return new Channel(jChannel["items"][0]["snippet"]["title"].ToString(),
                                    jChannel["items"][0]["id"].ToString(),
                                    jChannel["items"][0]["statistics"]["subscriberCount"].ToString());
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        static List<Video> GetVideos(string id)
        {
            // This one is pretty much the same as our GetID method
            // The request string however is a little bit more complex
            string requestString = "https://www.googleapis.com/youtube/v3/"         // ---
                                    + "search?"                                     // Now using the search? requests, which can return 'channel', 'video' or 'playlist'
                                    + "key=" + API_KEY                              // ---
                                    + "&channelId=" + id                            // We can filter our results by using our newly found ID
                                    + "&type=video"                                 // Another filter: We're looking for resources of type 'video'
                                    + "&part=snippet,id"                            // We're interested in the 'snippet' of the resources, contains all the infos
                                    + "&order=date"                                 // Orders resources by date (descending)
                                    + "&maxResults=10"                              // Limits the number of entries to 10
                                    + "&alt=json";                                  // 

            JObject jVideos = GetHttpResponse(requestString);

            // Here we'll store our Video object
            // Created a Video class so it's easier to access properties
            var videos = new List<Video>();

            // Again, we use JSON-formatting to access certain properties in the response
            foreach (var videoEntry in jVideos["items"])
            {
                videos.Add(new Video(videoEntry["snippet"]["title"].ToString(), videoEntry["id"]["videoId"].ToString(), (DateTime)videoEntry["snippet"]["publishedAt"]));
            }

            // Need to use a seperate request to get the views :/
            foreach (var videoEntry in videos)
            {
                GetViews(videoEntry);
            }

            return videos;
        }

        static bool GetViews(Video video)
        {
            string requestString = "https://www.googleapis.com/youtube/v3/"     // ---
                                   + "videos?"                                  // Only videos? contains info on viewsCount
                                   + "key=" + API_KEY                           // ---
                                   + "&id=" + video.ID                          // We can filter our results by using our newly found ID
                                   + "&part=statistics"                         // Contains viewCount
                                   + "&alt=json";                               // ---
            JObject jVideo = GetHttpResponse(requestString);

            // Update our video class
            video.Views = jVideo["items"][0]["statistics"]["viewCount"].ToString();
            video.Likes = jVideo["items"][0]["statistics"]["likeCount"].ToString();
            video.Dislikes = jVideo["items"][0]["statistics"]["dislikeCount"].ToString();


            return true;
        }
        static Channel GetChannelAlt(string username)
        {
            // This method is only called if we run into a common problem caused by YouTube:
            // Smaller channels can't be found via channel?forUsername=[username]
            // In this case we can --try-- to find the channel by filtering channels by their displayed name
            // Problem: more than one channel might contain [username]

            // See GetVideos() and GetChannel() for reference
            string requestString = "https://www.googleapis.com/youtube/v3/" // ---
                                + "search?"                                 // We have to implement search? here
                                + "key=" + API_KEY                          // ---
                                + "&q=" + username                          // Search via display name
                                + "&type=channel"                           // Search for resource of type 'channel'
                                + "&part=snippet"                           // ---
                                + "&alt=json";                              // ---

            JObject jChannel = GetHttpResponse(requestString);

            // YouTube's search algorithms might have blessed us with more than one entry
            // We create an array to hold them all and go through each one to see if we have a match
            JArray jChannelArray = (JArray)jChannel["items"];


            foreach (var entry in jChannelArray)
            {
                if (entry["snippet"]["title"].ToString() == username)
                {
                    return new Channel(entry["snippet"]["title"].ToString(),
                                        entry["snippet"]["channelId"].ToString(),
                                        "[unavailable]");
                }
            }

            return null;
        }

    }
    #endregion

    #region Classes

    public class Video
    {
        public string Title { get; set; }
        public string ID { get; set; }
        public DateTime Date { get; set; }
        public string Views { get; set; }
        public string Likes { get; set; }
        public string Dislikes { get; set; }

        public Video(string title, string id, DateTime date)
        {
            this.Title = title;
            this.Date = date;
            this.ID = id;
            this.Views = "[unavailable]";
            this.Likes = "[unavailable]";
            this.Dislikes = "[unavailable]";
        }

        public override string ToString()
        {
            return string.Format("\n==================================================================\n"
                                    + "==== Title: {0}\n"
                                    + "==== Published: {1}\n"
                                    + "==== Views: {2}\n"
                                    + "==== Likes: {3}\n"
                                    + "==== Dislikes: {4}", Title, Date, Views, Likes, Dislikes);
        }
    }

    public class Channel
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string Subs { get; set; }

        public Channel(string name, string id, string subs)
        {
            this.Name = name;
            this.ID = id;
            this.Subs = subs;
        }

        public override string ToString()
        {
            return string.Format("\n==================================================================\n"
                                    + "==== Name: {0}\n"
                                    + "==== ID: {1}\n"
                                    + "==== Subscribers: {2}", Name, ID, Subs);
        }
    }

    #endregion
}
