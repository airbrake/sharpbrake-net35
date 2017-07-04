using System.Globalization;
using Newtonsoft.Json;

namespace Sharpbrake.Client.Model
{
    /// <summary>
    /// Object that describes the current notifier library.
    /// </summary>
    public class NotifierInfo
    {
        /// <summary>
        /// The name of the notifier client submitting the request.
        /// </summary>
        [JsonProperty("name")]
        public string Name
        {
            get { return "sharpbrake"; }
        }

        /// <summary>
        /// The version number of the notifier client 
        /// submitting the request, e.g. "1.2.3".
        /// </summary>
        [JsonProperty("version")]
        public string Version
        {
            get
            {
                var version = typeof(NotifierInfo).Assembly.GetName().Version;
                // in the Version class Microsoft uses the next versioning schema: major.minor[.build[.revision]]
                return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            }
        }

        /// <summary>
        /// A URL at which more information can be obtained concerning the notifier client.
        /// </summary>
        [JsonProperty("url")]
        public string Url
        {
            get { return "https://github.com/airbrake/sharpbrake-net35"; }
        }
    }
}
