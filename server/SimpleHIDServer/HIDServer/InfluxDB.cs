using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Net;
using System.IO.Compression;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System.Net.Cache;

namespace HIDServer
{
    public class InfluxConnection
    {
        public String URL { get; set; }
        public String User { get; set; }
        public String Password { get; set; }
    }

    /// <summary>
    /// See: https://docs.influxdata.com/influxdb/v1.0/write_protocols/line_protocol_reference/#more-on-field-value-types
    /// </summary>
    public enum InfluxFieldType
    {
        FLOAT,  // 64bit!
        INT,    // 64bit!
        STRING, // 64K limit
        BOOLEAN,
        UNKNOWN
    }


    public interface InfluxWriter
    {
        void writeLine(String line);

        void flush();
        
        void WriteDebugInfo(JsonWriter writer);
    }

    public class InfluxQueryInfo
    {
        public string Database { get; set; }

        public string Name { get; set; }

        public string Query { get; set; }

        public void Write(JsonWriter writer)
        {
            writer.WriteStartObject();
            if(Database != null)
            {
                writer.WritePropertyName("database");
                writer.WriteValue(Database);
            }
            if (Name != null)
            {
                writer.WritePropertyName("name");
                writer.WriteValue(Name);
            }
            if (Query != null)
            {
                writer.WritePropertyName("query");
                writer.WriteValue(Query);
            }
            writer.WriteEndObject();
        }

        public String NormalizeQuery()
        {
            return Query
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Replace('"', ' ')
                .Replace(Database +".autogen.", string.Empty)
                .Replace(Database + ".\"default\".", string.Empty)
                .Replace(" ", string.Empty).Trim();
        }

        public InfluxQueryInfo Replace(String key, String val)
        {
            InfluxQueryInfo copy = new InfluxQueryInfo();
            copy.Database = this.Database;
            copy.Name = this.Name.Replace(key, val);
            copy.Query = this.Query.Replace(key, val);
            return copy;
        }

        public bool IsSameQuery(InfluxQueryInfo q)
        {
            String v0 = this.NormalizeQuery();
            String v1 = q.NormalizeQuery();
            return v0.Equals(v1);
        }
    }

    public class InfluxRetentionPolicy
    {
        public string Database { get; set; }

        public string Name { get; set; }

        public string Duration { get; set; }

        public int Replication { get; set; } = 1;

        public bool IsDefault { get; set; } = false;

        public InfluxRetentionPolicy()
        {

        }

        public InfluxRetentionPolicy(String db, string name)
        {
            this.Database = db;
            this.Name = name;
        }

        public String ToCreateStatement()
        {
            return "CREATE RETENTION POLICY " + Name 
                  + " ON " + Database
                  + " DURATION " + Duration 
                  + " REPLICATION " + Replication 
                  + ( IsDefault ? " DEFAULT" : "");
        }
        public String ToDropStatement()
        {
            return "DROP RETENTION POLICY " + Name
                  + " ON " + Database;
        }
    }

    public class InfluxSeries
    {
        public string Name { get; set; }
        public string[] Columns { get; set; }

        public List<JArray> Values { get; set; }

        public int GuessInterval()
        {
            if(Values == null || Values.Count<2)
            {
                return 0;
            }

            int count = 0;
            long total = 0;
            long prev = Values[0].Value<JArray>()[0].Value<long>();
            for (int i=1; i<Values.Count; i++)
            {
                long ts = Values[i].Value<JArray>()[0].Value<long>();
                long diff = prev-ts;
                total += diff;
                count++;
            }
            return (int)(total / (double)count);
        }
        
        public static string EscapeInfluxString(string v)
        {
            if(v.Length > 2048)
            {
                v = v.Substring(0, 2010) + "... ("+v.Length + " bytes original)";
            }
            return v.Replace("\"", "\\\"");
        }
    }

    public class InfluxDiagnostics
    {
        public string version;
        public long deltaTS;
        public long started;
        public int pid;
    }
    

    public interface InfluxDB
    {
        InfluxConnection Connection { get; }

        /// <summary>
        /// Check if the server is running
        /// </summary>
        /// <returns>The Version String</returns>
        String Ping();

        InfluxDiagnostics ReadDiagnostics();

        List<string> ShowDatabases();

        List<string> ShowMeasurements(string db);

        bool CreateDatabase(string name);

        bool DropDatabase(string name);

        HashSet<string> GetTagKeys(string db, string measurment);

        IDictionary<string,InfluxFieldType> GetFieldInfo(string db, string measurment);
        
        InfluxWriter getWriter(string database);

        IDictionary<String, InfluxQueryInfo> ShowContinuousQueries(string db);

        IDictionary<String, InfluxRetentionPolicy> ShowRetentionPolicies(string db);

        bool CreateRetentionPolicy(InfluxRetentionPolicy p);

        bool DropRetentionPolicy(InfluxRetentionPolicy p);

        bool CreateContinuousQuery(InfluxQueryInfo info);

        bool DropContinuousQuery(InfluxQueryInfo q);

        string ExecutePostQuery(string query, string db = null, string epoch = null );

        InfluxSeries ExecuteQuery(string query, string db, string epoch = "ms");
    }


    public class HttpInfluxDB : InfluxDB
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        readonly InfluxConnection conn;

        public HttpInfluxDB(InfluxConnection c)
        {
            this.conn = c;
        }

        public InfluxConnection Connection
        {
            get
            {
                return conn;
            }
        }

        public HttpWebRequest GetWebRequest(String path)
        {
            HttpWebRequest req = WebRequest.Create(conn.URL + path) as HttpWebRequest;
            req.Timeout = 5000; // 5 seconds
            if (conn.User != null)
            {
                req.Credentials = new NetworkCredential(conn.User, conn.Password);
            }
            req.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            return req;
        }

        public string Ping()
        {
            HttpWebRequest req = GetWebRequest("ping?rand=" + new Random().Next());
            req.Method = "HEAD";

            using (WebResponse res = req.GetResponse())
            {
                return res.Headers.Get("X-Influxdb-Version");
            }
        }
        public InfluxDiagnostics ReadDiagnostics()
        {
            InfluxDiagnostics d = new InfluxDiagnostics();

            DateTime before = DateTime.Now;
            HttpWebRequest req = GetWebRequest("query?q=SHOW DIAGNOSTICS&rand="+new Random().Next());
            req.Method = "GET";
            req.ReadWriteTimeout = 5000; // only allow 5 secs to read the stats

            Dictionary<string, InfluxQueryInfo> queries = new Dictionary<string, InfluxQueryInfo>();
            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                d.version = res.Headers.Get("X-Influxdb-Version");

                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));

                JToken tok;
                JObject results = (JObject)((JArray)JObject.Load(reader).GetValue("results")).First;
                JArray series = (JArray)results.GetValue("series");
                foreach (JToken t in series)
                {
                    JObject sobj = (JObject)t;
                    if( sobj.TryGetValue("name", out tok) )
                    {
                        if("system".Equals(tok.Value<string>()))
                        {
                            JArray arr = sobj.GetValue("values").Value<JArray>().First.Value<JArray>();
                            d.pid = arr[0].Value<int>();
                            
                            String now = arr[1].Value<string>();
                            String sta = arr[2].Value<string>();

                            d.started = GrafanaQuery.ToUnixTime(DateTime.Parse(sta));
                            long nowms = GrafanaQuery.ToUnixTime(DateTime.Parse(now));
                            d.deltaTS = GrafanaQuery.ToUnixTime(before) - nowms; // positive number is ahead
                        }
                    }
                }
            }
            return d;
        }
        

        #region Private Utility

        private List<string> readValues(JsonTextReader reader)
        {
            string prop = "xx";
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    prop = reader.Value.ToString();
                }
                else if (reader.TokenType == JsonToken.StartArray && "values".Equals(prop))
                {
                    List<string> vals = new List<string>(10);
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.String)
                        {
                            vals.Add(reader.Value.ToString());
                        }
                    }
                    return vals;
                }
            }
            return new List<string>(0);
        }

        #endregion


        public List<string> ShowDatabases()
        {
            HttpWebRequest req = GetWebRequest("query?q=SHOW DATABASES");
            req.Method = "GET";

            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));
                return readValues(reader);
            }
        }

        public List<string> ShowMeasurements(string database)
        {
            HttpWebRequest req = GetWebRequest("query?db=" + database + "&q=SHOW MEASUREMENTS");
            req.Method = "GET";

            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));
                return readValues(reader);
            }
        }


        public InfluxSeries ExecuteQuery(string query, string db = null, string epoch = null)
        {
            string res = DoHttpRequest("GET", query, db, epoch);

            InfluxSeries result = new InfluxSeries();
            JsonTextReader reader = new JsonTextReader(new StringReader(res));

            JObject results = (JObject)((JArray)JObject.Load(reader).GetValue("results")).First;

            JToken token = null;
            if (results.TryGetValue("series", out token)) {

                JArray series = (JArray)token;
                foreach (JToken t in series)
                {
                    JObject dbqs = (JObject)t;
                    if (dbqs.TryGetValue("name", out token))
                    {
                        result.Name = token.Value<string>();
                    }

                    JArray arr = dbqs.GetValue("columns") as JArray;
                    result.Columns = new string[arr.Count];
                    for (int i = 0; i < arr.Count; i++)
                    {
                        result.Columns[i] = arr[i].Value<string>();
                    }

                    arr = dbqs.GetValue("values") as JArray;
                    result.Values = new List<JArray>(arr.Count + 1);
                    if (arr != null && arr.Count > 0)
                    {
                        foreach (JToken v in arr)
                        {
                            result.Values.Add(v as JArray);
                        }
                    }
                }
            }
            return result;
        }
        
        public string ExecutePostQuery(string query, string db = null, string epoch = null)
        {
            return DoHttpRequest( "POST", query, db, epoch );
        }


        public string DoHttpRequest( string method, string query, string db = null, string epoch = null,
            Action<HttpWebRequest> prepare = null, Action<Stream> response=null)
        {
            HttpWebRequest req = null;
            String append = "";
            if (db != null)
            {
                append += "&db=" + db;
            }
            if (epoch != null)
            {
                append += "&epoch=" + epoch;
            }
            if (append.Length == 0)
            {
                append = "&";
            }

            if (query.Length > 200)
            {
                req = GetWebRequest("query?" + append.Substring(1));

                var postData = "q=" + HttpUtility.UrlEncode(query);
                var data = Encoding.ASCII.GetBytes(postData);

                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = data.Length;

                using (var stream = req.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            else
            {
                req = GetWebRequest("query?q=" + query + append);
                req.Method = method;
            }

            if(prepare!=null)
            {
                prepare.Invoke(req);
            }
            
            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                if (res.StatusCode == HttpStatusCode.NoContent)
                {
                    return null;
                }
                else if (res.StatusCode == HttpStatusCode.OK)
                {
                    if (response == null)
                    {
                        StreamReader read = new StreamReader(res.GetResponseStream());
                        return read.ReadToEnd();
                    }
                    else
                    {
                        response.Invoke(res.GetResponseStream());
                        return "OK";
                    }
                }
                //throw new Exception(fullResponse);
            }
            return null;
        }

        public bool CreateDatabase(string name)
        {
            ExecutePostQuery( "CREATE DATABASE \"" + name + "\"" );
            return true;
        }

        public bool DropDatabase(string name)
        {
            ExecutePostQuery("DROP DATABASE \"" + name + "\"");
            return true;
        }


        public HashSet<string> GetTagKeys(string db, string measurment)
        {
            HttpWebRequest req = GetWebRequest("query?db=" + db + "&q=SHOW TAG KEYS FROM \"" + measurment + "\"");
            req.Method = "GET";

            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));

                string prop = "xx";
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName)
                    {
                        prop = reader.Value.ToString();
                    }
                    else if (reader.TokenType == JsonToken.StartArray && "values".Equals(prop))
                    {
                        HashSet<string> vals = new HashSet<string>();
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.String)
                            {
                                vals.Add(reader.Value.ToString());
                            }
                        }
                        return vals;
                    }
                }
                return new HashSet<string>();
            }
        }

        public IDictionary<string, InfluxFieldType> GetFieldInfo(string db, string measurment)
        {
            HttpWebRequest req = GetWebRequest("query?db=" + db + "&q=SHOW FIELD KEYS FROM \"" + measurment + "\"");
            req.Method = "GET";

            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));
                
                string prop = "xx";
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName)
                    {
                        prop = reader.Value.ToString();
                    }
                    else if (reader.TokenType == JsonToken.StartArray && "values".Equals(prop))
                    {
                        string fname = null;
                        InfluxFieldType ftype;
                        IDictionary<string, InfluxFieldType> vals = new SortedDictionary<string,InfluxFieldType>();
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.String)
                            {
                                if (fname == null)
                                {
                                    fname = reader.Value.ToString();
                                }
                                else
                                {
                                    switch (reader.Value.ToString())
                                    {
                                        case "integer": ftype = InfluxFieldType.INT; break;
                                        case "float": ftype = InfluxFieldType.FLOAT; break;
                                        case "boolean": ftype = InfluxFieldType.BOOLEAN; break;
                                        case "string": ftype = InfluxFieldType.STRING; break;
                                        default:
                                            throw new Exception("Unknown field type: " + reader.Value);
                                    }
                                    if (vals.ContainsKey(fname))
                                    {
                                        logger.Error("warning.  multiple types fround for: " + db + " @ " + measurment + "/" + fname);
                                    }
                                    else
                                    {
                                        vals.Add(fname, ftype);
                                    }
                                    fname = null;
                                }
                            }
                        }
                        return vals;
                    }
                }
                return new Dictionary<string,InfluxFieldType>(0);
            }
        }

        public InfluxWriter getWriter(String database)
        {
            return new HttpInfluxWriter( this, database, 1024*100 ); // 100K
        }

        public IDictionary<String,InfluxQueryInfo> ShowContinuousQueries(string db)
        {
            HttpWebRequest req = GetWebRequest("query?q=SHOW CONTINUOUS QUERIES");
            req.Method = "GET";

            Dictionary<string,InfluxQueryInfo> queries = new Dictionary<string,InfluxQueryInfo>();
            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));
                
                JObject results = (JObject)((JArray)JObject.Load(reader).GetValue("results")).First;
                JArray series = (JArray)results.GetValue("series");
                foreach(JToken t in series)
                {
                    JObject dbqs = (JObject)t;
                    String dbname = dbqs.GetValue("name").Value<string>();
                    if(dbname.Equals(db))
                    {
                        JArray values = dbqs.GetValue("values") as JArray;
                        if (values != null && values.Count > 0)
                        {
                            foreach (JToken v in values)
                            {
                                JArray aa = v as JArray;
                                InfluxQueryInfo q = new InfluxQueryInfo();
                                q.Database = dbname;
                                q.Name = v[0].Value<string>();
                                q.Query = v[1].Value<string>();
                                queries[q.Name] = q;
                            }
                        }
                        break;
                    }
                }
            }
            return queries;
        }

        public IDictionary<String, InfluxRetentionPolicy> ShowRetentionPolicies(string db)
        {
            HttpWebRequest req = GetWebRequest("query?q=SHOW RETENTION POLICIES on "+db);
            req.Method = "GET";

            Dictionary<string, InfluxRetentionPolicy> policies = new Dictionary<string, InfluxRetentionPolicy>();
            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
            {
                JsonTextReader reader = new JsonTextReader(
                    new StreamReader(res.GetResponseStream()));

                JObject results = (JObject)((JArray)JObject.Load(reader).GetValue("results")).First;
                JArray series = (JArray)results.GetValue("series");
                if(series != null)
                {
                    foreach (JToken t in series)
                    {
                        JObject dbqs = (JObject)t;
                        JArray values = dbqs.GetValue("values") as JArray;
                        if (values != null && values.Count > 0)
                        {
                            foreach (JToken v in values)
                            {
                                JArray aa = v as JArray;
                                InfluxRetentionPolicy q = new InfluxRetentionPolicy();
                                q.Database = db;
                                q.Name = v[0].Value<string>();
                                q.Duration = v[1].Value<string>();
                                q.IsDefault = v[4].Value<bool>();
                                policies[q.Name] = q;
                            }
                        }
                    }
                }
            }
            return policies;
        }


        public bool CreateRetentionPolicy(InfluxRetentionPolicy p)
        {
            if (String.IsNullOrWhiteSpace(p.Database))
            {
                throw new Exception("Missing database");
            }
            ExecutePostQuery( p.ToCreateStatement(), null);
            return true;
        }
        public bool DropRetentionPolicy(InfluxRetentionPolicy p)
        {
            if (String.IsNullOrWhiteSpace(p.Database))
            {
                throw new Exception("Missing database");
            }
            ExecutePostQuery(p.ToDropStatement(), null);
            return true;
        }

        public bool CreateContinuousQuery(InfluxQueryInfo q)
        {
            if(String.IsNullOrWhiteSpace(q.Database))
            {
                throw new Exception("Missing database");
            }
            ExecutePostQuery(q.Query, q.Database);
            return true;
        }

        
        public bool DropContinuousQuery(InfluxQueryInfo q)
        {
            if (String.IsNullOrWhiteSpace(q.Database))
            {
                throw new Exception("Missing database");
            }
            ExecutePostQuery("DROP CONTINUOUS QUERY \""+q.Name+ "\" ON \"" + q.Database + "\"", q.Database);
            return true;
        }


    }


    public abstract class InfluxWriterBuffer : InfluxWriter
    {
        private readonly MemoryStream buffer;
        readonly TextWriter writer;

        int maxBytes;
        int maxLines;
        protected int count = 0;


        public long LastOK { get; internal set; } = 0;
        public long LastERR { get; internal set; } = 0;
        public long SentBytes { get; internal set; } = 0;
        public long ErrorCount { get; internal set; } = 0;
        public long DroppedCount { get; internal set; } = 0;

        public InfluxWriterBuffer(int maxBytes, int maxLines)
        {
            this.buffer = new MemoryStream(maxBytes + 1000);
            this.writer = new StreamWriter(this.buffer);
            this.maxBytes = maxBytes;
            this.maxLines = maxLines;
        }

        public void clear()
        {
            lock(this)
            {
                count = 0;
                buffer.SetLength(0);
            }
        }
        
        public virtual void writeLine(String line)
        {
            if (line == null || line.Length == 0)
            {
                return;
            }

            lock (this)
            {
                writer.Write(line);
                writer.Write('\n'); // NOTE, WriteLine does not work!
                writer.Flush();
            }

            if (buffer.Length > maxBytes || ++count > this.maxLines)
            {
                flush();
            }
        }

        public virtual void WriteDebugInfo(JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("buffer");
            writer.WriteValue(buffer.Length + " / " + buffer.Capacity);

            writer.WritePropertyName("ok");
            writer.WriteValue(LastOK);

            writer.WritePropertyName("sent");
            writer.WriteValue(SentBytes);

            if (ErrorCount > 0)
            {
                writer.WritePropertyName("errors");
                writer.WriteValue(ErrorCount);
            }

            if (LastERR > 0)
            {
                writer.WritePropertyName("err");
                writer.WriteValue(LastERR);
            }

            if (DroppedCount > 0)
            {
                writer.WritePropertyName("dropped");
                writer.WriteValue(DroppedCount);
            }

            writer.WriteEndObject();
        }


        public void flush()
        {
            writer.Flush();
            if (buffer.Length < 1)
            {
                return;
            }

            lock (this)
            {
                try
                {
                    this.doFlush(buffer);
                }
                finally
                {
                    count = 0;
                    buffer.SetLength(0);
                }
            }
        }

        public abstract void doFlush(MemoryStream buffer);
    }


    public class StubInfluxWriter : InfluxWriterBuffer
    {
        private StubInfluxWriter() : base(256, 1)
        {

        }

        public override void writeLine(string line)
        {
            // NOTHING
        }

        public override void doFlush(MemoryStream buffer)
        {

        }


        public override void WriteDebugInfo(JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("buffer");
            writer.WriteValue("stub");

            writer.WriteEndObject();
        }

        public static readonly StubInfluxWriter INSTANCE = new StubInfluxWriter();
    }


    public class HttpInfluxWriter : InfluxWriterBuffer
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public String database;
        readonly HttpInfluxDB influx;
        
        public bool zipit = false;

        public HttpInfluxWriter(HttpInfluxDB influx, String database, int maxBytes) : base(maxBytes, 200)
        {
            this.database = database;
            this.influx = influx;
        }


        public override void doFlush(MemoryStream buffer)
        {
            logger.Debug("Send to influx: " + count + " samples, " + buffer.Length + "bytes");

            count = 0;

            HttpWebRequest req = influx.GetWebRequest("write?precision=ms&db=" + database);
            req.Method = "POST";

            if (zipit)
            {
                req.ContentType = "text/plain";
                req.Headers.Add("Content-Encoding", "gzip");
            }

            if (logger.IsTraceEnabled)
            {
                logger.Trace(Encoding.ASCII.GetString(buffer.ToArray()));
            }

            using (Stream stream = req.GetRequestStream())
            {
                if (zipit)
                {
                    using (GZipStream compressionStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        buffer.WriteTo(compressionStream);
                    }
                }
                else
                {
                    buffer.WriteTo(stream);
                }
            }


            try
            {
                using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
                {
                    LastOK = GrafanaQuery.ToUnixTime(DateTime.Now);
                    SentBytes += buffer.Length;
                    return;
                }
            }
            catch (WebException ex)  // TODO, handle Influx Errors
            {
                ErrorCount++;
                try
                {
                    StreamReader read = new StreamReader(ex.Response.GetResponseStream());
                    String body = read.ReadToEnd();
                    if (ex.Response.ContentType.EndsWith("json"))
                    {
                        JObject v = JObject.Parse(body);
                        string err = v.GetValue("error").Value<string>();
                        logger.Warn("Error Writing To Influx: " + err);
                    }
                    else
                    {
                        logger.Warn("Error Writing To Influx: " + body);
                    }
                    LastERR = GrafanaQuery.ToUnixTime(DateTime.Now);
                }
                catch (Exception xx)
                {
                    logger.Warn(xx, "Error Trying to read WebException: " + ex);
                }
            }
        }
    }

    /// <summary>
    /// This will append all data to a file.  The file will stay open of at most one min.
    /// </summary>
    public class FileInfluxWriter : InfluxWriterBuffer
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public readonly DirectoryInfo dir;
        public int counter = 0;
        private FileStream file = null;
        private DateTime opened = new DateTime();
        public bool dbCreated = false;

        public FileInfluxWriter(DirectoryInfo dir, int maxBytes) : base(maxBytes, 200)
        {
            this.dir = dir;
            dir.Create(); // make sure it exists
        }

        // Sending in a null buffer will force release of any open resources
        public override void doFlush(MemoryStream buffer)
        {
            bool emptyBuffer = (buffer == null || buffer.Length < 1);
            bool openForMin = (DateTime.Now - opened) > TimeSpan.FromMinutes(1);

            if (emptyBuffer || openForMin)
            {
                if(file != null)
                {
                    try
                    {
                        file.Close();
                    }
                    finally
                    {
                        file = null;
                    }
                }
                if(emptyBuffer)
                {
                    return;
                }
            }

            try
            {
                if(file == null)
                {
                    string fname = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + (++counter).ToString("0000") + ".txt";
                    file = File.Create(Path.Combine(dir.FullName, fname));
                    opened = DateTime.Now;
                }
                buffer.WriteTo(file);
                file.Flush();
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Error flusing to file: "+ex + " / " + dir.Name);
                if (file != null)
                {
                    try
                    {
                        file.Close();
                    }
                    catch (Exception) { }
                    finally
                    {
                        file = null;
                    }
                }
            }
        }
    }

    public class AccumulatingInfluxBuffer : InfluxWriter
    {
        public readonly List<string> lines = new List<string>();

        readonly int max;

        public AccumulatingInfluxBuffer(int max)
        {
            this.max = max;
        }

        public void clear()
        {
            lines.Clear();
        }

        public void writeLine(string line)
        {
            lines.Add(line);
            if(lines.Count>max)
            {
                lines.RemoveAt(0);
            }
        }

        public void flush()
        {

        }

        public void WriteDebugInfo(JsonWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
