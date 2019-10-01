using System;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Hosting.Self;
using Nancy.Conventions;
using Nancy.ErrorHandling;
using Nancy.Diagnostics;
using Nancy.TinyIoc;
using System.IO;
using Nancy.Bootstrapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nancy.Configuration;
using Nancy.Responses;
using NLog;

namespace Grafana.Web
{
    public class ErrorResponse : Nancy.Response
    {
        public ErrorResponse(HttpStatusCode code, string msg, Exception ex = null)
        {
            this.ContentType = "application/json";
            this.StatusCode = code;
            this.Headers.Add("Access-Control-Allow-Origin", "*"); // for CORS
            this.Contents = stream =>
            {
                TextWriter tw = new StreamWriter(stream);
                using (JsonWriter json = new JsonTextWriter(tw))
                {
                    json.Formatting = Formatting.Indented;
                    json.WriteStartObject();
                    json.WritePropertyName("code");
                    json.WriteValue(code);

                    json.WritePropertyName("msg");
                    json.WriteValue(msg);

                    if (ex != null)
                    {
                        // ???
                    }

                    json.WriteEndObject();
                }
            };
        }
    }

    public class CustomStatusCode : IStatusCodeHandler
    {
        public bool HandlesStatusCode(HttpStatusCode statusCode, NancyContext context)
        {
            return (int)statusCode == 404;
        }
        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
            if (!(context.Response is ErrorResponse))
            {
                context.Response = new ErrorResponse(statusCode, "Not Found");
            }
        }
    }

    class Bootstrapper : DefaultNancyBootstrapper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private byte[] favicon;

        public Bootstrapper()
        {

        }

        public override void Configure(INancyEnvironment environment)
        {
            environment.Diagnostics(true, "hydropower", "nancy");
            base.Configure(environment);
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            // Use the singleton agent
            base.ApplicationStartup(container, pipelines);

            // Enable CORS.  Only executes when asked for
            pipelines.AfterRequest += (ctx) =>
            {
                if (ctx.Request.Headers.Keys.Contains("Origin"))
                {
                    var origins = "" + string.Join(" ", ctx.Request.Headers["Origin"]);
                    ctx.Response.Headers["Access-Control-Allow-Origin"] = origins;

                    if (ctx.Request.Method == "OPTIONS")
                    {
                        // handle CORS preflight request
                        ctx.Response.Headers["Access-Control-Allow-Methods"] =
                            "GET, POST, PUT, DELETE, OPTIONS";

                        if (ctx.Request.Headers.Keys.Contains("Access-Control-Request-Headers"))
                        {
                            var allowedHeaders = "" + string.Join(
                                ", ", ctx.Request.Headers["Access-Control-Request-Headers"]);
                            ctx.Response.Headers["Access-Control-Allow-Headers"] = allowedHeaders;
                        }
                    }
                }
            };

            pipelines.OnError.AddItemToEndOfPipeline((ctx, err) =>
            {
                logger.Info("Got error: " + err);
                return new ErrorResponse(HttpStatusCode.InternalServerError, err.Message, err);
            });
        }


        protected override byte[] FavIcon
        {
            get { return this.favicon ?? (this.favicon = LoadFavIcon()); }
        }

        private byte[] LoadFavIcon()
        {
            using (var resourceStream = GetType().Assembly.GetManifestResourceStream("HIDServer.favicon.ico"))
            {
                var memoryStream = new MemoryStream();
                resourceStream.CopyTo(memoryStream);
                return memoryStream.GetBuffer();
            }
        }

        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {

        }

    }

    public class JsonWriterResponse : Nancy.Response
    {
        public JsonWriterResponse(Action<JsonWriter> writer)
        {
            this.ContentType = "application/json";
            this.Contents = stream =>
            {
                TextWriter tw = new StreamWriter(stream);
                using (JsonWriter json = new JsonTextWriter(tw))
                {
                    json.Formatting = Formatting.Indented;
                    writer(json);
                }
            };
        }
    }


    public class ApiModule : Nancy.NancyModule
    {
        public static long GetLongParam(string v, long defaultValue)
        {
            if (!String.IsNullOrWhiteSpace(v))
            {
                return long.Parse(v);
            }
            return defaultValue;
        }

        public ApiModule() : base("/api")
        {
            Get("/", _ => {
                return new JsonWriterResponse(writer =>
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("hello");
                    writer.WriteValue("xxxx");
                    writer.WriteEndObject();
                });
            });

            Get("/xxx.txt", _ =>
            {
                return "hello";
            });
        }
    }

    public class SimpleModule : Nancy.NancyModule
    {
        public SimpleModule()
        {
            Get("/", _ => {
                return Response.AsRedirect("/api");
            });
        }
    }


    public class SimpleWebServer
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        private NancyHost _nancy;
        private readonly Uri[] uris;

        public SimpleWebServer(int port)
        {
            var configuration = new HostConfiguration()
            {
                UrlReservations = new UrlReservations()
                {
                    CreateAutomatically = true
                },
                RewriteLocalhost = true
            };
            
            uris = new Uri[] {
                new Uri( "http://localhost:"+port)
            };
            
            Bootstrapper bs = new Bootstrapper();
            _nancy = new NancyHost(bs, configuration, uris);
        }

        public void Start()
        {
            _nancy.Start();
            foreach (Uri u in uris)
            {
                logger.Info("SimpleWebServer listening on: " + u);
            }
        }
    }
}
