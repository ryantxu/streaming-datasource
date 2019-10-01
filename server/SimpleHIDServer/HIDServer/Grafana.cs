using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;


namespace HIDServer
{
    public class GrafanaQuery
    {
        public static DateTime FromUnixMS(long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime.ToLocalTime();
        }

        public static long ToUnixTime(DateTime t)
        {
            return (t.ToUniversalTime().Ticks - 621355968000000000) / 10000;
        }
    }
}
