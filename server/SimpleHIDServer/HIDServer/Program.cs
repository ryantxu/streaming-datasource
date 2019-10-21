using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Timers;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Grafana.Web;
using Newtonsoft.Json;
using NLog;
using Fleck;

using Hid = SharpLib.Hid;
using Win32 = SharpLib.Win32;

namespace HIDServer
{
    public class TheSockets
    {
        public List<IWebSocketConnection> open = new List<IWebSocketConnection>();
        public List<string> events = new List<string>();

        public InfluxWriter influx;
        private Timer timer;

        public TheSockets()
        {
            // Every 10s flush the data
            this.timer = new Timer(100);
            timer.Elapsed += new ElapsedEventHandler(this._timer_Elapsed);
        }

        public void write(DeviceValue value)
        {
            lock(this.events)
            {
                this.events.Add(value.ToJSON());
            }
            if(!timer.Enabled)
            {
                timer.Start();
            }

            if(this.influx!=null)
            {
                influx.writeLine(value.ToInfluxLine());
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(this.events.Count<1)
            {
                return; // nothing?  stop the timer?
            }

            StringBuilder str = new StringBuilder();
            lock (this.events)
            {
                Console.WriteLine("EVT " + DateTime.Now + " // " + this.events.Count);
                str.Append("{\"events\":[");
                for(int i=0; i<this.events.Count; i++)
                {
                    if(i!=0)
                    {
                        str.Append(",");
                    }
                    str.Append(this.events[i]);
                   // Console.WriteLine(" " + this.events[i]);
                }
                str.Append("]}");
                this.events.Clear();
            }

            foreach (IWebSocketConnection socket in this.open)
            {
                socket.Send(str.ToString());
            }

            //if (this.influx != null)
            //{
            //    influx.flush();
            //}
        }
    }

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();



        static void Main(string[] args)
        {
            logger.Info("Starting!");

            TheSockets sockets = new TheSockets();
            SimpleWebServer nws = new SimpleWebServer(4567);
            nws.Start();

            InfluxConnection c = new InfluxConnection();
            c.URL = "http://localhost:8086/";

           // HttpInfluxDB db = new HttpInfluxDB(c);
           // db.CreateDatabase("influxdays");
           // sockets.influx = new HttpInfluxWriter(db, "influxdays", 50000);


            foreach (var d in HidSharp.DeviceList.Local.GetHidDevices())
            {
                logger.Info("DEVICE: " + d.DevicePath);
                try
                {
                    logger.Info(" > " + d.GetFriendlyName());
                }
                catch { }
            }


            DeviceWatcher watcherSide = null;
            DeviceWatcher watcherWheel = null;

            HidSharp.HidDevice dev = DeviceWatcher.find("Saitek Side Panel Control Deck");
            if (dev != null)
            {
                watcherSide = new DeviceWatcher("panel_", dev, sockets);
            }

            dev = DeviceWatcher.find("Saitek Heavy Eqpt. Wheel & Pedal");
            if (dev != null)
            {
                watcherWheel = new DeviceWatcher("wheel_", dev, sockets);
            }

            WebSocketServer server = new WebSocketServer("ws://0.0.0.0:8181");
            server.RestartAfterListenError = true;
            server.Start(socket =>
            {
                socket.OnOpen = () => {
                    sockets.open.Add(socket);
                    logger.Info("WebSocket Open! " +sockets.open.Count);

                    if(watcherSide!=null)
                    {
                        watcherSide.broadcast();
                    }
                    if (watcherWheel != null)
                    {
                        watcherWheel.broadcast();
                    }

                };
                socket.OnClose = () => { sockets.open.Remove(socket); logger.Info("WebSocket Remove! " + sockets.open.Count); };
                socket.OnMessage = message => logger.Info("WebSocket: "+message);
            });
            

            while (true)
            {
                var x = Console.ReadLine();
                Console.WriteLine("ECHO: "+x);
            }
        }
    }
}
